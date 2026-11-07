using System.Text.RegularExpressions;
using MMV.Domain.Stores;

namespace MMV.Application.Catalog;

public enum StoreChangeStatus
{
    Created,
    InvalidInput,
    Duplicate,
    UnknownStore,
}

public sealed record StoreChangeResult(StoreChangeStatus Status, Guid? Id = null, IReadOnlyList<string>? Errors = null);

/// <summary>Écritures des magasins et emplacements : réservées à <c>MMV.Maintenance</c> (aucune route API en Phase 05).</summary>
public interface IStoreAdministrationStore
{
    Task<Store?> FindStoreByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> LocationCodeTakenAsync(Guid storeId, string code, CancellationToken cancellationToken);

    void Add(Store store);

    void Add(Location location);
}

/// <summary>
/// Magasins (D-05-15, D-05-20) : création par la commande de maintenance seulement, devise EUR ou MAD fixée à la création
/// et immuable (déclencheur SQL), emplacement par défaut créé avec le magasin. Aucun profil fiscal validé ici : le TTC
/// reste indisponible (ADR-010). Aucune donnée pilote créée par la migration.
/// </summary>
public sealed partial class StoreAdministrationService(IStoreAdministrationStore store, ICatalogUnitOfWork unitOfWork, TimeProvider clock)
{
    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryPattern();

    public async Task<StoreChangeResult> CreateStoreAsync(string code, string countryCode, string currency, string name, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        code = code.Trim().ToUpperInvariant();
        countryCode = countryCode.Trim().ToUpperInvariant();
        currency = currency.Trim().ToUpperInvariant();
        name = name.Trim();
        if (!CodePattern().IsMatch(code))
        {
            errors.Add("code : 1 à 32 caractères A-Z, 0-9, « - », « _ ».");
        }

        if (!CountryPattern().IsMatch(countryCode))
        {
            errors.Add("pays : code ISO 3166-1 alpha-2 (BE, MA…).");
        }

        if (!Currencies.IsSupported(currency))
        {
            errors.Add($"devise : {string.Join(" ou ", Currencies.Supported)} seulement en Phase 05 (aucune conversion).");
        }

        if (name.Length is 0 or > Store.NameMaxLength)
        {
            errors.Add($"nom : 1 à {Store.NameMaxLength} caractères.");
        }

        if (errors.Count > 0)
        {
            return new(StoreChangeStatus.InvalidInput, Errors: errors);
        }

        if (await store.FindStoreByCodeAsync(code, cancellationToken) is not null)
        {
            return new(StoreChangeStatus.Duplicate);
        }

        var now = CatalogClock.Now(clock);
        var created = Store.Create(Guid.CreateVersion7(now), code, name, countryCode, currency, now);
        store.Add(created);
        store.Add(Location.Create(Guid.CreateVersion7(now), created.Id, "MAGASIN", "Magasin", isDefault: true, now));
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? new(StoreChangeStatus.Created, created.Id) : new(StoreChangeStatus.Duplicate);
    }

    public async Task<StoreChangeResult> CreateLocationAsync(string storeCode, string code, string name, CancellationToken cancellationToken)
    {
        var shop = await store.FindStoreByCodeAsync(storeCode.Trim().ToUpperInvariant(), cancellationToken);
        if (shop is null)
        {
            return new(StoreChangeStatus.UnknownStore);
        }

        code = code.Trim().ToUpperInvariant();
        name = name.Trim();
        if (!CodePattern().IsMatch(code) || name.Length is 0 or > Location.NameMaxLength)
        {
            return new(StoreChangeStatus.InvalidInput, Errors: ["code ou nom d'emplacement invalide."]);
        }

        if (await store.LocationCodeTakenAsync(shop.Id, code, cancellationToken))
        {
            return new(StoreChangeStatus.Duplicate);
        }

        var now = CatalogClock.Now(clock);
        var location = Location.Create(Guid.CreateVersion7(now), shop.Id, code, name, isDefault: false, now);
        store.Add(location);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? new(StoreChangeStatus.Created, location.Id) : new(StoreChangeStatus.Duplicate);
    }
}
