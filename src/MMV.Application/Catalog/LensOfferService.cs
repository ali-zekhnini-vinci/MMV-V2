using System.Globalization;
using System.Text.Json;
using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stores;

namespace MMV.Application.Catalog;

/// <summary>Persistance des offres de verres (migration 0008).</summary>
public interface ILensOfferStore
{
    Task<LensOffer?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<LensOffer?> FindByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LensOffer>> ListAsync(Guid? rangeId, Guid? supplierId, bool includeArchived, CancellationToken cancellationToken);

    Task<IReadOnlyList<LensOfferRevision>> RevisionsAsync(Guid offerId, CancellationToken cancellationToken);

    /// <summary>
    /// GATE D : verrouille ces offres (<c>FOR NO KEY UPDATE</c>, ordre croissant des identifiants) dans la transaction en
    /// cours, avant la relecture du plan d'import : une modification concurrente attend la fin de l'import, ou l'import
    /// relit la valeur qu'elle a validée.
    /// </summary>
    Task LockAsync(IReadOnlyCollection<Guid> offerIds, CancellationToken cancellationToken);

    void Add(LensOffer offer);

    void Add(LensOfferRevision revision);
}

/// <summary>Saisie brute (chaînes décimales, jamais de flottant) d'une offre et de ses règles avancées.</summary>
public sealed record LensOfferInput(
    string? LensType, string? Material, Guid? IndexId, string? Production, int? LeadTimeDays, string? PriceHt, string? PriceCurrency,
    IReadOnlyList<int>? Diameters, string? SphereMin, string? SphereMax, string? CylinderMin, string? CylinderMax, string? AdditionMin, string? AdditionMax,
    IReadOnlyList<Guid>? TreatmentIds, IReadOnlyList<LensRuleInput>? Rules, bool RulesComplete, string? SourceReference, DateOnly? ValidFrom, DateOnly? ValidUntil,
    string? Notes, string? Reason);

public sealed record LensRangeInput(string? Min, string? Max);

public sealed record LensConditionsInput(
    IReadOnlyList<Guid>? TreatmentIds, IReadOnlyList<Guid>? IndexIds, string? Material, LensRangeInput? Sphere, LensRangeInput? Cylinder, LensRangeInput? Addition);

public sealed record LensRuleInput(
    string? Kind, string? Effect, string? Manufacturing, LensConditionsInput? Conditions, LensRangeInput? Range, IReadOnlyList<int>? Diameters,
    string? MaxSphereCylinderSum, string? SurchargeHt, int? ExtraLeadDays, string? Note);

public sealed record LensPrescriptionInput(string? Sphere, string? Cylinder, string? Addition, int? Diameter, IReadOnlyList<Guid>? TreatmentIds, Guid? IndexId);

/// <summary>
/// Offres fournisseur du référentiel verres (COR-05-05) : création, modification (révision + instantané), archivage,
/// réactivation, évaluation de compatibilité. Écriture : <c>catalog.configure</c> ; prix et suppléments (coûts) :
/// <c>costs.write</c> pour les modifier — refus 403 sinon, jamais d'effacement par omission. Règles STRUCTURÉES
/// validées une à une (types, bornes, références existantes) : aucune expression libre n'est acceptée ni évaluée.
/// </summary>
public sealed class LensOfferService(ICatalogUnitOfWork unitOfWork, ICatalogStore store, ILensOfferStore offers, TimeProvider clock, IAuditRecorder audit)
{
    public const decimal SphereLimit = 30m;
    public const decimal CylinderLimit = 15m;
    public const decimal AdditionLimit = 5m;
    public const int RuleNoteMaxLength = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Outcome<Guid>> CreateAsync(Guid? rangeId, Guid? supplierId, LensOfferInput input, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
        if (await offers.FindByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.CreatedByUserId == actor.UserId ? Outcome.Ok(existing.Id, replayed: true) : Outcome.Conflict<Guid>("request_id_reused");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var range = rangeId is { } r ? await store.FindLensReferenceAsync(r, cancellationToken) : null;
        if (range is null || range.Kind != LensReferenceKind.Range || range.IsArchived)
        {
            errors["rangeId"] = [rangeId is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown];
        }

        var supplier = supplierId is { } s ? await store.FindSupplierAsync(s, cancellationToken) : null;
        if (supplier is null || supplier.IsArchived)
        {
            errors["supplierId"] = [supplierId is null ? CatalogFieldErrors.Required : supplier is null ? CatalogFieldErrors.Unknown : CatalogFieldErrors.Archived];
        }

        var details = await ValidateAsync(input, errors, cancellationToken);
        if (details is null || errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        if ((details.PriceHt is not null || details.Rules.Any(x => x.SurchargeHt is not null)) && !actor.Has(Permissions.CostsWrite))
        {
            return Outcome.Forbidden<Guid>("costs_forbidden");
        }

        var now = CatalogClock.Now(clock);
        var offer = LensOffer.Create(Guid.CreateVersion7(now), rangeId!.Value, supplierId!.Value, details, requestId, actor.UserId, now);
        offers.Add(offer);
        offers.Add(LensOfferRevision.Create(Guid.CreateVersion7(now), offer.Id, offer.Revision, Snapshot(offer, details), input.Reason, actor.UserId, now));
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.CatalogModule, AuditActions.LensOfferCreated, AuditActions.LensOfferEntity, offer.Id,
            [CatalogAudit.Field("revision", null, "1")]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Status switch
        {
            CommitStatus.Saved => Outcome.Created(offer.Id),
            CommitStatus.UniqueViolation when commit.Constraint == "ux_lens_offers_creation_request_id" =>
                await offers.FindByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.CreatedByUserId == actor.UserId
                    ? Outcome.Ok(replay.Id, replayed: true)
                    : Outcome.Conflict<Guid>("request_id_reused"),
            _ => CatalogService.UniqueRefusal<Guid>(commit),
        };
    }

    public async Task<Outcome<Guid>> UpdateAsync(Guid id, LensOfferInput input, uint version, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
        var offer = await offers.FindAsync(id, cancellationToken);
        if (offer is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (offer.LastRequestId == requestId)
        {
            return Outcome.Ok(id, replayed: true);
        }

        if (offer.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (offer.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var details = await ValidateAsync(input, errors, cancellationToken);
        if (details is null || errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var before = offer.Snapshot();
        var costsChanged = before.PriceHt != details.PriceHt || before.PriceCurrency != details.PriceCurrency
            || !before.Rules.Select(x => x.SurchargeHt).SequenceEqual(details.Rules.Select(x => x.SurchargeHt));
        if (costsChanged && !actor.Has(Permissions.CostsWrite))
        {
            return Outcome.Forbidden<Guid>("costs_forbidden");
        }

        var changed = ChangedFields(before, details);
        if (changed.Count == 0)
        {
            return Outcome.Ok(id);
        }

        var now = CatalogClock.Now(clock);
        offer.Update(details, actor.UserId, now, requestId);
        offers.Add(LensOfferRevision.Create(Guid.CreateVersion7(now), offer.Id, offer.Revision, Snapshot(offer, details), input.Reason, actor.UserId, now));
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.CatalogModule, AuditActions.LensOfferUpdated, AuditActions.LensOfferEntity, offer.Id,
            [.. CatalogAudit.Fields(changed), CatalogAudit.Field("revision", (offer.Revision - 1).ToString(CultureInfo.InvariantCulture), offer.Revision.ToString(CultureInfo.InvariantCulture))]);
        return CatalogService.Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }

    public async Task<Outcome<Guid>> SetArchivedAsync(Guid id, bool archive, string? reason, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var offer = await offers.FindAsync(id, cancellationToken);
        if (offer is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (offer.LastRequestId == requestId)
        {
            return Outcome.Ok(id, replayed: true);
        }

        if (offer.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (offer.IsArchived == archive)
        {
            return Outcome.Conflict<Guid>(archive ? "archived" : "not_archived");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = CatalogText.Clean(reason, "reason", CatalogLimits.ReasonMaxLength, required: false, errors, multiline: true);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var now = CatalogClock.Now(clock);
        if (archive)
        {
            offer.Archive(text, userId, now, requestId);
        }
        else
        {
            offer.Reactivate(userId, now, requestId);
        }

        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, archive ? AuditActions.LensOfferArchived : AuditActions.LensOfferReactivated,
            AuditActions.LensOfferEntity, offer.Id, [CatalogAudit.Field("status", archive ? "active" : "archived", archive ? "archived" : "active")]);
        return CatalogService.Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }

    /// <summary>Évaluation SANS écriture d'une correction contre une offre (trois issues, jamais une validation clinique).</summary>
    public async Task<Outcome<LensCompatibility>> CheckAsync(Guid id, LensPrescriptionInput input, CancellationToken cancellationToken)
    {
        var offer = await offers.FindAsync(id, cancellationToken);
        if (offer is null)
        {
            return Outcome.Missing<LensCompatibility>();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var rx = Prescription(input, errors);
        return rx is null || errors.Count > 0
            ? Outcome.Invalid<LensCompatibility>(errors)
            : Outcome.Ok(LensCompatibilityEvaluator.Evaluate(offer.Capabilities(), rx, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
    }

    public static LensPrescription? Prescription(LensPrescriptionInput input, IDictionary<string, string[]> errors)
    {
        var sphere = Diopter(input.Sphere, "sphere", SphereLimit, errors);
        var cylinder = Diopter(input.Cylinder, "cylinder", CylinderLimit, errors);
        var addition = Diopter(input.Addition, "addition", AdditionLimit, errors);
        if (addition is < 0)
        {
            errors["addition"] = [CatalogFieldErrors.OutOfRange];
        }

        if (input.Diameter is < 30 or > 90)
        {
            errors["diameter"] = [CatalogFieldErrors.OutOfRange];
        }

        return errors.Count > 0 ? null : new LensPrescription(sphere, cylinder, addition, input.Diameter, input.TreatmentIds ?? [], input.IndexId);
    }

    // ---------- validation ----------

    /// <summary>Validation SANS écriture d'une saisie d'offre (import du référentiel des verres, contrôle à blanc).</summary>
    public Task<LensOfferDetails?> ValidateInputAsync(LensOfferInput input, Dictionary<string, string[]> errors, CancellationToken cancellationToken) =>
        ValidateAsync(input, errors, cancellationToken);

    private async Task<LensOfferDetails?> ValidateAsync(LensOfferInput input, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        var references = await store.ListLensReferencesAsync(cancellationToken);
        bool Active(Guid id, LensReferenceKind kind) => references.Any(r => r.Id == id && r.Kind == kind && !r.IsArchived);

        if (input.LensType is null || !LensTypes.All.Contains(input.LensType))
        {
            errors["lensType"] = [input.LensType is null ? CatalogFieldErrors.Required : CatalogFieldErrors.InvalidFormat];
        }

        if (input.Production is null || !LensProduction.All.Contains(input.Production))
        {
            errors["production"] = [input.Production is null ? CatalogFieldErrors.Required : CatalogFieldErrors.InvalidFormat];
        }

        var material = CatalogText.Clean(input.Material, "material", LensOffer.MaterialMaxLength, required: false, errors);
        if (input.IndexId is { } indexId && !Active(indexId, LensReferenceKind.Index))
        {
            errors["indexId"] = [CatalogFieldErrors.Unknown];
        }

        if (input.LeadTimeDays is < 0 or > 365)
        {
            errors["leadTimeDays"] = [CatalogFieldErrors.OutOfRange];
        }

        var price = input.PriceHt is null ? null : MoneyText.TryParse(input.PriceHt.Replace(',', '.'), MoneyText.PurchaseScale);
        if (input.PriceHt is not null && price is null or < 0 or > MoneyText.MaxPurchasePrice)
        {
            errors["priceHt"] = [CatalogFieldErrors.InvalidFormat];
        }

        var currency = input.PriceCurrency?.Trim().ToUpperInvariant();
        if ((price is null) != string.IsNullOrEmpty(currency) || (currency is { Length: > 0 } && !Currencies.IsSupported(currency)))
        {
            errors["priceCurrency"] = [CatalogFieldErrors.InvalidFormat];
        }

        var diameters = (input.Diameters ?? []).Distinct().Order().ToList();
        if (diameters.Count > LensOffer.MaxDiameters || diameters.Any(d => d is < 30 or > 90))
        {
            errors["diameters"] = [CatalogFieldErrors.OutOfRange];
        }

        var sphere = Range(new LensRangeInput(input.SphereMin, input.SphereMax), "sphere", SphereLimit, errors);
        var cylinder = Range(new LensRangeInput(input.CylinderMin, input.CylinderMax), "cylinder", CylinderLimit, errors);
        var addition = Range(new LensRangeInput(input.AdditionMin, input.AdditionMax), "addition", AdditionLimit, errors);
        if (addition is { Min: < 0 })
        {
            errors["addition"] = [CatalogFieldErrors.OutOfRange];
        }

        var treatments = (input.TreatmentIds ?? []).Distinct().ToList();
        if (treatments.Any(t => !Active(t, LensReferenceKind.Treatment)))
        {
            errors["treatmentIds"] = [CatalogFieldErrors.Unknown];
        }

        var source = CatalogText.Clean(input.SourceReference, "sourceReference", LensOffer.SourceMaxLength, required: false, errors);
        var notes = CatalogText.Clean(input.Notes, "notes", LensOffer.NotesMaxLength, required: false, errors, multiline: true);
        CatalogText.Clean(input.Reason, "reason", LensOfferRevision.ReasonMaxLength, required: false, errors, multiline: true);
        if (input.ValidFrom is { } from && input.ValidUntil is { } until && from > until)
        {
            errors["validUntil"] = [CatalogFieldErrors.OutOfRange];
        }

        var rules = new List<LensRule>();
        var ruleInputs = input.Rules ?? [];
        if (ruleInputs.Count > LensOffer.MaxRules)
        {
            errors["rules"] = [CatalogFieldErrors.OutOfRange];
        }

        for (var i = 0; i < ruleInputs.Count && i < LensOffer.MaxRules; i++)
        {
            if (Rule(ruleInputs[i], $"rules[{i}].", Active, errors) is { } rule)
            {
                rules.Add(rule);
            }
        }

        return errors.Count > 0
            ? null
            : new LensOfferDetails(input.LensType!, material, input.IndexId, input.Production!, input.LeadTimeDays, price, price is null ? null : currency, diameters,
                sphere, cylinder, addition, treatments, rules, input.RulesComplete, source, input.ValidFrom, input.ValidUntil, notes);
    }

    private static LensRule? Rule(LensRuleInput input, string prefix, Func<Guid, LensReferenceKind, bool> active, Dictionary<string, string[]> errors)
    {
        var before = errors.Count;
        if (input.Kind is null || !LensRuleKinds.All.Contains(input.Kind))
        {
            errors[prefix + "kind"] = [CatalogFieldErrors.InvalidFormat];
        }

        if (input.Effect is null || !LensRuleEffects.All.Contains(input.Effect))
        {
            errors[prefix + "effect"] = [CatalogFieldErrors.InvalidFormat];
        }

        var manufacturing = input.Manufacturing ?? LensManufacturing.Standard;
        if (!LensManufacturing.All.Contains(manufacturing))
        {
            errors[prefix + "manufacturing"] = [CatalogFieldErrors.InvalidFormat];
        }

        var kind = input.Kind ?? string.Empty;
        var limit = kind == LensRuleKinds.CylinderRange ? CylinderLimit : kind == LensRuleKinds.AdditionRange ? AdditionLimit : SphereLimit;
        var range = input.Range is null ? null : Range(input.Range, prefix + "range", limit, errors);
        if (LensRuleKinds.IsRange(kind) && range is null)
        {
            errors[prefix + "range"] = [CatalogFieldErrors.Required];
        }

        var diameters = input.Diameters?.Distinct().Order().ToList();
        if (kind == LensRuleKinds.Diameters && (diameters is null || diameters.Count == 0 || diameters.Count > LensOffer.MaxDiameters || diameters.Any(d => d is < 30 or > 90)))
        {
            errors[prefix + "diameters"] = [CatalogFieldErrors.OutOfRange];
        }

        var maxSum = Diopter(input.MaxSphereCylinderSum, prefix + "maxSphereCylinderSum", SphereLimit + CylinderLimit, errors);
        if (kind == LensRuleKinds.CombinationLimit && maxSum is null or <= 0)
        {
            errors[prefix + "maxSphereCylinderSum"] = [CatalogFieldErrors.Required];
        }

        var surcharge = input.SurchargeHt is null ? null : MoneyText.TryParse(input.SurchargeHt.Replace(',', '.'), MoneyText.PurchaseScale);
        if (input.SurchargeHt is not null && surcharge is null or < 0 or > 99_999m)
        {
            errors[prefix + "surchargeHt"] = [CatalogFieldErrors.InvalidFormat];
        }

        if (input.ExtraLeadDays is < 0 or > 365)
        {
            errors[prefix + "extraLeadDays"] = [CatalogFieldErrors.OutOfRange];
        }

        var note = CatalogText.Clean(input.Note, prefix + "note", RuleNoteMaxLength, required: false, errors);
        LensRuleConditions? conditions = null;
        if (input.Conditions is { } c)
        {
            if ((c.TreatmentIds ?? []).Any(t => !active(t, LensReferenceKind.Treatment)))
            {
                errors[prefix + "conditions.treatmentIds"] = [CatalogFieldErrors.Unknown];
            }

            if ((c.IndexIds ?? []).Any(t => !active(t, LensReferenceKind.Index)))
            {
                errors[prefix + "conditions.indexIds"] = [CatalogFieldErrors.Unknown];
            }

            var material = CatalogText.Clean(c.Material, prefix + "conditions.material", LensOffer.MaterialMaxLength, required: false, errors);
            conditions = new LensRuleConditions(
                c.TreatmentIds is { Count: > 0 } ? [.. c.TreatmentIds.Distinct()] : null, c.IndexIds is { Count: > 0 } ? [.. c.IndexIds.Distinct()] : null, material,
                c.Sphere is null ? null : Range(c.Sphere, prefix + "conditions.sphere", SphereLimit, errors),
                c.Cylinder is null ? null : Range(c.Cylinder, prefix + "conditions.cylinder", CylinderLimit, errors),
                c.Addition is null ? null : Range(c.Addition, prefix + "conditions.addition", AdditionLimit, errors));
            if (conditions.Specificity == 0)
            {
                conditions = null;
            }
        }

        return errors.Count > before
            ? null
            : new LensRule(kind, input.Effect!, manufacturing, conditions, range, kind == LensRuleKinds.Diameters ? diameters : null,
                kind == LensRuleKinds.CombinationLimit ? maxSum : null, surcharge, input.ExtraLeadDays, note);
    }

    private static decimal? Diopter(string? text, string field, decimal limit, IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Valeur signée (dioptries) : signe explicite admis, au plus deux décimales, jamais de flottant.
        var trimmed = text.Trim().Replace(',', '.');
        var negative = trimmed.StartsWith('-');
        var magnitude = MoneyText.TryParse(trimmed.TrimStart('+', '-'), 2);
        var value = magnitude is null ? null : negative ? -magnitude : magnitude;
        if (value is null || Math.Abs(value.Value) > limit)
        {
            errors[field] = [value is null ? CatalogFieldErrors.InvalidFormat : CatalogFieldErrors.OutOfRange];
            return null;
        }

        return value;
    }

    private static DiopterRange? Range(LensRangeInput input, string field, decimal limit, Dictionary<string, string[]> errors)
    {
        var min = Diopter(input.Min, field + "Min", limit, errors);
        var max = Diopter(input.Max, field + "Max", limit, errors);
        if (min is null && max is null)
        {
            return null;
        }

        if (min is null || max is null || min > max)
        {
            errors[field] = [min is null || max is null ? CatalogFieldErrors.Required : CatalogFieldErrors.OutOfRange];
            return null;
        }

        return new DiopterRange(min.Value, max.Value);
    }

    private static List<string> ChangedFields(LensOfferDetails before, LensOfferDetails after)
    {
        var fields = new List<string>();
        void Check(string name, object? a, object? b)
        {
            if (JsonSerializer.Serialize(a, Json) != JsonSerializer.Serialize(b, Json))
            {
                fields.Add(name);
            }
        }

        Check("lensType", before.LensType, after.LensType);
        Check("material", before.Material, after.Material);
        Check("indexId", before.IndexId, after.IndexId);
        Check("production", before.Production, after.Production);
        Check("leadTimeDays", before.LeadTimeDays, after.LeadTimeDays);
        // Paires comparées champ par champ : un ValueTuple se sérialise en « {} » (champs ignorés par System.Text.Json),
        // si bien qu'un changement du seul prix ou de la seule validité passait inaperçu (correction post-audit 06/11/2026).
        if (before.PriceHt != after.PriceHt || !string.Equals(before.PriceCurrency, after.PriceCurrency, StringComparison.Ordinal))
        {
            fields.Add("priceHt");
        }

        Check("diameters", before.Diameters, after.Diameters);
        Check("sphere", before.Sphere, after.Sphere);
        Check("cylinder", before.Cylinder, after.Cylinder);
        Check("addition", before.Addition, after.Addition);
        Check("treatmentIds", before.TreatmentIds.Order(), after.TreatmentIds.Order());
        Check("rules", before.Rules, after.Rules);
        Check("rulesComplete", before.RulesComplete, after.RulesComplete);
        Check("sourceReference", before.SourceReference, after.SourceReference);
        if (before.ValidFrom != after.ValidFrom || before.ValidUntil != after.ValidUntil)
        {
            fields.Add("validity");
        }

        Check("notes", before.Notes, after.Notes);
        return fields;
    }

    private static string Snapshot(LensOffer offer, LensOfferDetails details) =>
        JsonSerializer.Serialize(new { offer.RangeId, offer.SupplierId, details }, Json);
}
