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
                // UX-IA (03/11/2026) : tarif obligatoire pour CHAQUE variante — prix manuel pour l'article commandé.
                new
                {
                    variant = new { label = "Écaille 52", stockPolicy = "order_only" }, suppliers = new[] { new { supplierId = supplier, isPrimary = true } },
                    price = new { mode = "manual", salePriceHt = "99.00", reason = "Prix catalogue" },
                },
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

        // Prestation : stock initial refusé ; fiche sans fournisseur acceptée. UX-IA (03/11/2026) : la fiche SANS prix,
        // admise jusqu'ici (COR-05-06), est désormais refusée — tarification obligatoire.
        var service = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "services", name = $"Prestation {s}",
            variants = new[] { new { variant = new { label = "Ajustage", stockPolicy = "service" }, stock = new { locationId = bench.LocationBe, quantity = 1 } } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, service.Status);
        Assert.Contains("variants[0].stock", service.Raw, StringComparison.Ordinal);
        var unpriced = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "services", name = $"Prestation {s}", variants = new[] { new { variant = new { label = "Ajustage", stockPolicy = "service" } } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unpriced.Status);
        Assert.Contains("variants[0].price", unpriced.Raw, StringComparison.Ordinal);
        var minimal = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "services", name = $"Prestation {s}",
            variants = new[] { new { variant = new { label = "Ajustage", stockPolicy = "service" }, price = new { mode = "manual", salePriceHt = "15.00" } } },
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
    /// <summary>
    /// UX-IA — tarification obligatoire : une seule variante sans tarif refuse TOUTE la création (aucune écriture) ; prix
    /// par variante (coefficient calculé par le serveur, prix manuel réservé à pricing.manual).
    /// </summary>
    [Phase05Fact]
    public async Task Wizard_RefusesAnyUnpricedVariant_AndPricesEachVariantIndependently()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("admin", AllRights);
        var s = bench.Suffix;

        object Variant(string label, object? price) => price is null
            ? new { variant = new { label, stockPolicy = "stocked" } }
            : new { variant = new { label, stockPolicy = "stocked" }, price };

        // Variante 2 sans tarif : 422 précis, rien n'est écrit (ni modèle ni prix de la variante 1).
        var partial = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", name = $"Tarif partiel {s}",
            variants = new[] { Variant("Noir", new { mode = "coefficient", purchasePriceHt = "40", coefficient = "2.5", expectedSalePriceHt = "100.00" }), Variant("Bleu", null) },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, partial.Status);
        Assert.Contains("\"variants[1].price\"", partial.Raw, StringComparison.Ordinal);
        Assert.Contains("required", partial.Raw, StringComparison.Ordinal);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", $"Tarif partiel {s}")));

        // Mode inconnu (ex. ancien « later ») : refusé.
        var later = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", name = $"Tarif plus tard {s}", variants = new[] { Variant("Noir", new { mode = "later" }) },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, later.Status);
        Assert.Contains("variants[0].price.mode", later.Raw, StringComparison.Ordinal);

        // Prix différent par variante : coefficient (serveur, arrondi au centime) et manuel.
        var created = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", name = $"Tarif par variante {s}",
            variants = new[]
            {
                Variant("Noir", new { mode = "coefficient", purchasePriceHt = "33.3333", coefficient = "3", expectedSalePriceHt = "100.00" }),
                Variant("Or", new { mode = "manual", salePriceHt = "149.90", reason = "Finition or" }),
            },
        });
        Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
        var black = created["variants"][0].GetProperty("variantId").GetGuid();
        var gold = created["variants"][1].GetProperty("variantId").GetGuid();
        Assert.Equal("100.00", (await bench.GetAsync(admin, $"/api/catalog/variants/{black}"))["row"].GetProperty("price").GetProperty("salePriceHt").GetString());
        var goldPrice = (await bench.GetAsync(admin, $"/api/catalog/variants/{gold}"))["row"].GetProperty("price");
        Assert.Equal("149.90", goldPrice.GetProperty("salePriceHt").GetString());
        Assert.Equal("manual", goldPrice.GetProperty("pricingMode").GetString());
        // Historique des prix conservé : chaque variante tarifée a son historique.
        foreach (var id in new[] { black, gold })
        {
            var history = await bench.GetAsync(admin, $"/api/pricing/variants/{id}/history");
            Assert.True(history.Status == HttpStatusCode.OK, history.Raw);
            Assert.NotEqual("[]", history.Raw.Trim());
        }

        // Prix manuel sans pricing.manual : 403 avant écriture.
        var pricer = await bench.UserAsync("tarifeur", Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.PricingWrite, Permissions.CostsRead);
        var manual = await bench.PostAsync(pricer, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", name = $"Tarif manuel refusé {s}", variants = new[] { Variant("Noir", new { mode = "manual", salePriceHt = "10.00" }) },
        });
        Assert.Equal(HttpStatusCode.Forbidden, manual.Status);
        Assert.Equal("manual_price_forbidden", manual.Code);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", $"Tarif manuel refusé {s}")));
    }

    /// <summary>
    /// UX-IA — seuil d'alerte (option A) : un seuil par (variante, magasin), indépendant des emplacements ; alerte quand le
    /// disponible AGRÉGÉ du magasin (en main − réservé, emplacements actifs) atteint ou passe sous le seuil.
    /// </summary>
    [Phase05Fact]
    public async Task Wizard_SetsThresholdPerStore_AlertOnAggregatedAvailable()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("admin", [.. AllRights, Permissions.StockAdjust]);
        var s = bench.Suffix;
        var price = new { mode = "manual", salePriceHt = "120.00" };

        var created = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", brand = "Ray-Ban", name = $"Seuil {s}",
            variants = new[] { new { variant = new { label = "Noir", stockPolicy = "stocked" }, price, stock = new { locationId = bench.LocationBe, quantity = 20 }, lowStockThreshold = 10 } },
        });
        Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
        var variant = created["variants"][0].GetProperty("variantId").GetGuid();
        Assert.Equal(1L, await bench.ScalarAsync<long>(
            "SELECT count(*) FROM stock.variant_store_settings WHERE variant_id = @v AND store_id = @st AND low_stock_threshold = 10", ("v", variant), ("st", bench.StoreBe)));

        // Second emplacement du même magasin : 20 + 15 = 35 disponibles, un seul seuil (10) pour le magasin.
        var moved = await bench.PostAsync(admin, "/api/stock/movements", new
        {
            requestId = NewId(), variantId = variant, locationId = bench.LocationBeBack, type = "manual_in", quantity = 15, reasonCode = "found",
        });
        Assert.True(moved.Status is HttpStatusCode.Created or HttpStatusCode.OK, moved.Raw);
        var row = (await bench.GetAsync(admin, $"/api/catalog/variants/{variant}"))["row"];
        Assert.Equal(35, row.GetProperty("stock").GetProperty("available").GetInt32());
        Assert.Equal(10, row.GetProperty("stock").GetProperty("lowStockThreshold").GetInt32());
        Assert.Equal("in_stock", row.GetProperty("availability").GetString());
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.variant_store_settings WHERE variant_id = @v", ("v", variant)));

        // Sorties de 20 (A) et 5 (B) : disponible agrégé = 10 = seuil → « stock faible » (seuil atteint).
        var outgoing = await bench.PostAsync(admin, "/api/stock/movements", new
        {
            requestId = NewId(), variantId = variant, locationId = bench.LocationBe, type = "manual_out", quantity = 20, reasonCode = "loss",
        });
        Assert.True(outgoing.Status is HttpStatusCode.Created or HttpStatusCode.OK, outgoing.Raw);
        outgoing = await bench.PostAsync(admin, "/api/stock/movements", new
        {
            requestId = NewId(), variantId = variant, locationId = bench.LocationBeBack, type = "manual_out", quantity = 5, reasonCode = "loss",
        });
        Assert.True(outgoing.Status is HttpStatusCode.Created or HttpStatusCode.OK, outgoing.Raw);
        row = (await bench.GetAsync(admin, $"/api/catalog/variants/{variant}"))["row"];
        Assert.Equal(10, row.GetProperty("stock").GetProperty("available").GetInt32());
        Assert.Equal("low_stock", row.GetProperty("availability").GetString());
        var alerts = await bench.GetAsync(admin, $"/api/stock/alerts?storeId={bench.StoreBe}");
        Assert.True(alerts.Status == HttpStatusCode.OK, alerts.Raw);
        Assert.Contains(alerts["items"].EnumerateArray(), a => a.GetProperty("variantId").GetGuid() == variant && a.GetProperty("availability").GetString() == "low_stock");

        // Seuil modifiable ensuite (fiche produit → PUT /api/stock/thresholds) : 9 → au-dessus, plus d'alerte.
        var balances = await bench.GetAsync(admin, $"/api/stock/balances?storeId={bench.StoreBe}&q={created["variants"][0].GetProperty("sku").GetString()}");
        var version = balances["items"].EnumerateArray().First(b => b.GetProperty("variantId").GetGuid() == variant).GetProperty("thresholdVersion").GetUInt32();
        var changed = await bench.PutAsync(admin, $"/api/stock/thresholds/{variant}", new { requestId = NewId(), version, storeId = bench.StoreBe, lowStockThreshold = 9 });
        Assert.True(changed.Status == HttpStatusCode.OK, changed.Raw);
        Assert.Equal("in_stock", (await bench.GetAsync(admin, $"/api/catalog/variants/{variant}"))["row"].GetProperty("availability").GetString());

        // Seuil pour un article commandé : refusé ; seuil sans stock.adjust : 403 avant écriture.
        var orderOnly = await bench.PostAsync(admin, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", name = $"Seuil commandé {s}",
            variants = new[] { new { variant = new { label = "Noir", stockPolicy = "order_only" }, price, lowStockThreshold = 2 } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, orderOnly.Status);
        Assert.Contains("variants[0].lowStockThreshold", orderOnly.Raw, StringComparison.Ordinal);
        var clerk = await bench.UserAsync("magasinier", AllRights);
        var noAdjust = await bench.PostAsync(clerk, "/api/catalog/products", new
        {
            requestId = NewId(), familyCode = "optical_frames", name = $"Seuil sans droit {s}",
            variants = new[] { new { variant = new { label = "Noir", stockPolicy = "stocked" }, price, lowStockThreshold = 2 } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, noAdjust.Status);
        Assert.Equal("threshold_forbidden", noAdjust.Code);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE name = @n", ("n", $"Seuil sans droit {s}")));
    }
}
