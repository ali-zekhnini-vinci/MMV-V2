using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMV.Domain.Catalog;

/// <summary>Type de verre d'une offre (liste fermée).</summary>
public static class LensTypes
{
    public const string SingleVision = "single_vision";
    public const string Progressive = "progressive";
    public const string Degressive = "degressive";
    public const string Bifocal = "bifocal";
    public const string Other = "other";

    public static IReadOnlyList<string> All { get; } = [SingleVision, Progressive, Degressive, Bifocal, Other];

    /// <summary>Types pour lesquels une addition est attendue.</summary>
    public static bool UsesAddition(string type) => type is Progressive or Degressive or Bifocal;
}

/// <summary>Politique d'approvisionnement de l'offre : verre tenu en stock chez l'opticien ou commandé à la demande.</summary>
public static class LensProduction
{
    public const string Stocked = "stocked";
    public const string OnOrder = "on_order";

    public static IReadOnlyList<string> All { get; } = [Stocked, OnOrder];
}

/// <summary>Fabrication standard ou spéciale (hors plage standard, souvent avec supplément et délai).</summary>
public static class LensManufacturing
{
    public const string Standard = "standard";
    public const string Special = "special";

    public static IReadOnlyList<string> All { get; } = [Standard, Special];
}

/// <summary>
/// Types de règle avancée, STRUCTURÉS (jamais d'expression libre ni de code venu d'une fiche fournisseur) :
/// plages (SPH, CYL, ADD), diamètres, limite de combinaison |SPH| + |CYL|, restriction explicite.
/// </summary>
public static class LensRuleKinds
{
    public const string SphereRange = "sphere_range";
    public const string CylinderRange = "cylinder_range";
    public const string AdditionRange = "addition_range";
    public const string Diameters = "diameters";
    public const string CombinationLimit = "combination_limit";
    public const string Restriction = "restriction";

    public static IReadOnlyList<string> All { get; } = [SphereRange, CylinderRange, AdditionRange, Diameters, CombinationLimit, Restriction];

    public static bool IsRange(string kind) => kind is SphereRange or CylinderRange or AdditionRange;
}

public static class LensRuleEffects
{
    /// <summary>Autorise (plage ou diamètres proposés dans les conditions de la règle).</summary>
    public const string Allow = "allow";

    /// <summary>Interdit explicitement (plage exclue, restriction de combinaison).</summary>
    public const string Deny = "deny";

    public static IReadOnlyList<string> All { get; } = [Allow, Deny];
}

/// <summary>Plage dioptrique fermée [Min, Max] (pas de 0,25 D non imposé : la fiche fournisseur fait foi).</summary>
public sealed record DiopterRange(decimal Min, decimal Max)
{
    public bool Contains(decimal value) => value >= Min && value <= Max;
}

/// <summary>
/// Conditions d'application d'une règle (toutes facultatives, combinées en ET) : traitements TOUS choisis, indice,
/// matériau, et plages où se situe la correction. Une règle sans condition s'applique toujours.
/// </summary>
public sealed record LensRuleConditions(
    IReadOnlyList<Guid>? TreatmentIds = null,
    IReadOnlyList<Guid>? IndexIds = null,
    string? Material = null,
    DiopterRange? Sphere = null,
    DiopterRange? Cylinder = null,
    DiopterRange? Addition = null)
{
    [JsonIgnore]
    public int Specificity =>
        (TreatmentIds?.Count ?? 0) + (IndexIds?.Count > 0 ? 1 : 0) + (Material is null ? 0 : 1) + (Sphere is null ? 0 : 1) + (Cylinder is null ? 0 : 1) + (Addition is null ? 0 : 1);
}

/// <summary>Règle avancée d'une offre (voir <see cref="LensRuleKinds"/>).</summary>
public sealed record LensRule(
    string Kind,
    string Effect,
    string Manufacturing,
    LensRuleConditions? Conditions = null,
    DiopterRange? Range = null,
    IReadOnlyList<int>? Diameters = null,
    decimal? MaxSphereCylinderSum = null,
    decimal? SurchargeHt = null,
    int? ExtraLeadDays = null,
    string? Note = null);

/// <summary>Correction à évaluer (issue plus tard du dossier optique, Phase 06) ; valeurs absentes = inconnues, jamais 0.</summary>
public sealed record LensPrescription(decimal? Sphere, decimal? Cylinder, decimal? Addition, int? Diameter, IReadOnlyList<Guid> TreatmentIds, Guid? IndexId = null);

public enum LensCompatibilityStatus
{
    /// <summary>Compatible avec les règles fournisseur COMPLÈTES enregistrées (pas une garantie du fabricant).</summary>
    Compatible,

    /// <summary>Incompatible selon une règle explicite enregistrée.</summary>
    Incompatible,

    /// <summary>Compatibilité à vérifier, faute de données suffisantes.</summary>
    ToVerify,
}

/// <summary>Motif borné (code + paramètres textuels non sensibles) ; le poste le traduit.</summary>
public sealed record LensCompatibilityReason(string Code, string Severity, IReadOnlyDictionary<string, string>? Values = null);

public sealed record LensCompatibility(
    LensCompatibilityStatus Status, string Manufacturing, IReadOnlyList<LensCompatibilityReason> Reasons, decimal SurchargeHt, int ExtraLeadDays);

/// <summary>Capacités d'une offre à évaluer (vue domaine, indépendante de la persistance).</summary>
public sealed record LensOfferCapabilities(
    string LensType,
    string? Material,
    Guid? IndexId,
    IReadOnlyCollection<Guid> TreatmentIds,
    DiopterRange? Sphere,
    DiopterRange? Cylinder,
    DiopterRange? Addition,
    IReadOnlyList<int> Diameters,
    IReadOnlyList<LensRule> Rules,
    bool RulesComplete,
    DateOnly? ValidUntil);

/// <summary>
/// Évaluation de fabricabilité selon les SEULES règles fournisseur enregistrées (COR-05-05). Trois issues distinctes :
/// compatible (toutes les dimensions couvertes par une fiche déclarée complète), incompatible (règle explicite),
/// à vérifier (donnée manquante, fiche incomplète ou expirée). Ce n'est NI une validation clinique NI une garantie du
/// fabricant. Règle de spécificité : pour une dimension, si des règles « autoriser » à conditions s'appliquent, seules
/// les plus spécifiques comptent (ex. traitement anti-lumière bleue : SPH −10,00 à +5,00) ; sinon plage de base ∪
/// règles sans condition. Une règle « interdire » applicable l'emporte toujours.
/// </summary>
public static class LensCompatibilityEvaluator
{
    public static LensCompatibility Evaluate(LensOfferCapabilities offer, LensPrescription rx, DateOnly today)
    {
        var reasons = new List<LensCompatibilityReason>();
        var incompatible = false;
        var verify = false;
        var manufacturing = LensManufacturing.Standard;
        decimal surcharge = 0;
        var extraDays = 0;

        void Fail(string code, Dictionary<string, string>? values = null)
        {
            incompatible = true;
            reasons.Add(new(code, "error", values));
        }

        void Verify(string code, Dictionary<string, string>? values = null)
        {
            verify = true;
            reasons.Add(new(code, "warning", values));
        }

        static string D(decimal value) => value.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);

        foreach (var treatment in rx.TreatmentIds.Where(t => !offer.TreatmentIds.Contains(t)))
        {
            Fail("treatment_not_offered", new() { ["treatmentId"] = treatment.ToString() });
        }

        if (rx.IndexId is { } index && offer.IndexId is { } offered && index != offered)
        {
            Fail("index_mismatch");
        }

        var applicable = offer.Rules.Where(r => Applies(r.Conditions, offer, rx)).ToList();
        foreach (var rule in applicable.Where(r => r.Effect == LensRuleEffects.Deny && r.Kind == LensRuleKinds.Restriction))
        {
            Fail("restriction", rule.Note is null ? null : new() { ["note"] = rule.Note });
        }

        void Dimension(string kind, string name, decimal? value, DiopterRange? baseRange, bool required)
        {
            if (value is null)
            {
                if (required)
                {
                    Verify($"{name}_missing");
                }

                return;
            }

            foreach (var deny in applicable.Where(r => r.Kind == kind && r.Effect == LensRuleEffects.Deny && r.Range is { } range && range.Contains(value.Value)))
            {
                Fail($"{name}_denied", new() { ["value"] = D(value.Value), ["min"] = D(deny.Range!.Min), ["max"] = D(deny.Range.Max) });
                return;
            }

            var allows = applicable.Where(r => r.Kind == kind && r.Effect == LensRuleEffects.Allow && r.Range is not null).ToList();
            var specific = allows.Count == 0 ? 0 : allows.Max(r => r.Conditions?.Specificity ?? 0);
            var ranges = specific > 0
                ? allows.Where(r => (r.Conditions?.Specificity ?? 0) == specific).Select(r => (r.Range!, (LensRule?)r)).ToList()
                : [.. (baseRange is null ? [] : new[] { (baseRange, (LensRule?)null) }), .. allows.Select(r => (r.Range!, (LensRule?)r))];
            if (ranges.Count == 0)
            {
                // Correction nulle sans plage déclarée : rien à vérifier (CYL 0, ADD 0).
                if (value.Value != 0)
                {
                    Verify($"{name}_no_data", new() { ["value"] = D(value.Value) });
                }

                return;
            }

            var hits = ranges.Where(r => r.Item1.Contains(value.Value)).ToList();
            if (hits.Count == 0)
            {
                Fail($"{name}_out_of_range", new()
                {
                    ["value"] = D(value.Value), ["min"] = D(ranges.Min(r => r.Item1.Min)), ["max"] = D(ranges.Max(r => r.Item1.Max)),
                });
                return;
            }

            // Fabrication standard si une plage standard couvre la valeur ; sinon fabrication spéciale (supplément, délai).
            var standard = hits.FirstOrDefault(h => h.Item2 is null || h.Item2.Manufacturing == LensManufacturing.Standard);
            var chosen = standard.Item1 is not null ? standard : hits[0];
            if (chosen.Item2 is { } rule)
            {
                if (rule.Manufacturing == LensManufacturing.Special)
                {
                    manufacturing = LensManufacturing.Special;
                    reasons.Add(new($"{name}_special_manufacturing", "info", new Dictionary<string, string> { ["value"] = D(value.Value) }));
                }

                surcharge += rule.SurchargeHt ?? 0;
                extraDays = Math.Max(extraDays, rule.ExtraLeadDays ?? 0);
            }
        }

        Dimension(LensRuleKinds.SphereRange, "sphere", rx.Sphere, offer.Sphere, required: true);
        Dimension(LensRuleKinds.CylinderRange, "cylinder", rx.Cylinder, offer.Cylinder, required: false);
        if (LensTypes.UsesAddition(offer.LensType))
        {
            Dimension(LensRuleKinds.AdditionRange, "addition", rx.Addition, offer.Addition, required: true);
        }
        else if (rx.Addition is { } addition && addition != 0)
        {
            reasons.Add(new("addition_not_applicable", "info"));
        }

        foreach (var limit in applicable.Where(r => r.Kind == LensRuleKinds.CombinationLimit && r.MaxSphereCylinderSum is not null))
        {
            if (rx.Sphere is { } s && Math.Abs(s) + Math.Abs(rx.Cylinder ?? 0) > limit.MaxSphereCylinderSum!.Value)
            {
                Fail("combination_exceeded", new() { ["max"] = D(limit.MaxSphereCylinderSum.Value) });
            }
        }

        if (rx.Diameter is { } diameter)
        {
            var diameterRules = applicable.Where(r => r.Kind == LensRuleKinds.Diameters && r.Effect == LensRuleEffects.Allow && r.Diameters is { Count: > 0 }).ToList();
            var allowed = diameterRules.Count > 0 ? diameterRules.SelectMany(r => r.Diameters!).ToHashSet() : offer.Diameters.ToHashSet();
            if (applicable.Any(r => r.Kind == LensRuleKinds.Diameters && r.Effect == LensRuleEffects.Deny && r.Diameters?.Contains(diameter) == true))
            {
                Fail("diameter_denied", new() { ["value"] = diameter.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            else if (allowed.Count == 0)
            {
                Verify("diameter_no_data");
            }
            else if (!allowed.Contains(diameter))
            {
                Fail("diameter_not_offered", new() { ["value"] = diameter.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
        }

        if (offer.ValidUntil is { } until && until < today)
        {
            Verify("data_expired", new() { ["validUntil"] = until.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });
        }

        if (!offer.RulesComplete)
        {
            Verify("rules_incomplete");
        }

        var status = incompatible ? LensCompatibilityStatus.Incompatible : verify ? LensCompatibilityStatus.ToVerify : LensCompatibilityStatus.Compatible;
        return new LensCompatibility(status, manufacturing, reasons, surcharge, extraDays);
    }

    private static bool Applies(LensRuleConditions? conditions, LensOfferCapabilities offer, LensPrescription rx)
    {
        if (conditions is null)
        {
            return true;
        }

        if (conditions.TreatmentIds is { Count: > 0 } treatments && !treatments.All(rx.TreatmentIds.Contains))
        {
            return false;
        }

        var index = rx.IndexId ?? offer.IndexId;
        if (conditions.IndexIds is { Count: > 0 } indices && (index is null || !indices.Contains(index.Value)))
        {
            return false;
        }

        if (conditions.Material is { } material && !string.Equals(material, offer.Material, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (conditions.Sphere is null || (rx.Sphere is { } s && conditions.Sphere.Contains(s)))
            && (conditions.Cylinder is null || (rx.Cylinder is { } c && conditions.Cylinder.Contains(c)))
            && (conditions.Addition is null || (rx.Addition is { } a && conditions.Addition.Contains(a)));
    }
}

/// <summary>Sérialisation stricte des règles (membres inconnus refusés, nombres stricts) — stockage <c>jsonb</c>.</summary>
public static class LensRuleJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(IReadOnlyList<LensRule> rules) => JsonSerializer.Serialize(rules, Options);

    public static IReadOnlyList<LensRule> Deserialize(string json) => JsonSerializer.Deserialize<List<LensRule>>(json, Options) ?? [];

    public static string SerializeDiameters(IReadOnlyList<int> diameters) => JsonSerializer.Serialize(diameters);

    public static IReadOnlyList<int> DeserializeDiameters(string json) => JsonSerializer.Deserialize<List<int>>(json) ?? [];
}
