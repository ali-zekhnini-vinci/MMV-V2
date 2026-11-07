namespace MMV.Domain.Catalog;

/// <summary>
/// Offre d'un FOURNISSEUR pour une gamme de verres (COR-05-05) : ce que ce fournisseur fabrique ou livre réellement,
/// séparé du référentiel (fabricant, gamme, indice, traitement), des variantes commercialisables et de la correction
/// du client. Niveau simple : type, matériau / indice, traitements, politique stocké ou commandé, plages SPH / CYL /
/// ADD, diamètres, délai, prix connu. Niveau avancé : règles structurées (<see cref="LensRule"/>), provenance et
/// validité de la fiche technique, fiche déclarée complète ou non. Chaque modification incrémente la révision et laisse
/// un instantané (<see cref="LensOfferRevision"/>). Jamais de suppression : archivage.
/// </summary>
public sealed class LensOffer : ArchivableEntity
{
    public const int MaterialMaxLength = 60;
    public const int SourceMaxLength = 200;
    public const int NotesMaxLength = 1000;
    public const int MaxRules = 60;
    public const int MaxDiameters = 20;

    private readonly List<LensOfferTreatment> _treatments = [];

    private LensOffer()
    {
        LensTypeCode = LensTypes.SingleVision;
        ProductionCode = LensProduction.OnOrder;
        DiametersJson = "[]";
        RulesJson = "[]";
    }

    public Guid Id { get; private set; }

    public Guid RangeId { get; private set; }

    public Guid SupplierId { get; private set; }

    public string LensTypeCode { get; private set; }

    public string? Material { get; private set; }

    public Guid? IndexId { get; private set; }

    public string ProductionCode { get; private set; }

    public int? LeadTimeDays { get; private set; }

    /// <summary>Prix fournisseur HT connu (coût : servi seulement avec <c>costs.read</c>).</summary>
    public decimal? PriceHt { get; private set; }

    public string? PriceCurrency { get; private set; }

    public string DiametersJson { get; private set; }

    public decimal? SphereMin { get; private set; }

    public decimal? SphereMax { get; private set; }

    public decimal? CylinderMin { get; private set; }

    public decimal? CylinderMax { get; private set; }

    public decimal? AdditionMin { get; private set; }

    public decimal? AdditionMax { get; private set; }

    public string RulesJson { get; private set; }

    /// <summary>Vrai si la fiche fournisseur enregistrée est déclarée COMPLÈTE (sinon toute évaluation reste « à vérifier »).</summary>
    public bool RulesComplete { get; private set; }

    public string? SourceReference { get; private set; }

    public DateOnly? ValidFrom { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    public string? Notes { get; private set; }

    public int Revision { get; private set; }

    public Guid CreationRequestId { get; private set; }

    public IReadOnlyList<LensOfferTreatment> Treatments => _treatments;

    public static LensOffer Create(Guid id, Guid rangeId, Guid supplierId, LensOfferDetails details, Guid requestId, Guid userId, DateTimeOffset now)
    {
        var offer = new LensOffer { Id = id, RangeId = rangeId, SupplierId = supplierId, CreationRequestId = requestId };
        offer.Apply(details);
        offer.Revision = 1;
        offer.Created(userId, now, requestId);
        return offer;
    }

    public void Update(LensOfferDetails details, Guid userId, DateTimeOffset now, Guid requestId)
    {
        Apply(details);
        Revision++;
        Touch(userId, now, requestId);
    }

    public void Bump(Guid userId, DateTimeOffset now, Guid requestId)
    {
        Revision++;
        Touch(userId, now, requestId);
    }

    public LensOfferDetails Snapshot() => new(
        LensTypeCode, Material, IndexId, ProductionCode, LeadTimeDays, PriceHt, PriceCurrency, LensRuleJson.DeserializeDiameters(DiametersJson),
        Range(SphereMin, SphereMax), Range(CylinderMin, CylinderMax), Range(AdditionMin, AdditionMax), [.. _treatments.Select(t => t.TreatmentId).Order()],
        LensRuleJson.Deserialize(RulesJson), RulesComplete, SourceReference, ValidFrom, ValidUntil, Notes);

    public LensOfferCapabilities Capabilities() => new(
        LensTypeCode, Material, IndexId, [.. _treatments.Select(t => t.TreatmentId)], Range(SphereMin, SphereMax), Range(CylinderMin, CylinderMax),
        Range(AdditionMin, AdditionMax), LensRuleJson.DeserializeDiameters(DiametersJson), LensRuleJson.Deserialize(RulesJson), RulesComplete, ValidUntil);

    private static DiopterRange? Range(decimal? min, decimal? max) => min is { } a && max is { } b ? new DiopterRange(a, b) : null;

    private void Apply(LensOfferDetails details)
    {
        LensTypeCode = details.LensType;
        Material = details.Material;
        IndexId = details.IndexId;
        ProductionCode = details.Production;
        LeadTimeDays = details.LeadTimeDays;
        PriceHt = details.PriceHt;
        PriceCurrency = details.PriceCurrency;
        DiametersJson = LensRuleJson.SerializeDiameters(details.Diameters);
        (SphereMin, SphereMax) = (details.Sphere?.Min, details.Sphere?.Max);
        (CylinderMin, CylinderMax) = (details.Cylinder?.Min, details.Cylinder?.Max);
        (AdditionMin, AdditionMax) = (details.Addition?.Min, details.Addition?.Max);
        RulesJson = LensRuleJson.Serialize(details.Rules);
        RulesComplete = details.RulesComplete;
        SourceReference = details.SourceReference;
        ValidFrom = details.ValidFrom;
        ValidUntil = details.ValidUntil;
        Notes = details.Notes;
        _treatments.RemoveAll(t => !details.TreatmentIds.Contains(t.TreatmentId));
        foreach (var treatment in details.TreatmentIds.Where(t => _treatments.All(x => x.TreatmentId != t)))
        {
            _treatments.Add(new LensOfferTreatment(Id, treatment));
        }
    }
}

/// <summary>Contenu validé d'une offre (niveau simple + avancé).</summary>
public sealed record LensOfferDetails(
    string LensType,
    string? Material,
    Guid? IndexId,
    string Production,
    int? LeadTimeDays,
    decimal? PriceHt,
    string? PriceCurrency,
    IReadOnlyList<int> Diameters,
    DiopterRange? Sphere,
    DiopterRange? Cylinder,
    DiopterRange? Addition,
    IReadOnlyList<Guid> TreatmentIds,
    IReadOnlyList<LensRule> Rules,
    bool RulesComplete,
    string? SourceReference,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    string? Notes);

public sealed class LensOfferTreatment
{
    private LensOfferTreatment()
    {
    }

    internal LensOfferTreatment(Guid offerId, Guid treatmentId)
    {
        OfferId = offerId;
        TreatmentId = treatmentId;
    }

    public Guid OfferId { get; private set; }

    public Guid TreatmentId { get; private set; }
}

/// <summary>Instantané d'une révision (ajout seul) : historique des règles fournisseur.</summary>
public sealed class LensOfferRevision
{
    public const int ReasonMaxLength = 500;

    private LensOfferRevision()
    {
        Snapshot = "{}";
    }

    public Guid Id { get; private set; }

    public Guid OfferId { get; private set; }

    public int Revision { get; private set; }

    public string Snapshot { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public static LensOfferRevision Create(Guid id, Guid offerId, int revision, string snapshot, string? reason, Guid userId, DateTimeOffset now) =>
        new() { Id = id, OfferId = offerId, Revision = revision, Snapshot = snapshot, Reason = reason, ChangedAtUtc = now, ChangedByUserId = userId };
}
