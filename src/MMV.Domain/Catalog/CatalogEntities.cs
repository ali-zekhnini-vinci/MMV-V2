using System.Text.Json;

namespace MMV.Domain.Catalog;

/// <summary>Limites communes du catalogue (contraintes SQL de la migration 0007).</summary>
public static class CatalogLimits
{
    public const int BrandMaxLength = 100;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int SkuMaxLength = 64;
    public const int ReferenceMaxLength = 64;
    public const int BarcodeMaxLength = 14;
    public const int LabelMaxLength = 200;
    public const int ReasonMaxLength = 500;
    public const int LensNameMaxLength = 120;
    public const int MaxVariantsPerModel = 200;
    public const int MaxTreatmentsPerVariant = 20;
}

/// <summary>Attributs d'un modèle ou d'une variante : objet JSON de chaînes, clés validées selon la famille.</summary>
public static class AttributeJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyDictionary<string, string> values) =>
        JsonSerializer.Serialize(new SortedDictionary<string, string>(values.ToDictionary(), StringComparer.Ordinal), Options);

    public static IReadOnlyDictionary<string, string> Parse(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options) ?? [];
}

/// <summary>Marqueurs d'archivage communs (aucune suppression physique, D-05-07).</summary>
public abstract class ArchivableEntity
{
    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public Guid CreatedByUserId { get; protected set; }

    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    public Guid UpdatedByUserId { get; protected set; }

    public DateTimeOffset? ArchivedAtUtc { get; protected set; }

    public Guid? ArchivedByUserId { get; protected set; }

    public string? ArchiveReason { get; protected set; }

    /// <summary>Dernière requête appliquée (idempotence des réémissions).</summary>
    public Guid? LastRequestId { get; protected set; }

    /// <summary>Jeton de concurrence optimiste : colonne système <c>xmin</c>.</summary>
    public uint Version { get; protected set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    protected void Created(Guid userId, DateTimeOffset now, Guid? requestId)
    {
        CreatedAtUtc = now;
        CreatedByUserId = userId;
        Touch(userId, now, requestId);
    }

    public void Touch(Guid userId, DateTimeOffset now, Guid? requestId)
    {
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }

    public void Archive(string? reason, Guid userId, DateTimeOffset now, Guid? requestId)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Déjà archivé.");
        }

        ArchivedAtUtc = now;
        ArchivedByUserId = userId;
        ArchiveReason = reason;
        Touch(userId, now, requestId);
    }

    public void Reactivate(Guid userId, DateTimeOffset now, Guid? requestId)
    {
        if (!IsArchived)
        {
            throw new InvalidOperationException("Déjà actif.");
        }

        ArchivedAtUtc = null;
        ArchivedByUserId = null;
        ArchiveReason = null;
        Touch(userId, now, requestId);
    }
}

/// <summary>Modèle produit (D-05-04 option B) : famille, marque, nom ; ne porte NI prix NI stock.</summary>
public sealed class ProductModel : ArchivableEntity
{
    private readonly List<ProductVariant> _variants = [];

    private ProductModel()
    {
        FamilyCode = string.Empty;
        Name = string.Empty;
        AttributesJson = "{}";
    }

    public Guid Id { get; private set; }

    public string FamilyCode { get; private set; }

    public string? Brand { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Gamme de verres (famille « verres correcteurs » seulement, D-05-21).</summary>
    public Guid? LensRangeId { get; private set; }

    public string AttributesJson { get; private set; }

    public Guid? CreationRequestId { get; private set; }

    public IReadOnlyList<ProductVariant> Variants => _variants;

    public IReadOnlyDictionary<string, string> Attributes => AttributeJson.Parse(AttributesJson);

    public static ProductModel Create(Guid id, ProductModelDetails details, Guid requestId, Guid userId, DateTimeOffset now)
    {
        var model = new ProductModel { Id = id, FamilyCode = details.FamilyCode, CreationRequestId = requestId };
        model.Apply(details);
        model.Created(userId, now, requestId);
        return model;
    }

    public void Update(ProductModelDetails details, Guid userId, DateTimeOffset now, Guid requestId)
    {
        if (details.FamilyCode != FamilyCode)
        {
            throw new InvalidOperationException("La famille d'un modèle n'est pas modifiable.");
        }

        Apply(details);
        Touch(userId, now, requestId);
    }

    public ProductModelDetails Snapshot() => new(FamilyCode, Brand, Name, Description, LensRangeId, Attributes);

    public void AddVariant(ProductVariant variant) => _variants.Add(variant);

    private void Apply(ProductModelDetails details)
    {
        Brand = details.Brand;
        Name = details.Name;
        Description = details.Description;
        LensRangeId = details.LensRangeId;
        AttributesJson = AttributeJson.Serialize(details.Attributes);
    }
}

/// <summary>Données saisissables d'un modèle, déjà validées.</summary>
public sealed record ProductModelDetails(
    string FamilyCode, string? Brand, string Name, string? Description, Guid? LensRangeId, IReadOnlyDictionary<string, string> Attributes)
{
    public bool SameAs(ProductModelDetails other) =>
        FamilyCode == other.FamilyCode && Brand == other.Brand && Name == other.Name && Description == other.Description
        && LensRangeId == other.LensRangeId && AttributeJson.Serialize(Attributes) == AttributeJson.Serialize(other.Attributes);
}

/// <summary>
/// Variante : unité de vente, de prix (par magasin) et de stock (par emplacement). SKU unique dans l'installation,
/// références fabricant facultatives, code-barres facultatif unique (D-05-14) ; politique de stock propre (Q04).
/// </summary>
public sealed class ProductVariant : ArchivableEntity
{
    private readonly List<VariantLensTreatment> _treatments = [];

    private ProductVariant()
    {
        Sku = string.Empty;
        SkuKey = string.Empty;
        Label = string.Empty;
        AttributesJson = "{}";
        StockPolicyCode = StockPolicies.Stocked;
    }

    public Guid Id { get; private set; }

    public Guid ModelId { get; private set; }

    public string Sku { get; private set; }

    /// <summary>Clé d'unicité du SKU (<see cref="CatalogKeys.Normalize"/>), indexée UNIQUE.</summary>
    public string SkuKey { get; private set; }

    public string? ManufacturerReference { get; private set; }

    public string? Barcode { get; private set; }

    public string Label { get; private set; }

    public string AttributesJson { get; private set; }

    public Guid? LensIndexId { get; private set; }

    public string StockPolicyCode { get; private set; }

    /// <summary>Catégorie fiscale : renseignée seulement après décision ADR-010 (aucune écriture par l'API en Phase 05).</summary>
    public string? TaxCategoryCode { get; private set; }

    public Guid? CreationRequestId { get; private set; }

    public IReadOnlyList<VariantLensTreatment> Treatments => _treatments;

    public StockPolicy StockPolicy => StockPolicies.FromCode(StockPolicyCode);

    public IReadOnlyDictionary<string, string> Attributes => AttributeJson.Parse(AttributesJson);

    public static ProductVariant Create(Guid id, Guid modelId, ProductVariantDetails details, StockPolicy policy, Guid? requestId, Guid userId, DateTimeOffset now)
    {
        var variant = new ProductVariant
        {
            Id = id,
            ModelId = modelId,
            StockPolicyCode = StockPolicies.ToCode(policy),
            CreationRequestId = requestId,
        };
        variant.Apply(details);
        variant.Created(userId, now, requestId);
        return variant;
    }

    public void Update(ProductVariantDetails details, Guid userId, DateTimeOffset now, Guid requestId)
    {
        Apply(details);
        Touch(userId, now, requestId);
    }

    public void ChangePolicy(StockPolicy policy, Guid userId, DateTimeOffset now, Guid requestId)
    {
        StockPolicyCode = StockPolicies.ToCode(policy);
        Touch(userId, now, requestId);
    }

    public ProductVariantDetails Snapshot() =>
        new(Sku, ManufacturerReference, Barcode, Label, Attributes, LensIndexId, [.. _treatments.Select(t => t.TreatmentId).Order()]);

    private void Apply(ProductVariantDetails details)
    {
        Sku = details.Sku;
        SkuKey = CatalogKeys.Normalize(details.Sku);
        ManufacturerReference = details.ManufacturerReference;
        Barcode = details.Barcode;
        Label = details.Label;
        AttributesJson = AttributeJson.Serialize(details.Attributes);
        LensIndexId = details.LensIndexId;
        _treatments.RemoveAll(t => !details.TreatmentIds.Contains(t.TreatmentId));
        foreach (var treatment in details.TreatmentIds.Where(t => _treatments.All(existing => existing.TreatmentId != t)))
        {
            _treatments.Add(new VariantLensTreatment(Id, treatment));
        }
    }
}

/// <summary>Données saisissables d'une variante (hors politique, modifiée par une action dédiée), déjà validées.</summary>
public sealed record ProductVariantDetails(
    string Sku,
    string? ManufacturerReference,
    string? Barcode,
    string Label,
    IReadOnlyDictionary<string, string> Attributes,
    Guid? LensIndexId,
    IReadOnlyList<Guid> TreatmentIds)
{
    public bool SameAs(ProductVariantDetails other) =>
        Sku == other.Sku && ManufacturerReference == other.ManufacturerReference && Barcode == other.Barcode && Label == other.Label
        && AttributeJson.Serialize(Attributes) == AttributeJson.Serialize(other.Attributes) && LensIndexId == other.LensIndexId
        && TreatmentIds.Order().SequenceEqual(other.TreatmentIds.Order());
}

/// <summary>Traitement d'une variante de verre (D-05-21, QR-07 : traitements inclus dans la variante).</summary>
public sealed class VariantLensTreatment
{
    private VariantLensTreatment()
    {
    }

    internal VariantLensTreatment(Guid variantId, Guid treatmentId)
    {
        VariantId = variantId;
        TreatmentId = treatmentId;
    }

    public Guid VariantId { get; private set; }

    public Guid TreatmentId { get; private set; }
}

/// <summary>Type d'élément du référentiel verres (D-05-21) : configurable, aucune valeur pré-remplie.</summary>
public enum LensReferenceKind
{
    Manufacturer,
    Range,
    Index,
    Treatment,
}

/// <summary>
/// Élément du référentiel verres : fabricant, gamme (d'un fabricant), indice (valeur décimale exacte), traitement
/// (d'un fabricant ou générique). Une seule table, discriminée par <see cref="Kind"/> ; archivage, jamais de suppression.
/// </summary>
public sealed class LensReference : ArchivableEntity
{
    private LensReference()
    {
        KindCode = string.Empty;
        Name = string.Empty;
        NameKey = string.Empty;
    }

    public Guid Id { get; private set; }

    public string KindCode { get; private set; }

    public string Name { get; private set; }

    /// <summary>Clé d'unicité par (type, parent, nom) : nom normalisé, ou valeur exacte de l'indice.</summary>
    public string NameKey { get; private set; }

    /// <summary>Fabricant parent (gamme : obligatoire ; traitement : facultatif ; autres : absent).</summary>
    public Guid? ManufacturerId { get; private set; }

    /// <summary>Valeur d'un indice (ex. 1.600), décimale exacte.</summary>
    public decimal? IndexValue { get; private set; }

    public LensReferenceKind Kind => KindCode switch
    {
        LensReferenceKinds.Manufacturer => LensReferenceKind.Manufacturer,
        LensReferenceKinds.Range => LensReferenceKind.Range,
        LensReferenceKinds.Index => LensReferenceKind.Index,
        _ => LensReferenceKind.Treatment,
    };

    public static LensReference Create(Guid id, LensReferenceKind kind, string name, Guid? manufacturerId, decimal? indexValue, Guid requestId, Guid userId, DateTimeOffset now)
    {
        var reference = new LensReference { Id = id, KindCode = LensReferenceKinds.ToCode(kind) };
        reference.Apply(name, manufacturerId, indexValue);
        reference.Created(userId, now, requestId);
        return reference;
    }

    public void Rename(string name, decimal? indexValue, Guid userId, DateTimeOffset now, Guid requestId)
    {
        Apply(name, ManufacturerId, indexValue);
        Touch(userId, now, requestId);
    }

    private void Apply(string name, Guid? manufacturerId, decimal? indexValue)
    {
        Name = name;
        ManufacturerId = manufacturerId;
        IndexValue = indexValue;
        NameKey = indexValue is { } value ? LensReferenceKinds.IndexKey(value) : CatalogKeys.Normalize(name);
    }
}

public static class LensReferenceKinds
{
    public const string Manufacturer = "manufacturer";
    public const string Range = "range";
    public const string Index = "index";
    public const string Treatment = "treatment";

    public static string ToCode(LensReferenceKind kind) => kind switch
    {
        LensReferenceKind.Manufacturer => Manufacturer,
        LensReferenceKind.Range => Range,
        LensReferenceKind.Index => Index,
        _ => Treatment,
    };

    public static LensReferenceKind? FromSegment(string? segment) => segment switch
    {
        "manufacturers" => LensReferenceKind.Manufacturer,
        "ranges" => LensReferenceKind.Range,
        "indices" => LensReferenceKind.Index,
        "treatments" => LensReferenceKind.Treatment,
        _ => null,
    };

    /// <summary>Indice : 3 décimales fixes (1.5 et 1.500 désignent le même indice).</summary>
    public static string IndexKey(decimal value) => value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
}
