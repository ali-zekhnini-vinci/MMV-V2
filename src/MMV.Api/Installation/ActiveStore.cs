using MMV.Api.Clients;
using MMV.Application.Catalog;
using MMV.Domain.Stores;

namespace MMV.Api.Installation;

/// <summary>
/// Configuration d'installation (COR-05-01) : UN serveur local = UN magasin. Section <c>Installation</c>, clé
/// <c>StoreCode</c> (code de <c>org.stores</c>, créé par <c>MMV.Maintenance stores create</c>). Sans clé, le serveur
/// n'adopte le magasin QUE s'il est l'unique magasin actif de la base ; zéro ou plusieurs magasins : échec sûr
/// (<c>503 store_not_configured</c>), jamais de choix silencieux.
/// </summary>
public sealed class InstallationOptions
{
    public const string SectionName = "Installation";

    public string? StoreCode { get; set; }
}

/// <summary>Motif d'une installation sans magasin résolu (codes bornés, servis au poste).</summary>
public static class StoreResolutionProblems
{
    public const string NoStore = "no_store";
    public const string Ambiguous = "ambiguous";
    public const string UnknownCode = "unknown_code";
    public const string Archived = "archived";
}

/// <summary>Magasin résolu et origine de la résolution (<c>configuration</c> ou <c>single_store</c>).</summary>
public sealed record ActiveStoreResolution(Store? Store, string? Source, string? Problem)
{
    public bool IsResolved => Store is not null;
}

/// <summary>
/// Résolution du magasin de l'installation. L'identifiant résolu est mémorisé (singleton) ; l'entité est relue à
/// chaque requête (statut fiscal, archivage). Un échec n'est jamais mémorisé : le magasin créé après le démarrage est
/// adopté sans redémarrage. Aucune donnée n'est écrite.
/// </summary>
public sealed partial class InstallationStoreResolver(InstallationOptions options, ILogger<InstallationStoreResolver> logger)
{
    private Guid? _storeId;
    private string? _source;

    public string? ConfiguredCode { get; } = string.IsNullOrWhiteSpace(options.StoreCode) ? null : options.StoreCode.Trim().ToUpperInvariant();

    public async Task<ActiveStoreResolution> ResolveAsync(ICatalogStore store, CancellationToken cancellationToken)
    {
        if (_storeId is { } known)
        {
            var current = await store.GetStoreAsync(known, cancellationToken);
            return current is null || current.IsArchived
                ? new ActiveStoreResolution(null, null, StoreResolutionProblems.Archived)
                : new ActiveStoreResolution(current, _source, null);
        }

        var stores = await store.ListStoresAsync(includeArchived: true, cancellationToken);
        Store? chosen;
        string source;
        if (ConfiguredCode is { } code)
        {
            chosen = stores.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.Ordinal));
            if (chosen is null || chosen.IsArchived)
            {
                LogUnresolved(logger, chosen is null ? StoreResolutionProblems.UnknownCode : StoreResolutionProblems.Archived, code);
                return new ActiveStoreResolution(null, null, chosen is null ? StoreResolutionProblems.UnknownCode : StoreResolutionProblems.Archived);
            }

            source = "configuration";
        }
        else
        {
            var active = stores.Where(s => !s.IsArchived).ToList();
            if (active.Count != 1)
            {
                var problem = active.Count == 0 ? StoreResolutionProblems.NoStore : StoreResolutionProblems.Ambiguous;
                LogUnresolved(logger, problem, "(non configuré)");
                return new ActiveStoreResolution(null, null, problem);
            }

            chosen = active[0];
            source = "single_store";
        }

        (_storeId, _source) = (chosen.Id, source);
        LogResolved(logger, chosen.Code, chosen.CurrencyCode, source);
        return new ActiveStoreResolution(chosen, source, null);
    }

    [LoggerMessage(EventId = 5100, Level = LogLevel.Information, Message = "Installation : magasin actif {Code} ({Currency}), origine {Source}.")]
    private static partial void LogResolved(ILogger logger, string code, string currency, string source);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Warning, Message = "Installation : aucun magasin actif ({Problem}, code {Code}). Renseigner Installation:StoreCode.")]
    private static partial void LogUnresolved(ILogger logger, string problem, string code);
}

/// <summary>
/// Contexte magasin d'une requête : posé par <see cref="ActiveStoreFilter"/> avant le gestionnaire. Le
/// <c>storeId</c> éventuellement transmis par le poste n'est qu'une assertion : différent du magasin de l'installation,
/// il est refusé comme un magasin inexistant (<c>404 store_not_found</c>, aucune donnée d'un autre magasin).
/// </summary>
internal static class ActiveStore
{
    private const string ItemKey = "mmv.active-store";

    public static Store Of(HttpContext context) =>
        context.Items[ItemKey] as Store ?? throw new InvalidOperationException("Route magasin sans ActiveStoreFilter.");

    /// <summary>Magasin actif si <paramref name="requested"/> est absent ou identique ; sinon null.</summary>
    public static Store? Match(HttpContext context, Guid? requested)
    {
        var store = Of(context);
        return requested is null || requested == store.Id ? store : null;
    }

    public static bool Owns(HttpContext context, Guid storeId) => Of(context).Id == storeId;

    internal static void Set(HttpContext context, Store store) => context.Items[ItemKey] = store;

    public static readonly ErrorResponse StoreNotFound = new("store_not_found");
}

/// <summary>Filtre des groupes Catalogue, Tarifs, Stock, Inventaires et Import : magasin résolu ou 503.</summary>
internal static class ActiveStoreFilter
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var resolver = http.RequestServices.GetRequiredService<InstallationStoreResolver>();
        var resolution = await resolver.ResolveAsync(http.RequestServices.GetRequiredService<ICatalogStore>(), http.RequestAborted);
        if (!resolution.IsResolved)
        {
            return Results.Json(new StoreNotConfiguredResponse("store_not_configured", resolution.Problem!), statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        ActiveStore.Set(http, resolution.Store!);
        return await next(context);
    }

    public static TBuilder RequireActiveStore<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.AddEndpointFilter(InvokeAsync);
}

public sealed record StoreNotConfiguredResponse(string Code, string Reason);

public static class InstallationSetup
{
    public static IServiceCollection AddInstallation(this IServiceCollection services, IConfiguration configuration)
    {
        // Lecture différée : la configuration complète (variables d'environnement, secrets, hôte de test) est celle de l'hôte construit.
        _ = configuration;
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection(InstallationOptions.SectionName).Get<InstallationOptions>() ?? new InstallationOptions());
        services.AddSingleton<InstallationStoreResolver>();
        return services;
    }
}
