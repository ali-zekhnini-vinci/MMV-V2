using System.Net;
using MMV.Domain.Identity;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// COR-05-06 — enregistrement final de l'assistant (<c>POST /api/catalog/products</c>) : tout ou rien, idempotent,
/// droits par section contrôlés avant écriture, SKU générés par le serveur, aucun stock pour une prestation.
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05ProductWizardTests(ITestOutputHelper output)
{
    private static Guid NewId() => Guid.NewGuid();

    private static readonly string[] AllRights =
    [
        Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.CostsRead, Permissions.CostsWrite, Permissions.PricingWrite, Permissions.PricingManual,
        Permissions.StockRead, Permissions.StockMovementsWrite, Permissions.SuppliersRead, Permissions.SuppliersWrite,
    ];

    [Phase05Fact]
    public async Task Wizard_CreatesEverything_InOneTransaction_OrNothing()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("admin", AllRights);
        var s = bench.Suffix;
        var supplier = (await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"WIZ{s}", name = $"Fournisseur assistant {s}", countryCode = "BE" }))["id"].GetGuid();

        object Body(Guid requestId, string name, string expected, Guid location) => new
        {
            requestId, familyCode = "optical_frames", brand = "Banc", name,
            variants = new object[]
            {
                new
                {
                    variant = new { label = "Noir 52", stockPolicy = "stocked", attributes = new Dictionary<string, string> { ["color"] = "Noir" } },
                    suppliers = new[] { new { supplierId = supplier, supplierReference = $"0012-{s}", isPrimary = true } },
                    price = new { mode = "coefficient", purchasePriceHt = "40.1250", coefficient = "2.5", expectedSalePriceHt = expected },
                    stock = new { locationId = location, quantity = 3 },
                },
                new { variant = new { label = "Écaille 52", stockPolicy = "order_only" }, suppliers = new[] { new { supplierId = supplier, isPrimary = true } } },
            },
        };

        // Échec à la DERNIÈRE étape possible (aperçu périmé du prix, après création du modèle et des liens) : rien ne reste.
        var stale = await bench.PostAsync(admin, "/api/catalog/products", Body(NewId(), $"Assistant échec {s}", "1.00", bench.LocationBe));
        Assert.True(stale.Status is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity, stale.Raw);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", $"Assistant échec {s}")));
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.variant_suppliers WHERE supplier_id = @s", ("s", supplier)));
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE location_id = @l", ("l", bench.LocationBe)));

        // Emplacement d'un autre magasin : refus avant toute écriture.
        var foreign = await bench.PostAsync(admin, "/api/catalog/products", Body(NewId(), $"Assistant étranger {s}", "100.31", bench.LocationMa));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foreign.Status);
        Assert.Contains("variants[0].stock.locationId", foreign.Raw, StringComparison.Ordinal);

        // Succès : 40.1250 × 2.5 = 100.3125 → 100.31 (arrondi serveur au centime).
        var requestId = NewId();
        var created = await bench.PostAsync(admin, "/api/catalog/products", Body(requestId, $"Assistant {s}", "100.31", bench.LocationBe));
        Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
        var modelId = created["modelId"].GetGuid();
        var variants = created["variants"];
        Assert.Equal(2, variants.GetArrayLength());
        Assert.StartsWith("MON-", variants[0].GetProperty("sku").GetString(), StringComparison.Ordinal);
        Assert.NotEqual(variants[0].GetProperty("sku").GetString(), variants[1].GetProperty("sku").GetString());
        var first = variants[0].GetProperty("variantId").GetGuid();
        var detail = await bench.GetAsync(admin, $"/api/catalog/variants/{first}");
        Assert.Equal("100.31", detail["row"].GetProperty("price").GetProperty("salePriceHt").GetString());
        Assert.Equal("40.1250", detail["row"].GetProperty("price").GetProperty("purchasePriceHt").GetString());
        Assert.Equal(3, detail["row"].GetProperty("stock").GetProperty("onHand").GetInt32());
        Assert.Equal($"0012-{s}", detail["suppliers"][0].GetProperty("supplierReference").GetString());
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE variant_id = @v AND reason_code = 'initial_stock'", ("v", first)));

        // Réémission : même produit, aucun doublon (ni modèle, ni lien, ni mouvement).
        var replay = await bench.PostAsync(admin, "/api/catalog/products", Body(requestId, $"Assistant {s}", "100.31", bench.LocationBe));
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.True(replay["replayed"].GetBoolean());
        Assert.Equal(modelId, replay["modelId"].GetGuid());
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", $"Assistant {s}")));
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE variant_id = @v", ("v", first)));

        // Prestation : stock initial refusé ; fiche minimale (sans fournisseur ni prix) acceptée.
        var service = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "services", name = $"Prestation {s}",
            variants = new[] { new { variant = new { label = "Ajustage", stockPolicy = "service" }, stock = new { locationId = bench.LocationBe, quantity = 1 } } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, service.Status);
        Assert.Contains("variants[0].stock", service.Raw, StringComparison.Ordinal);
        var minimal = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "services", name = $"Prestation {s}", variants = new[] { new { variant = new { label = "Ajustage", stockPolicy = "service" } } },
        });
        Assert.Equal(HttpStatusCode.Created, minimal.Status);
        Assert.StartsWith("PRE-", minimal["variants"][0].GetProperty("sku").GetString(), StringComparison.Ordinal);

        // Droits par section : sans pricing.write, refus 403 AVANT écriture (aucun modèle créé).
        var writer = await bench.UserAsync("redacteur", Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.SuppliersRead, Permissions.StockMovementsWrite);
        var noPricing = await bench.PostAsync(writer, "/api/catalog/products", Body(NewId(), $"Assistant sans droit {s}", "100.31", bench.LocationBe));
        Assert.Equal(HttpStatusCode.Forbidden, noPricing.Status);
        Assert.Equal("pricing_forbidden", noPricing.Code);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", $"Assistant sans droit {s}")));
    }
}
