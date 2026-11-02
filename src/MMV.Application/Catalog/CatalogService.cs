using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Suppliers;

namespace MMV.Application.Catalog;

/// <summary>Raisons bornées d'un refus d'archivage ou de changement de politique (D-05-02, D-05-07 — QR-02 appliquée).</summary>
public static class CatalogRefusals
{
    public const string StockOnHand = "stock_on_hand";
    public const string StockReserved = "stock_reserved";
    public const string InventoryOpen = "inventory_open";
    public const string ModelArchived = "model_archived";
}

/// <summary>
/// Cas d'usage du catalogue (05-A) : modèles et variantes, politique de stock, archivage / réactivation, référentiel des
/// verres, rattachements fournisseurs. Chaque écriture porte un identifiant de requête (réémission = état courant, rien
/// n'est réécrit) et, sauf la création, la version lue (<c>xmin</c>) ; audit dans la même transaction, sans aucun coût.
/// </summary>
public sealed class CatalogService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IPricingStore pricing, TimeProvider clock, IAuditRecorder audit)
{
    // ---------- modèles ----------

    /// <summary>Création d'un modèle et de ses variantes (au moins une) en une transaction.</summary>
    public async Task<Outcome<Guid>> CreateModelAsync(ModelInput input, IReadOnlyList<VariantInput> variants, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (await store.FindModelByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.CreatedByUserId == userId ? Outcome.Ok<Guid>(existing.Id, replayed: true) : Outcome.Conflict<Guid>("request_id_reused");
        }

        // SKU absent : généré par le serveur (COR-05-06) ; une saisie explicite reste possible et contrôlée.
        variants = await WithGeneratedSkusAsync(input.FamilyCode, variants, cancellationToken);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var details = CatalogInputValidator.Model(input, errors);
        if (variants.Count == 0)
        {
            errors["variants"] = [CatalogFieldErrors.Required];
        }
        else if (variants.Count > CatalogLimits.MaxVariantsPerModel)
        {
            errors["variants"] = [CatalogFieldErrors.OutOfRange];
        }

        var family = Families.Find(input.FamilyCode);
        var validated = new List<(ProductVariantDetails Details, StockPolicy Policy)>();
        if (family is not null)
        {
            for (var i = 0; i < variants.Count; i++)
            {
                var (variant, policy) = CatalogInputValidator.Variant(variants[i], family, requirePolicy: true, errors, $"variants[{i}].");
                if (variant is not null)
                {
                    validated.Add((variant, policy!.Value));
                }
            }
        }

        if (details is null || errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await CheckReferencesAsync(details, validated.Select(v => v.Details).ToList(), null, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var now = CatalogClock.Now(clock);
        var model = ProductModel.Create(Guid.CreateVersion7(now), details, requestId, userId, now);
        store.Add(model);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.ModelCreated, AuditActions.ModelEntity, model.Id,
            CatalogAudit.Fields(ModelFields(null, details)));
        foreach (var (variantDetails, policy) in validated)
        {
            var variant = ProductVariant.Create(Guid.CreateVersion7(now), model.Id, variantDetails, policy, null, userId, now);
            store.Add(variant);
            RecordVariantCreated(variant, userId, now);
        }

        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Status switch
        {
            CommitStatus.Saved => Outcome.Created<Guid>(model.Id),
            CommitStatus.UniqueViolation when commit.Constraint == "ux_product_models_creation_request_id" =>
                await store.FindModelByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.CreatedByUserId == userId
                    ? Outcome.Ok<Guid>(replay.Id, replayed: true)
                    : Outcome.Conflict<Guid>("request_id_reused"),
            _ => UniqueRefusal<Guid>(commit),
        };
    }

    /// <summary>Complète les SKU vides par des SKU générés, distincts entre eux et non déjà pris (5 essais par variante).</summary>
    internal async Task<IReadOnlyList<VariantInput>> WithGeneratedSkusAsync(string? familyCode, IReadOnlyList<VariantInput> variants, CancellationToken cancellationToken)
    {
        if (variants.All(v => !string.IsNullOrWhiteSpace(v.Sku)))
        {
            return variants;
        }

        var used = new HashSet<string>(variants.Where(v => !string.IsNullOrWhiteSpace(v.Sku)).Select(v => CatalogKeys.Normalize(v.Sku!)), StringComparer.Ordinal);
        var result = new List<VariantInput>(variants.Count);
        foreach (var variant in variants)
        {
            if (!string.IsNullOrWhiteSpace(variant.Sku))
            {
                result.Add(variant);
                continue;
            }

            var sku = SkuGenerator.Propose(familyCode);
            for (var attempt = 0; attempt < 5 && (used.Contains(CatalogKeys.Normalize(sku))
                || (await store.TakenSkusAsync([CatalogKeys.Normalize(sku)], null, cancellationToken)).Count > 0); attempt++)
            {
                sku = SkuGenerator.Propose(familyCode);
            }

            used.Add(CatalogKeys.Normalize(sku));
            result.Add(variant with { Sku = sku });
        }

        return result;
    }

    public async Task<Outcome<Guid>> UpdateModelAsync(Guid id, ModelInput input, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var model = await store.FindModelAsync(id, cancellationToken);
        if (model is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (model.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (model.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (model.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var details = CatalogInputValidator.Model(input with { FamilyCode = input.FamilyCode ?? model.FamilyCode }, errors);
        if (details is not null && details.FamilyCode != model.FamilyCode)
        {
            errors["familyCode"] = [CatalogFieldErrors.NotAllowed];
        }

        if (details is null || errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var before = model.Snapshot();
        if (before.SameAs(details))
        {
            return Outcome.Ok<Guid>(id);
        }

        await CheckReferencesAsync(details, [], null, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var now = CatalogClock.Now(clock);
        model.Update(details, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.ModelUpdated, AuditActions.ModelEntity, id,
            CatalogAudit.Fields(ModelFields(before, details)));
        return Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }

    public async Task<Outcome<Guid>> AddVariantAsync(Guid modelId, VariantInput input, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (await store.FindVariantByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.ModelId == modelId && existing.CreatedByUserId == userId
                ? Outcome.Ok<Guid>(existing.Id, replayed: true)
                : Outcome.Conflict<Guid>("request_id_reused");
        }

        var model = await store.FindModelAsync(modelId, cancellationToken);
        if (model is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (model.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        input = (await WithGeneratedSkusAsync(model.FamilyCode, [input], cancellationToken))[0];

        if (model.Variants.Count >= CatalogLimits.MaxVariantsPerModel)
        {
            return Outcome.Invalid<Guid>("variants", CatalogFieldErrors.OutOfRange);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (details, policy) = CatalogInputValidator.Variant(input, Families.Find(model.FamilyCode)!, requirePolicy: true, errors);
        if (details is null)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await CheckReferencesAsync(null, [details], null, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var now = CatalogClock.Now(clock);
        var variant = ProductVariant.Create(Guid.CreateVersion7(now), modelId, details, policy!.Value, requestId, userId, now);
        store.Add(variant);
        RecordVariantCreated(variant, userId, now);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Status switch
        {
            CommitStatus.Saved => Outcome.Created<Guid>(variant.Id),
            CommitStatus.UniqueViolation when commit.Constraint == "ux_product_variants_creation_request_id" =>
                await store.FindVariantByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.CreatedByUserId == userId
                    ? Outcome.Ok<Guid>(replay.Id, replayed: true)
                    : Outcome.Conflict<Guid>("request_id_reused"),
            _ => UniqueRefusal<Guid>(commit),
        };
    }

    public async Task<Outcome<Guid>> UpdateVariantAsync(Guid id, VariantInput input, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var variant = await store.FindVariantAsync(id, cancellationToken);
        if (variant is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (variant.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (variant.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (variant.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var model = (await store.FindModelAsync(variant.ModelId, cancellationToken))!;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (details, _) = CatalogInputValidator.Variant(input, Families.Find(model.FamilyCode)!, requirePolicy: false, errors);
        if (details is null)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var before = variant.Snapshot();
        if (before.SameAs(details))
        {
            return Outcome.Ok<Guid>(id);
        }

        await CheckReferencesAsync(null, [details], id, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var now = CatalogClock.Now(clock);
        variant.Update(details, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantUpdated, AuditActions.VariantEntity, id,
            CatalogAudit.Fields(VariantFields(before, details)));
        return Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }

    /// <summary>
    /// Changement de politique (D-05-02 (a), règle proposée appliquée) : vers ou depuis <c>stocked</c>, quantité en main et
    /// réservée nulles dans tous les emplacements et aucun inventaire ouvert. Variante verrouillée <c>FOR NO KEY UPDATE</c>
    /// AVANT la lecture des soldes : une écriture de stock concurrente (qui la tient <c>FOR SHARE</c>) est attendue.
    /// </summary>
    public async Task<Outcome<Guid>> ChangePolicyAsync(Guid id, string? policyCode, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (StockPolicies.TryParse(policyCode) is not { } policy)
        {
            return Outcome.Invalid<Guid>("stockPolicy", policyCode is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await store.LockVariantAsync(id, RowLock.NoKeyUpdate, cancellationToken))
        {
            return Outcome.Missing<Guid>();
        }

        var variant = (await store.FindVariantAsync(id, cancellationToken))!;
        if (variant.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (variant.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (variant.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var model = (await store.FindModelAsync(variant.ModelId, cancellationToken))!;
        if (!Families.Find(model.FamilyCode)!.AllowedPolicies.Contains(policy))
        {
            return Outcome.Invalid<Guid>("stockPolicy", CatalogFieldErrors.NotAllowed);
        }

        var previous = variant.StockPolicy;
        if (previous == policy)
        {
            return Outcome.Ok<Guid>(id);
        }

        if (previous == StockPolicy.Stocked || policy == StockPolicy.Stocked)
        {
            if (await StockRefusalAsync(id, cancellationToken) is { } reason)
            {
                return Outcome.Refuse<Guid>("policy_change_refused", new { reason });
            }
        }

        var now = CatalogClock.Now(clock);
        variant.ChangePolicy(policy, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantPolicyChanged, AuditActions.VariantEntity, id,
            [CatalogAudit.Field("stockPolicy", StockPolicies.ToCode(previous), StockPolicies.ToCode(policy))]);
        return await CommitAsync(transaction, id, cancellationToken);
    }

    /// <summary>Archivage d'une variante (D-05-07 option A adaptée, QR-02) : refusé si stock en main ou réservé, ou inventaire ouvert.</summary>
    public async Task<Outcome<Guid>> ArchiveVariantAsync(Guid id, string? reason, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = CatalogText.Clean(reason, "reason", CatalogLimits.ReasonMaxLength, required: false, errors, multiline: true);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await store.LockVariantAsync(id, RowLock.NoKeyUpdate, cancellationToken))
        {
            return Outcome.Missing<Guid>();
        }

        var variant = (await store.FindVariantAsync(id, cancellationToken))!;
        if (variant.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (variant.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (variant.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        if (await StockRefusalAsync(id, cancellationToken) is { } refusal)
        {
            return Outcome.Refuse<Guid>("archive_refused", new { reason = refusal });
        }

        var now = CatalogClock.Now(clock);
        variant.Archive(text, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantArchived, AuditActions.VariantEntity, id,
            text is null ? [] : [CatalogAudit.Field("reason")]);
        return await CommitAsync(transaction, id, cancellationToken);
    }

    public async Task<Outcome<Guid>> ReactivateVariantAsync(Guid id, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var variant = await store.FindVariantAsync(id, cancellationToken);
        if (variant is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (variant.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (variant.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (!variant.IsArchived)
        {
            return Outcome.Conflict<Guid>("not_archived");
        }

        if ((await store.FindModelAsync(variant.ModelId, cancellationToken))!.IsArchived)
        {
            return Outcome.Refuse<Guid>("reactivation_refused", new { reason = CatalogRefusals.ModelArchived });
        }

        var now = CatalogClock.Now(clock);
        variant.Reactivate(userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantReactivated, AuditActions.VariantEntity, id, []);
        return Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }

    /// <summary>Archivage d'un modèle : archive aussi ses variantes actives, toutes ou aucune (mêmes conditions de stock).</summary>
    public async Task<Outcome<Guid>> ArchiveModelAsync(Guid id, string? reason, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = CatalogText.Clean(reason, "reason", CatalogLimits.ReasonMaxLength, required: false, errors, multiline: true);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var model = await store.FindModelAsync(id, cancellationToken);
        if (model is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (model.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (model.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (model.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var active = model.Variants.Where(v => !v.IsArchived).Select(v => v.Id).ToList();
        await store.LockVariantsAsync(active, RowLock.NoKeyUpdate, cancellationToken);
        foreach (var variantId in active.Order())
        {
            if (await StockRefusalAsync(variantId, cancellationToken) is { } refusal)
            {
                return Outcome.Refuse<Guid>("archive_refused", new { reason = refusal, variantId });
            }
        }

        var now = CatalogClock.Now(clock);
        foreach (var variant in model.Variants.Where(v => !v.IsArchived))
        {
            variant.Archive(text, userId, now, requestId);
            CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantArchived, AuditActions.VariantEntity, variant.Id, []);
        }

        model.Archive(text, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.ModelArchived, AuditActions.ModelEntity, id,
            [CatalogAudit.Field("lineCount", null, active.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)), .. text is null ? Array.Empty<AuditChange>() : [CatalogAudit.Field("reason")]]);
        return await CommitAsync(transaction, id, cancellationToken);
    }

    /// <summary>Réactivation d'un modèle seul (ses variantes restent archivées : réactivation explicite de chacune).</summary>
    public async Task<Outcome<Guid>> ReactivateModelAsync(Guid id, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var model = await store.FindModelAsync(id, cancellationToken);
        if (model is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (model.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (model.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (!model.IsArchived)
        {
            return Outcome.Conflict<Guid>("not_archived");
        }

        var now = CatalogClock.Now(clock);
        model.Reactivate(userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.ModelReactivated, AuditActions.ModelEntity, id, []);
        return Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }

    // ---------- référentiel verres (D-05-21) ----------

    public async Task<Outcome<Guid>> CreateLensReferenceAsync(
        LensReferenceKind kind, string? name, Guid? manufacturerId, string? indexValue, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (await store.FindLensReferenceByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.CreatedByUserId == userId ? Outcome.Ok<Guid>(existing.Id, replayed: true) : Outcome.Conflict<Guid>("request_id_reused");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (text, value) = LensFields(kind, name, indexValue, errors);
        if (kind == LensReferenceKind.Range && manufacturerId is null)
        {
            errors["manufacturerId"] = [CatalogFieldErrors.Required];
        }
        else if (kind is LensReferenceKind.Manufacturer or LensReferenceKind.Index && manufacturerId is not null)
        {
            errors["manufacturerId"] = [CatalogFieldErrors.NotAllowed];
        }
        else if (manufacturerId is { } parent)
        {
            var manufacturer = await store.FindLensReferenceAsync(parent, cancellationToken);
            if (manufacturer is not { Kind: LensReferenceKind.Manufacturer })
            {
                errors["manufacturerId"] = [CatalogFieldErrors.Unknown];
            }
            else if (manufacturer.IsArchived)
            {
                errors["manufacturerId"] = [CatalogFieldErrors.Archived];
            }
        }

        if (errors.Count > 0 || text is null)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var key = value is { } v ? LensReferenceKinds.IndexKey(v) : CatalogKeys.Normalize(text);
        if (await store.LensReferenceTakenAsync(LensReferenceKinds.ToCode(kind), manufacturerId, key, null, cancellationToken))
        {
            return Outcome.Invalid<Guid>(value is null ? "name" : "indexValue", CatalogFieldErrors.Duplicate);
        }

        var now = CatalogClock.Now(clock);
        var reference = LensReference.Create(Guid.CreateVersion7(now), kind, text, manufacturerId, value, requestId, userId, now);
        store.Add(reference);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.LensReferenceCreated, AuditActions.LensReferenceEntity, reference.Id,
            [CatalogAudit.Field("kind", null, LensReferenceKinds.ToCode(kind))]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? Outcome.Created<Guid>(reference.Id) : UniqueRefusal<Guid>(commit, value is null ? "name" : "indexValue");
    }

    public async Task<Outcome<Guid>> UpdateLensReferenceAsync(Guid id, string? name, string? indexValue, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var reference = await store.FindLensReferenceAsync(id, cancellationToken);
        if (reference is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (reference.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (reference.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (reference.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (text, value) = LensFields(reference.Kind, name, indexValue, errors);
        if (errors.Count > 0 || text is null)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        if (text == reference.Name && value == reference.IndexValue)
        {
            return Outcome.Ok<Guid>(id);
        }

        var key = value is { } v ? LensReferenceKinds.IndexKey(v) : CatalogKeys.Normalize(text);
        if (await store.LensReferenceTakenAsync(reference.KindCode, reference.ManufacturerId, key, id, cancellationToken))
        {
            return Outcome.Invalid<Guid>(value is null ? "name" : "indexValue", CatalogFieldErrors.Duplicate);
        }

        var now = CatalogClock.Now(clock);
        reference.Rename(text, value, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.LensReferenceUpdated, AuditActions.LensReferenceEntity, id,
            [CatalogAudit.Field(value is null ? "name" : "indexValue")]);
        return Commit(await unitOfWork.SaveAsync(cancellationToken), id, value is null ? "name" : "indexValue");
    }

    /// <summary>Archivage d'un élément du référentiel : les fiches existantes le gardent ; il n'est plus proposé.</summary>
    public async Task<Outcome<Guid>> SetLensReferenceArchivedAsync(Guid id, bool archive, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var reference = await store.FindLensReferenceAsync(id, cancellationToken);
        if (reference is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (reference.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (reference.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (reference.IsArchived == archive)
        {
            return Outcome.Conflict<Guid>(archive ? "archived" : "not_archived");
        }

        var now = CatalogClock.Now(clock);
        if (archive)
        {
            reference.Archive(null, userId, now, requestId);
        }
        else
        {
            reference.Reactivate(userId, now, requestId);
        }

        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule,
            archive ? AuditActions.LensReferenceArchived : AuditActions.LensReferenceReactivated, AuditActions.LensReferenceEntity, id, []);
        return Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }

    // ---------- rattachements fournisseurs (D-05-06) ----------

    /// <summary>
    /// Crée ou modifie le rattachement (variante, fournisseur). Prix fournisseur : <c>costs.write</c> exigé (sinon 403, rien
    /// n'est écrit) ; historisé ; ne modifie JAMAIS le prix d'achat HT de référence. Fournisseur archivé : ni nouveau
    /// rattachement, ni principal. Un seul principal : l'ancien principal perd ce rôle dans la même transaction.
    /// </summary>
    public async Task<Outcome<Guid>> SetSupplierLinkAsync(
        Guid variantId, Guid supplierId, VariantSupplierInput input, uint? version, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
        if (input.PriceSpecified && !actor.Has(Permissions.CostsWrite))
        {
            return Outcome.Forbidden<Guid>("costs_forbidden");
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        // Ordre : variante (sérialise les changements de principal), puis fournisseur (archivage concurrent).
        if (!await store.LockVariantAsync(variantId, RowLock.NoKeyUpdate, cancellationToken))
        {
            return Outcome.Missing<Guid>();
        }

        if (!await store.LockSupplierAsync(supplierId, RowLock.Share, cancellationToken))
        {
            return Outcome.Missing<Guid>("supplier_not_found");
        }

        var variant = (await store.FindVariantAsync(variantId, cancellationToken))!;
        var supplier = (await store.FindSupplierAsync(supplierId, cancellationToken))!;
        var links = await store.LinksForVariantAsync(variantId, cancellationToken);
        var link = links.FirstOrDefault(l => l.SupplierId == supplierId);
        if (link?.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(variantId, replayed: true);
        }

        // Nouveau rattachement (ou ré-ajout d'un rattachement retiré) : version absente ou 0 admise ; sinon version lue exigée.
        var fresh = link is null || link.IsRemoved;
        if (fresh ? version is not (null or 0) && version != link?.Version : version != link!.Version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (variant.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var current = link is { IsRemoved: false } ? link.Snapshot() : null;
        var details = CatalogInputValidator.Link(input, current, errors);
        if (details is null)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        if (supplier.IsArchived && (current is null || details.IsPrimary))
        {
            return Outcome.Invalid<Guid>(current is null ? "supplierId" : "isPrimary", CatalogFieldErrors.Archived);
        }

        if (current is not null && current == details)
        {
            return Outcome.Ok<Guid>(variantId);
        }

        var now = CatalogClock.Now(clock);
        if (details.IsPrimary)
        {
            foreach (var other in links.Where(l => l.SupplierId != supplierId && l.IsPrimary))
            {
                other.ClearPrimary(actor.UserId, now, requestId);
            }

            // L'index unique partiel est vérifié ligne à ligne : l'ancien principal est libéré AVANT la promotion.
            var cleared = await unitOfWork.SaveAsync(cancellationToken);
            if (!cleared.Saved)
            {
                return Commit(cleared, variantId);
            }
        }

        var fields = new List<AuditChange> { CatalogAudit.Field("supplierId", null, supplierId.ToString()) };
        if (link is null)
        {
            link = VariantSupplier.Create(variantId, supplierId, details, actor.UserId, now, requestId);
            store.Add(link);
        }
        else
        {
            link.Apply(details, actor.UserId, now, requestId);
        }

        if (current?.SupplierReference != details.SupplierReference)
        {
            fields.Add(CatalogAudit.Field("supplierReference"));
        }

        if (current?.LeadTimeDays != details.LeadTimeDays)
        {
            fields.Add(CatalogAudit.Field("leadTimeDays"));
        }

        if ((current?.IsPrimary ?? false) != details.IsPrimary)
        {
            fields.Add(CatalogAudit.Field("isPrimary", (current?.IsPrimary ?? false) ? "true" : "false", details.IsPrimary ? "true" : "false"));
        }

        if (current?.SupplierPriceHt != details.SupplierPriceHt || current?.SupplierCurrency != details.SupplierCurrency)
        {
            // Nom du champ seulement dans l'audit ; la valeur vit dans l'historique des prix (costs.read).
            fields.Add(CatalogAudit.Field("supplierPriceHt"));
            pricing.Add(PriceHistoryEntry.Value(
                Guid.CreateVersion7(now), variantId, null, supplierId, details.SupplierCurrency ?? current?.SupplierCurrency ?? "XXX",
                PriceFields.SupplierPriceHt, current?.SupplierPriceHt, details.SupplierPriceHt, PriceOrigins.Edit, null, actor.UserId, now, requestId));
        }

        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.CatalogModule,
            current is null ? AuditActions.SupplierLinkAdded : AuditActions.SupplierLinkUpdated, AuditActions.VariantEntity, variantId, fields);
        return await CommitAsync(transaction, variantId, cancellationToken);
    }

    /// <summary>Retrait (marqué, conservé) ; retirer le principal d'une variante <c>order_only</c> la rend « incomplète » (QR-01).</summary>
    public async Task<Outcome<Guid>> RemoveSupplierLinkAsync(Guid variantId, Guid supplierId, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await store.LockVariantAsync(variantId, RowLock.NoKeyUpdate, cancellationToken))
        {
            return Outcome.Missing<Guid>();
        }

        var link = await store.FindLinkAsync(variantId, supplierId, cancellationToken);
        if (link is null)
        {
            return Outcome.Missing<Guid>("link_not_found");
        }

        if (link.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(variantId, replayed: true);
        }

        if (link.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (link.IsRemoved)
        {
            return Outcome.Conflict<Guid>("already_removed");
        }

        var now = CatalogClock.Now(clock);
        var wasPrimary = link.IsPrimary;
        link.Remove(userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.SupplierLinkRemoved, AuditActions.VariantEntity, variantId,
            [CatalogAudit.Field("supplierId", supplierId.ToString(), null), .. wasPrimary ? [CatalogAudit.Field("isPrimary", "true", "false")] : Array.Empty<AuditChange>()]);
        return await CommitAsync(transaction, variantId, cancellationToken);
    }

    // ---------- outils ----------

    /// <summary>Référentiel verres et unicité des SKU / codes-barres (entre eux et contre la base).</summary>
    private async Task CheckReferencesAsync(
        ProductModelDetails? model, List<ProductVariantDetails> variants, Guid? excludeVariantId, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        if (model?.LensRangeId is { } rangeId)
        {
            var range = await store.FindLensReferenceAsync(rangeId, cancellationToken);
            if (range is not { Kind: LensReferenceKind.Range })
            {
                errors["lensRangeId"] = [CatalogFieldErrors.Unknown];
            }
            else if (range.IsArchived)
            {
                errors["lensRangeId"] = [CatalogFieldErrors.Archived];
            }
        }

        string Field(int index, string name) =>
            excludeVariantId is null && model is not null ? $"variants[{index}].{name}" : name;

        var skuKeys = variants.Select(v => CatalogKeys.Normalize(v.Sku)).ToList();
        var takenSkus = await store.TakenSkusAsync(skuKeys, excludeVariantId, cancellationToken);
        var barcodes = variants.Where(v => v.Barcode is not null).Select(v => v.Barcode!).ToList();
        var takenBarcodes = barcodes.Count == 0 ? new HashSet<string>() : await store.TakenBarcodesAsync(barcodes, excludeVariantId, cancellationToken);
        for (var i = 0; i < variants.Count; i++)
        {
            var variant = variants[i];
            if (takenSkus.Contains(skuKeys[i]) || skuKeys.IndexOf(skuKeys[i]) != i)
            {
                errors[Field(i, "sku")] = [CatalogFieldErrors.Duplicate];
            }

            if (variant.Barcode is { } barcode && (takenBarcodes.Contains(barcode) || variants.Select(v => v.Barcode).ToList().IndexOf(barcode) != i))
            {
                errors[Field(i, "barcode")] = [CatalogFieldErrors.Duplicate];
            }

            if (variant.LensIndexId is { } indexId)
            {
                var index = await store.FindLensReferenceAsync(indexId, cancellationToken);
                if (index is not { Kind: LensReferenceKind.Index })
                {
                    errors[Field(i, "lensIndexId")] = [CatalogFieldErrors.Unknown];
                }
                else if (index.IsArchived)
                {
                    errors[Field(i, "lensIndexId")] = [CatalogFieldErrors.Archived];
                }
            }

            foreach (var treatmentId in variant.TreatmentIds)
            {
                var treatment = await store.FindLensReferenceAsync(treatmentId, cancellationToken);
                if (treatment is not { Kind: LensReferenceKind.Treatment } || treatment.IsArchived)
                {
                    errors[Field(i, "treatmentIds")] = [treatment is null ? CatalogFieldErrors.Unknown : CatalogFieldErrors.Archived];
                }
            }
        }
    }

    /// <summary>Raison d'un refus lié au stock (à appeler APRÈS le verrou de la variante), ou <c>null</c>.</summary>
    private async Task<string?> StockRefusalAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var (onHand, reserved) = await store.StockTotalsAsync(variantId, cancellationToken);
        if (reserved > 0)
        {
            return CatalogRefusals.StockReserved;
        }

        if (onHand > 0)
        {
            return CatalogRefusals.StockOnHand;
        }

        return await store.InOpenInventoryAsync(variantId, cancellationToken) ? CatalogRefusals.InventoryOpen : null;
    }

    private void RecordVariantCreated(ProductVariant variant, Guid userId, DateTimeOffset now) =>
        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantCreated, AuditActions.VariantEntity, variant.Id,
            [CatalogAudit.Field("stockPolicy", null, variant.StockPolicyCode), .. CatalogAudit.Fields(VariantFields(null, variant.Snapshot()))]);

    private static (string? Name, decimal? Value) LensFields(LensReferenceKind kind, string? name, string? indexValue, Dictionary<string, string[]> errors)
    {
        if (kind == LensReferenceKind.Index)
        {
            var value = CatalogInputValidator.LensIndex(indexValue, errors);
            var label = CatalogText.Clean(name, "name", CatalogLimits.LensNameMaxLength, required: false, errors);
            return (label ?? (value is { } v ? LensReferenceKinds.IndexKey(v) : null), value);
        }

        return (CatalogText.Clean(name, "name", CatalogLimits.LensNameMaxLength, required: true, errors), null);
    }

    private static IEnumerable<string> ModelFields(ProductModelDetails? before, ProductModelDetails after)
    {
        if (before?.Brand != after.Brand)
        {
            yield return "brand";
        }

        if (before?.Name != after.Name)
        {
            yield return "name";
        }

        if (before?.Description != after.Description)
        {
            yield return "description";
        }

        if (before?.LensRangeId != after.LensRangeId)
        {
            yield return "lensRangeId";
        }

        if (before is null ? after.Attributes.Count > 0 : AttributeJson.Serialize(before.Attributes) != AttributeJson.Serialize(after.Attributes))
        {
            yield return "attributes";
        }
    }

    private static IEnumerable<string> VariantFields(ProductVariantDetails? before, ProductVariantDetails after)
    {
        if (before?.Sku != after.Sku)
        {
            yield return "sku";
        }

        if (before?.ManufacturerReference != after.ManufacturerReference)
        {
            yield return "manufacturerReference";
        }

        if (before?.Barcode != after.Barcode)
        {
            yield return "barcode";
        }

        if (before?.Label != after.Label)
        {
            yield return "label";
        }

        if (before is null ? after.Attributes.Count > 0 : AttributeJson.Serialize(before.Attributes) != AttributeJson.Serialize(after.Attributes))
        {
            yield return "attributes";
        }

        if (before?.LensIndexId != after.LensIndexId)
        {
            yield return "lensIndexId";
        }

        if (before is null ? after.TreatmentIds.Count > 0 : !before.TreatmentIds.Order().SequenceEqual(after.TreatmentIds.Order()))
        {
            yield return "treatmentIds";
        }
    }

    private static async Task<Outcome<Guid>> CommitAsync(ITransactionScope transaction, Guid id, ICatalogUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return Commit(commit, id);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Ok<Guid>(id);
    }

    private Task<Outcome<Guid>> CommitAsync(ITransactionScope transaction, Guid id, CancellationToken cancellationToken) =>
        CommitAsync(transaction, id, unitOfWork, cancellationToken);

    /// <summary>Issue d'un enregistrement sans transaction explicite (un seul <c>SaveChanges</c>).</summary>
    internal static Outcome<Guid> Commit(CommitResult commit, Guid id, string duplicateField = "sku") =>
        commit.Saved ? Outcome.Ok<Guid>(id) : UniqueRefusal<Guid>(commit, duplicateField);

    /// <summary>Unicité violée entre le contrôle et l'écriture (écriture concurrente) → erreur de champ ; sinon conflit.</summary>
    internal static Outcome<T> UniqueRefusal<T>(CommitResult commit, string duplicateField = "sku") => commit switch
    {
        { Status: CommitStatus.UniqueViolation, Constraint: "ux_product_variants_sku" } => Outcome.Invalid<T>("sku", CatalogFieldErrors.Duplicate),
        { Status: CommitStatus.UniqueViolation, Constraint: "ux_product_variants_barcode" } => Outcome.Invalid<T>("barcode", CatalogFieldErrors.Duplicate),
        { Status: CommitStatus.UniqueViolation, Constraint: "ux_suppliers_code" } => Outcome.Invalid<T>("code", CatalogFieldErrors.Duplicate),
        { Status: CommitStatus.UniqueViolation, Constraint: "ux_lens_references_kind_parent_name" } => Outcome.Invalid<T>(duplicateField, CatalogFieldErrors.Duplicate),
        { Status: CommitStatus.CheckViolation } => Outcome.Refuse<T>("stock_insufficient"),
        _ => Outcome.Conflict<T>(),
    };
}
