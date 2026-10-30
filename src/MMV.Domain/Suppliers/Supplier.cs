using MMV.Domain.Catalog;

namespace MMV.Domain.Suppliers;

/// <summary>
/// Fournisseur (D-05-06) : code interne unique, pays, UN contact minimal (données personnelles : nom, e-mail,
/// téléphone), adresse, notes, identifiants légaux facultatifs (contrôle de format seulement, aucune règle juridique
/// déclarée). Actif ou archivé, jamais supprimé.
/// </summary>
public sealed class Supplier : ArchivableEntity
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 200;
    public const int ContactMaxLength = 120;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 32;
    public const int AddressLineMaxLength = 200;
    public const int PostalCodeMaxLength = 16;
    public const int CityMaxLength = 100;
    public const int NotesMaxLength = 2000;

    private Supplier()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        Name = string.Empty;
        CountryCode = string.Empty;
        LegalIdentifiersJson = "{}";
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string CodeKey { get; private set; }

    public string Name { get; private set; }

    public string CountryCode { get; private set; }

    public string? ContactName { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? AddressLine { get; private set; }

    public string? PostalCode { get; private set; }

    public string? City { get; private set; }

    public string LegalIdentifiersJson { get; private set; }

    public string? Notes { get; private set; }

    public Guid? CreationRequestId { get; private set; }

    public IReadOnlyDictionary<string, string> LegalIdentifiers => AttributeJson.Parse(LegalIdentifiersJson);

    public static Supplier Create(Guid id, SupplierDetails details, Guid requestId, Guid userId, DateTimeOffset now)
    {
        var supplier = new Supplier { Id = id, CreationRequestId = requestId };
        supplier.Apply(details);
        supplier.Created(userId, now, requestId);
        return supplier;
    }

    public void Update(SupplierDetails details, Guid userId, DateTimeOffset now, Guid requestId)
    {
        Apply(details);
        Touch(userId, now, requestId);
    }

    public SupplierDetails Snapshot() =>
        new(Code, Name, CountryCode, ContactName, Email, Phone, AddressLine, PostalCode, City, LegalIdentifiers, Notes);

    private void Apply(SupplierDetails details)
    {
        Code = details.Code;
        CodeKey = CatalogKeys.Normalize(details.Code);
        Name = details.Name;
        CountryCode = details.CountryCode;
        ContactName = details.ContactName;
        Email = details.Email;
        Phone = details.Phone;
        AddressLine = details.AddressLine;
        PostalCode = details.PostalCode;
        City = details.City;
        LegalIdentifiersJson = AttributeJson.Serialize(details.LegalIdentifiers);
        Notes = details.Notes;
    }
}

public sealed record SupplierDetails(
    string Code,
    string Name,
    string CountryCode,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine,
    string? PostalCode,
    string? City,
    IReadOnlyDictionary<string, string> LegalIdentifiers,
    string? Notes)
{
    /// <summary>Champs différents (noms seulement : valeurs jamais journalisées).</summary>
    public IReadOnlyList<string> ChangedFields(SupplierDetails? before)
    {
        var fields = new List<string>();
        void Compare(string field, string? previous, string? current)
        {
            if (before is null ? current is not null : previous != current)
            {
                fields.Add(field);
            }
        }

        Compare("code", before?.Code, Code);
        Compare("name", before?.Name, Name);
        Compare("countryCode", before?.CountryCode, CountryCode);
        Compare("contactName", before?.ContactName, ContactName);
        Compare("email", before?.Email, Email);
        Compare("phone", before?.Phone, Phone);
        Compare("addressLine", before?.AddressLine, AddressLine);
        Compare("postalCode", before?.PostalCode, PostalCode);
        Compare("city", before?.City, City);
        Compare(
            "legalIdentifiers",
            before is null ? null : AttributeJson.Serialize(before.LegalIdentifiers),
            before is null && LegalIdentifiers.Count == 0 ? null : AttributeJson.Serialize(LegalIdentifiers));
        Compare("notes", before?.Notes, Notes);
        return fields;
    }
}

/// <summary>Identifiants légaux admis par pays : CONTRÔLE DE FORMAT seulement (aucune règle juridique déclarée).</summary>
public static class LegalIdentifierFormats
{
    /// <summary>Clé → expression régulière du format admis, par pays ; tout autre pays : aucune clé admise.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ByCountry { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            // Maroc : ICE (15 chiffres), IF (identifiant fiscal, 7 à 10 chiffres), RC (registre de commerce, 1 à 10 chiffres).
            ["MA"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ice"] = "^[0-9]{15}$",
                ["if"] = "^[0-9]{7,10}$",
                ["rc"] = "^[0-9]{1,10}$",
            },
            // Belgique : numéro d'entreprise (BCE / TVA) 10 chiffres, préfixe BE facultatif, points tolérés à la saisie.
            ["BE"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["enterpriseNumber"] = "^(BE)?[01][0-9]{9}$",
            },
        };
}

/// <summary>
/// Rattachement variante ↔ fournisseur (D-05-06) : référence fournisseur, prix d'achat fournisseur HT (PROTÉGÉ, avec sa
/// devise, aucune conversion), délai indicatif, au plus un principal par variante. Retrait = marquage (conservé).
/// Un prix fournisseur ne modifie JAMAIS de lui-même le prix d'achat HT de référence du magasin (D-05-16).
/// </summary>
public sealed class VariantSupplier
{
    public const int LeadTimeMaxDays = 365;

    private VariantSupplier()
    {
    }

    public Guid VariantId { get; private set; }

    public Guid SupplierId { get; private set; }

    public string? SupplierReference { get; private set; }

    public decimal? SupplierPriceHt { get; private set; }

    public string? SupplierCurrency { get; private set; }

    public int? LeadTimeDays { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public Guid? RemovedByUserId { get; private set; }

    public Guid? LastRequestId { get; private set; }

    public uint Version { get; private set; }

    public bool IsRemoved => RemovedAtUtc is not null;

    public static VariantSupplier Create(Guid variantId, Guid supplierId, VariantSupplierDetails details, Guid userId, DateTimeOffset now, Guid requestId)
    {
        var link = new VariantSupplier { VariantId = variantId, SupplierId = supplierId, CreatedAtUtc = now, CreatedByUserId = userId };
        link.Apply(details, userId, now, requestId);
        return link;
    }

    /// <summary>Modification ou ré-ajout d'un rattachement retiré (réactivé).</summary>
    public void Apply(VariantSupplierDetails details, Guid userId, DateTimeOffset now, Guid requestId)
    {
        SupplierReference = details.SupplierReference;
        SupplierPriceHt = details.SupplierPriceHt;
        SupplierCurrency = details.SupplierCurrency;
        LeadTimeDays = details.LeadTimeDays;
        IsPrimary = details.IsPrimary;
        RemovedAtUtc = null;
        RemovedByUserId = null;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }

    public void ClearPrimary(Guid userId, DateTimeOffset now, Guid requestId)
    {
        IsPrimary = false;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }

    public void Remove(Guid userId, DateTimeOffset now, Guid requestId)
    {
        IsPrimary = false;
        RemovedAtUtc = now;
        RemovedByUserId = userId;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }

    public VariantSupplierDetails Snapshot() => new(SupplierReference, SupplierPriceHt, SupplierCurrency, LeadTimeDays, IsPrimary);
}

public sealed record VariantSupplierDetails(string? SupplierReference, decimal? SupplierPriceHt, string? SupplierCurrency, int? LeadTimeDays, bool IsPrimary);
