using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MMV.Domain.Identity;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// COR-05-01 — un serveur local = un magasin : magasin résolu par la configuration du serveur, jamais par le poste ;
/// un <c>storeId</c> d'un autre magasin est refusé comme un magasin inexistant ; ambiguïté = échec sûr (503).
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05StoreContextTests(ITestOutputHelper output)
{
    private static Guid NewId() => Guid.NewGuid();

    [Phase05Fact]
    public async Task Store_IsTheInstallationStore_AndAnotherStoreIsNeverServed()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("admin", Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.CostsRead, Permissions.CostsWrite, Permissions.PricingWrite,
            Permissions.StockRead, Permissions.StockAdjust, Permissions.StockMovementsWrite, Permissions.InventoryCount, Permissions.InventoryValidate, Permissions.SuppliersRead);

        var current = await bench.GetAsync(admin, "/api/store");
        Assert.Equal(HttpStatusCode.OK, current.Status);
        Assert.Equal(bench.StoreBe, current["store"].GetProperty("id").GetGuid());
        Assert.Equal("EUR", current["store"].GetProperty("currency").GetString());
        Assert.Equal("configuration", current["source"].GetString());
        var list = await bench.GetAsync(admin, "/api/stores");
        Assert.Equal(1, list.Body.GetArrayLength());
        Assert.Equal(bench.StoreBe, list.Body[0].GetProperty("id").GetGuid());

        var currentMa = await bench.GetMaAsync(admin, "/api/store");
        Assert.Equal(bench.StoreMa, currentMa["store"].GetProperty("id").GetGuid());
        Assert.Equal("MAD", currentMa["store"].GetProperty("currency").GetString());

        // Sans storeId : magasin de l'installation ; avec le storeId d'un autre magasin : 404 store_not_found.
        Assert.Equal(HttpStatusCode.OK, (await bench.GetAsync(admin, "/api/catalog/variants")).Status);
        foreach (var path in new[]
        {
            $"/api/catalog/variants?storeId={bench.StoreMa}", $"/api/stock/balances?storeId={bench.StoreMa}", $"/api/stock/alerts?storeId={bench.StoreMa}",
            $"/api/stock/movements?storeId={bench.StoreMa}", $"/api/stock/receipts?storeId={bench.StoreMa}", $"/api/inventories?storeId={bench.StoreMa}",
            $"/api/pricing/recalculations?storeId={bench.StoreMa}",
        })
        {
            var refused = await bench.GetAsync(admin, path);
            Assert.True(refused.Status == HttpStatusCode.NotFound && refused.Code == "store_not_found", $"{path} : {refused.Status} {refused.Raw}");
        }

        var model = await bench.PostAsync(admin, "/api/catalog/models", new
        {
            requestId = NewId(), familyCode = "optical_frames", brand = "Banc", name = $"Monture contexte {bench.Suffix}",
            variants = new[] { new { sku = $"CTX-{bench.Suffix}", label = "Noir", stockPolicy = "stocked" } },
        });
        Assert.Equal(HttpStatusCode.Created, model.Status);
        var modelId = model["id"].GetGuid();
        var variant = (await bench.GetAsync(admin, $"/api/catalog/models/{modelId}"))["variants"][0].GetProperty("row").GetProperty("variantId").GetGuid();

        // Écritures : tarif, seuil, mouvement vers un emplacement d'un autre magasin, inventaire d'un autre magasin.
        var price = await bench.PutAsync(admin, $"/api/pricing/variants/{variant}", new { requestId = NewId(), storeId = bench.StoreMa, purchasePriceHt = "10", coefficient = "2", expectedSalePriceHt = "20.00" });
        Assert.Equal("store_not_found", price.Code);
        var threshold = await bench.PutAsync(admin, $"/api/stock/thresholds/{variant}", new { requestId = NewId(), storeId = bench.StoreMa, lowStockThreshold = 2 });
        Assert.Equal("store_not_found", threshold.Code);
        var movement = await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = variant, locationId = bench.LocationMa, type = "manual_in", quantity = 3, reasonCode = "found" });
        Assert.Equal(HttpStatusCode.NotFound, movement.Status);
        Assert.Equal("location_not_found", movement.Code);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE location_id = @l", ("l", bench.LocationMa)));

        var inventoryMa = await bench.PostMaAsync(admin, "/api/inventories", new { requestId = NewId(), locationIds = new[] { bench.LocationMa }, scopeType = "full" });
        Assert.Equal(HttpStatusCode.Created, inventoryMa.Status);
        var sessionMa = inventoryMa["id"].GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await bench.GetAsync(admin, $"/api/inventories/{sessionMa}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await bench.PostAsync(admin, $"/api/inventories/{sessionMa}/abandon", new { requestId = NewId(), version = 1, reason = "x" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await bench.GetMaAsync(admin, $"/api/inventories/{sessionMa}")).Status);

        // Le prix MAD reste dans la devise MAD : le serveur MA n'accepte pas l'euro, le serveur BE ne sert que l'euro.
        var mad = await bench.PutMaAsync(admin, $"/api/pricing/variants/{variant}", new { requestId = NewId(), currency = "EUR", purchasePriceHt = "10", coefficient = "2", expectedSalePriceHt = "20.00" });
        Assert.Equal("currency_mismatch", mad.Code);
    }

    [Phase05Fact]
    public async Task Server_WithoutStoreCode_AndSeveralStores_FailsSafely()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var reader = await bench.UserAsync("lecteur", Permissions.CatalogRead, Permissions.StockRead);
        await using var unconfigured = Server(null);
        using var http = unconfigured.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        foreach (var path in new[] { "/api/store", "/api/catalog/variants", "/api/stock/balances" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bench.TokenOf(reader));
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("\"code\":\"store_not_configured\"", body, StringComparison.Ordinal);
            Assert.Contains("\"reason\":\"ambiguous\"", body, StringComparison.Ordinal);
        }

        await using var unknown = Server("CODE-INEXISTANT");
        using var http2 = unknown.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/store");
        request2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bench.TokenOf(reader));
        using var response2 = await http2.SendAsync(request2);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response2.StatusCode);
        Assert.Contains("\"reason\":\"unknown_code\"", await response2.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Server(string? storeCode) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Mmv"] = Phase05Target.ConnectionString,
            ["Installation:StoreCode"] = storeCode ?? string.Empty,
        }));
    });

    /// <summary>
    /// UX-IA (03/11/2026) — routes ajoutées ou étendues : assistant produit (tarif, stock, seuil), import de tarifs et seuil
    /// depuis la fiche refusent un autre magasin (404 store_not_found, rien d'écrit) ; aucun magasin n'est jamais choisi par le poste.
    /// </summary>
    [Phase05Fact]
    public async Task UxAiRoutes_RefuseAnotherStore_NothingWritten()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("admin", Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.CatalogImport, Permissions.PricingManual,
            Permissions.StockRead, Permissions.StockMovementsWrite, Permissions.StockAdjust, Permissions.CostsRead, Permissions.CostsWrite);
        var name = $"Autre magasin {bench.Suffix}";
        var product = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), storeId = bench.StoreMa, familyCode = "optical_frames", name,
            variants = new[] { new { variant = new { label = "Noir", stockPolicy = "stocked" }, price = new { mode = "manual", salePriceHt = "10.00" }, lowStockThreshold = 2 } },
        });
        Assert.True(product.Status == HttpStatusCode.NotFound && product.Code == "store_not_found", product.Raw);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", name)));

        // Stock initial vers un emplacement d'un autre magasin : refusé avant écriture.
        var foreignLocation = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", name,
            variants = new[] { new { variant = new { label = "Noir", stockPolicy = "stocked" }, price = new { mode = "manual", salePriceHt = "10.00" }, stock = new { locationId = bench.LocationMa, quantity = 1 } } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foreignLocation.Status);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", name)));

        var prices = await bench.PostAsync(admin, "/api/imports", new { requestId = NewId(), kind = "prices", storeId = bench.StoreMa, fileName = "tarifs.csv", content = "SKU;Prix vente HT\nX;1\n" });
        Assert.True(prices.Status == HttpStatusCode.NotFound && prices.Code == "store_not_found", prices.Raw);
        Assert.Equal(HttpStatusCode.OK, (await bench.GetAsync(admin, "/api/imports/assistant")).Status);
    }
}
