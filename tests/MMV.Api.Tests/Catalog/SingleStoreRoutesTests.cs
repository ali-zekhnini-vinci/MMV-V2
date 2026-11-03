using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MMV.Api.Tests.Catalog;

/// <summary>
/// UX-IA, COR-05-01 vérifié — un serveur = un magasin : la table des routes de l'API ne contient AUCUNE écriture sur un
/// magasin ou sur le rattachement de l'installation (création et emplacements : console de maintenance seulement), et
/// chaque groupe commercial exige le magasin de l'installation. Contrôle structurel, sans base.
/// </summary>
public sealed class SingleStoreRoutesTests
{
    private static readonly string[] CommercialPrefixes = ["/api/catalog", "/api/pricing", "/api/suppliers", "/api/stock", "/api/inventories", "/api/imports"];

    private static IReadOnlyList<RouteEndpoint> Routes()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = null })));
        return [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
    }

    private static IEnumerable<string> Methods(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

    [Fact]
    public void NoRoute_WritesAStore_OrTheInstallationBinding()
    {
        var writes = Routes()
            .Where(e => Methods(e).Any(m => m is "POST" or "PUT" or "PATCH" or "DELETE"))
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Where(p => p.Contains("/store", StringComparison.OrdinalIgnoreCase) || p.Contains("installation", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(writes);
    }

    [Fact]
    public void StoreRoutes_AreReadOnly_AndCommercialGroups_RequireTheInstallationStore()
    {
        var routes = Routes();
        var storeReads = routes.Where(e => e.RoutePattern.RawText is "/api/stores" or "/api/store").ToList();
        Assert.Equal(2, storeReads.Count);
        Assert.All(storeReads, e => Assert.Equal(["GET"], Methods(e)));

        // Les nouvelles routes UX-IA appartiennent aux groupes commerciaux, tous filtrés par le magasin de l'installation
        // (refus d'un autre magasin et 503 sans magasin résolu : vérifiés en base par Phase05StoreContextTests).
        var commercial = routes.Where(e => e.RoutePattern.RawText is { } p
            && CommercialPrefixes.Any(prefix => p.StartsWith(prefix, StringComparison.Ordinal))).ToList();
        Assert.NotEmpty(commercial);
        Assert.Contains(commercial, e => e.RoutePattern.RawText == "/api/catalog/lens-catalog");
        Assert.Contains(commercial, e => e.RoutePattern.RawText == "/api/imports/assistant");
    }
}
