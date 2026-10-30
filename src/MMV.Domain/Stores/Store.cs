namespace MMV.Domain.Stores;

/// <summary>
/// Magasin (phase 05, D-05-15, D-05-20) : porte la DEVISE (ISO 4217, EUR ou MAD en Phase 05) et le statut de son profil
/// fiscal. Aucune conversion entre devises ; aucun taux ni catégorie fiscale inventés (ADR-010 ouvert). Créé par
/// <c>MMV.Maintenance stores create</c> (aucun écran en Phase 05) ; jamais supprimé. Devise non modifiable (aucune
/// méthode de modification ; déclencheur SQL en garantie).
/// </summary>
public sealed class Store
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 120;

    private Store()
    {
        Code = string.Empty;
        Name = string.Empty;
        CountryCode = string.Empty;
        CurrencyCode = string.Empty;
        FiscalProfileStatus = FiscalProfileStatuses.NotValidated;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 (<c>BE</c>, <c>MA</c>…).</summary>
    public string CountryCode { get; private set; }

    /// <summary>ISO 4217 (<see cref="Currencies"/>).</summary>
    public string CurrencyCode { get; private set; }

    /// <summary><c>not_validated</c> tant qu'aucune décision fiscale n'est enregistrée : TTC indisponible.</summary>
    public string FiscalProfileStatus { get; private set; }

    public DateTimeOffset? FiscalValidatedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public bool IsFiscalProfileValidated => FiscalProfileStatus == FiscalProfileStatuses.Validated;

    public static Store Create(Guid id, string code, string name, string countryCode, string currencyCode, DateTimeOffset now)
    {
        if (!Currencies.IsSupported(currencyCode))
        {
            throw new ArgumentException("Devise non prise en charge.", nameof(currencyCode));
        }

        return new Store
        {
            Id = id,
            Code = code,
            Name = name,
            CountryCode = countryCode,
            CurrencyCode = currencyCode,
            CreatedAtUtc = now,
        };
    }

    public void Archive(DateTimeOffset now) => ArchivedAtUtc ??= now;
}

/// <summary>Emplacement de stock d'un magasin ; exactement un emplacement par défaut actif par magasin.</summary>
public sealed class Location
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 120;

    private Location()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid StoreId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public static Location Create(Guid id, Guid storeId, string code, string name, bool isDefault, DateTimeOffset now) =>
        new()
        {
            Id = id,
            StoreId = storeId,
            Code = code,
            Name = name,
            IsDefault = isDefault,
            CreatedAtUtc = now,
        };
}

/// <summary>Devises admises en Phase 05 (D-05-20 : pilotes fictifs Belgique — EUR, Maroc — MAD). Unité mineure : centime.</summary>
public static class Currencies
{
    public const string Euro = "EUR";
    public const string Dirham = "MAD";

    public static IReadOnlyList<string> Supported { get; } = [Euro, Dirham];

    public static bool IsSupported(string? code) => code is Euro or Dirham;
}

public static class FiscalProfileStatuses
{
    public const string NotValidated = "not_validated";
    public const string Validated = "validated";
}
