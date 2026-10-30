using System.Net;
using System.Text.Json;
using MMV.Domain.Identity;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Intégration finale de la Phase 05 (mandat du 30/10/2026 § 3), en complément de <see cref="Phase05PostgreSqlTests"/> :
/// les six familles (modèles et variantes), le référentiel des verres, le stock réservé (aucune sortie ni ajustement
/// sous la quantité réservée, contrainte de base), les inventaires complets et partiels, et la matrice des 16 droits
/// (chaque droit nécessaire à sa route, aucune écriture par les sondes). Données fictives, purgées (résidu 0).
/// Avec <c>MMV_PG05_API_BASE</c>, les requêtes passent par le processus MMV.Api réel.
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05FinalIntegrationTests(ITestOutputHelper output)
{
    private static readonly string[] Everything = [.. Permissions.Phase05, Permissions.AuditRead];

    private static Guid NewId() => Guid.NewGuid();

    private static async Task<Guid> LensReferenceAsync(Phase05Bench bench, string user, string kind, object body)
    {
        var created = await bench.PostAsync(user, $"/api/catalog/lens-references/{kind}", body);
        Assert.True(created.Status == HttpStatusCode.Created, $"{kind} : {created.Raw}");
        return created["id"].GetGuid();
    }

    [Phase05Fact]
    public async Task SixFamilies_ModelsAndVariants_LensCatalog()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var reader = await bench.UserAsync("lecteur", Permissions.CatalogRead);

        // Référentiel des verres : fabricant → gamme → indice, traitement ; droit catalog.configure exigé.
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PostAsync(reader, "/api/catalog/lens-references/manufacturers", new { requestId = NewId(), name = $"Fabricant {bench.Suffix}" })).Status);
        var manufacturer = await LensReferenceAsync(bench, admin, "manufacturers", new { requestId = NewId(), name = $"Fabricant fictif {bench.Suffix}" });
        var range = await LensReferenceAsync(bench, admin, "ranges", new { requestId = NewId(), name = $"Gamme fictive {bench.Suffix}", manufacturerId = manufacturer });
        var index = await LensReferenceAsync(bench, admin, "indices", new { requestId = NewId(), name = $"Indice {bench.Suffix}", indexValue = "1.6" });
        var treatment = await LensReferenceAsync(bench, admin, "treatments", new { requestId = NewId(), name = $"Antireflet fictif {bench.Suffix}" });
        var references = await bench.GetAsync(reader, "/api/catalog/lens-references");
        Assert.Equal(HttpStatusCode.OK, references.Status);
        foreach (var name in new[] { $"Fabricant fictif {bench.Suffix}", $"Gamme fictive {bench.Suffix}", $"Antireflet fictif {bench.Suffix}" })
        {
            Assert.Contains(name, references.Raw, StringComparison.Ordinal);
        }

        // Une gamme exige son fabricant ; un verre exige sa gamme (modèle) et son indice (variante).
        var orphanRange = await bench.PostAsync(admin, "/api/catalog/lens-references/ranges", new { requestId = NewId(), name = $"Gamme orpheline {bench.Suffix}" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, orphanRange.Status);
        var noRange = await bench.PostAsync(admin, "/api/catalog/models", new { requestId = NewId(), familyCode = "ophthalmic_lenses", name = $"Verre {bench.Suffix}", variants = new[] { new { sku = $"VER-{bench.Suffix}-X", label = "X", stockPolicy = "order_only" } } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noRange.Status);
        Assert.Contains("lensRangeId", noRange.Raw, StringComparison.Ordinal);

        var families = new (string Family, string Policy, object? Extra)[]
        {
            ("optical_frames", "stocked", null), ("sunglasses", "stocked", null), ("ophthalmic_lenses", "order_only", range),
            ("contact_lenses_solutions", "order_only", null), ("accessories", "stocked", null), ("services", "service", null),
        };
        foreach (var (family, policy, lensRange) in families)
        {
            var sku = $"FAM-{bench.Suffix}-{family}";
            var created = await bench.PostAsync(admin, "/api/catalog/models", new
            {
                requestId = NewId(),
                familyCode = family,
                brand = "Marque fictive",
                name = $"Modèle {bench.Suffix} {family}",
                lensRangeId = lensRange,
                variants = new[]
                {
                    new
                    {
                        sku, label = $"Variante {family}", stockPolicy = policy,
                        lensIndexId = family == "ophthalmic_lenses" ? index : (Guid?)null,
                        treatmentIds = family == "ophthalmic_lenses" ? new[] { treatment } : null,
                    },
                },
            });
            Assert.True(created.Status == HttpStatusCode.Created, $"{family} : {created.Raw}");
            output.WriteLine($"famille {family} : modèle créé, variante {sku} ({policy})");
        }

        // Une prestation n'a pas de stock ; un bien ne peut pas être une prestation (politiques de la famille).
        var badService = await bench.PostAsync(admin, "/api/catalog/models", new { requestId = NewId(), familyCode = "services", name = $"Prestation {bench.Suffix}", variants = new[] { new { sku = $"SRV-{bench.Suffix}-X", label = "X", stockPolicy = "stocked" } } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badService.Status);

        var list = await bench.GetAsync(reader, $"/api/catalog/variants?storeId={bench.StoreBe}&q=FAM-{bench.Suffix}&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, list.Status);
        var codes = list["items"].EnumerateArray().Select(i => i.GetProperty("familyCode").GetString()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(families.Select(f => f.Family).Order(StringComparer.Ordinal).ToArray(), codes);
        Assert.Equal(1L, await bench.ScalarAsync<long>(
            "SELECT count(*) FROM catalog.variant_lens_treatments t JOIN catalog.product_variants v ON v.id = t.variant_id WHERE v.sku = @s AND t.treatment_id = @t",
            ("s", $"FAM-{bench.Suffix}-ophthalmic_lenses"), ("t", treatment)));
        Assert.Equal(6L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.families"));
    }

    /// <summary>
    /// La réservation naît avec les ventes (Phase 06) : la quantité réservée est posée ici par SQL sur la base de test, pour
    /// éprouver dès maintenant la règle « disponible = en main − réservé » des sorties et ajustements, et la contrainte.
    /// </summary>
    [Phase05Fact]
    public async Task ReservedStock_CannotBeTakenOut_NorAdjustedBelow()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var created = await bench.PostAsync(admin, "/api/catalog/models", new { requestId = NewId(), familyCode = "accessories", name = $"Étui {bench.Suffix}", variants = new[] { new { sku = $"RES-{bench.Suffix}", label = "Étui fictif", stockPolicy = "stocked" } } });
        Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
        var variant = await bench.ScalarAsync<Guid>("SELECT id FROM catalog.product_variants WHERE sku = @s", ("s", $"RES-{bench.Suffix}"));
        Assert.Equal(HttpStatusCode.Created, (await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = variant, locationId = bench.LocationBe, type = "manual_in", quantity = 5, reasonCode = "found" })).Status);
        await bench.ExecuteAsync("UPDATE stock.balances SET reserved = 3 WHERE variant_id = @v AND location_id = @l", ("v", variant), ("l", bench.LocationBe));

        var movements = await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE variant_id = @v", ("v", variant));
        var outOfReserved = await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = variant, locationId = bench.LocationBe, type = "manual_out", quantity = 3, reasonCode = "breakage" });
        Assert.Equal("stock_reserved", outOfReserved.Code);
        Assert.Equal(movements, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE variant_id = @v", ("v", variant)));

        var withinAvailable = await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = variant, locationId = bench.LocationBe, type = "manual_out", quantity = 2, reasonCode = "breakage" });
        Assert.True(withinAvailable.Status == HttpStatusCode.Created, withinAvailable.Raw);
        Assert.Equal(3, await bench.ScalarAsync<int>("SELECT on_hand FROM stock.balances WHERE variant_id = @v AND location_id = @l", ("v", variant), ("l", bench.LocationBe)));

        var adjustBelow = await bench.PostAsync(admin, "/api/stock/adjustments", new { requestId = NewId(), variantId = variant, locationId = bench.LocationBe, expectedOnHand = 3, countedQuantity = 2, reasonCode = "loss" });
        Assert.Equal("stock_reserved", adjustBelow.Code);
        Assert.Equal(3, await bench.ScalarAsync<int>("SELECT on_hand FROM stock.balances WHERE variant_id = @v AND location_id = @l", ("v", variant), ("l", bench.LocationBe)));

        // La base refuse elle-même une réservation supérieure au stock en main.
        await Assert.ThrowsAsync<PostgresException>(() => bench.ExecuteAsync("UPDATE stock.balances SET reserved = 4 WHERE variant_id = @v AND location_id = @l", ("v", variant), ("l", bench.LocationBe)));
        var row = (await bench.GetAsync(admin, $"/api/stock/balances?storeId={bench.StoreBe}&locationId={bench.LocationBe}&pageSize=200"))["items"].EnumerateArray()
            .Single(r => r.GetProperty("variantId").GetGuid() == variant);
        output.WriteLine($"solde affiché : {row.GetRawText()}");
        Assert.Equal(3, row.GetProperty("onHand").GetInt32());
    }

    [Phase05Fact]
    public async Task Inventories_Full_And_Partial_UncountedRules()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        async Task<Guid> VariantAsync(string family, string sku, string policy)
        {
            var created = await bench.PostAsync(admin, "/api/catalog/models", new { requestId = NewId(), familyCode = family, name = $"Modèle {sku}", variants = new[] { new { sku, label = sku, stockPolicy = policy } } });
            Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
            return await bench.ScalarAsync<Guid>("SELECT id FROM catalog.product_variants WHERE sku = @s", ("s", sku));
        }

        var frame = await VariantAsync("optical_frames", $"INVF-{bench.Suffix}", "stocked");
        var accessory = await VariantAsync("accessories", $"INVA-{bench.Suffix}", "stocked");
        var ordered = await VariantAsync("accessories", $"INVO-{bench.Suffix}", "order_only");
        foreach (var (v, q) in new[] { (frame, 4), (accessory, 6) })
        {
            Assert.Equal(HttpStatusCode.Created, (await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = v, locationId = bench.LocationBe, type = "manual_in", quantity = q, reasonCode = "found" })).Status);
        }

        // Inventaire COMPLET du magasin (deux emplacements), non aveugle : variantes stockées seulement.
        var full = await bench.PostAsync(admin, "/api/inventories", new { requestId = NewId(), storeId = bench.StoreBe, locationIds = new[] { bench.LocationBe, bench.LocationBeBack }, scopeType = "full" });
        Assert.True(full.Status == HttpStatusCode.Created, full.Raw);
        var session = full["id"].GetGuid();
        var detail = await bench.GetAsync(admin, $"/api/inventories/{session}");
        var lines = detail["lines"].EnumerateArray().ToList();
        Assert.DoesNotContain(lines, l => l.GetProperty("variantId").GetGuid() == ordered);
        Assert.Contains(lines, l => l.GetProperty("variantId").GetGuid() == frame && l.GetProperty("locationId").GetGuid() == bench.LocationBe && l.GetProperty("expectedAtOpen").GetInt32() == 4);
        output.WriteLine($"inventaire complet : {lines.Count} ligne(s)");

        // Un inventaire partiel recouvrant le complet ouvert est refusé (sessions concurrentes).
        Assert.Equal("inventory_open", (await bench.PostAsync(admin, "/api/inventories", new { requestId = NewId(), storeId = bench.StoreBe, locationIds = new[] { bench.LocationBe }, scopeType = "family", scopeFamilyCode = "accessories" })).Code);

        // Comptage de la monture seulement ; « set_to_zero » remettrait l'accessoire à 0 : on exclut les non comptés.
        uint LineVersion(ApiResult d, Guid v, Guid l) => d["lines"].EnumerateArray().Single(x => x.GetProperty("variantId").GetGuid() == v && x.GetProperty("locationId").GetGuid() == l).GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.OK, (await bench.PutAsync(admin, $"/api/inventories/{session}/lines/{frame}/{bench.LocationBe}", new { requestId = NewId(), version = LineVersion(detail, frame, bench.LocationBe), countedQuantity = 4 })).Status);
        var version = (await bench.GetAsync(admin, $"/api/inventories/{session}"))["summary"].GetProperty("version").GetUInt32();
        var validated = await bench.PostAsync(admin, $"/api/inventories/{session}/validate", new { requestId = NewId(), version, uncounted = "exclude" });
        Assert.True(validated.Status == HttpStatusCode.OK, validated.Raw);
        Assert.Equal(6, await bench.ScalarAsync<int>("SELECT on_hand FROM stock.balances WHERE variant_id = @v AND location_id = @l", ("v", accessory), ("l", bench.LocationBe)));
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE source_id = @s", ("s", session)));

        // Inventaire PARTIEL (famille « accessories ») : la monture est hors périmètre ; non compté → mis à zéro.
        var partial = await bench.PostAsync(admin, "/api/inventories", new { requestId = NewId(), storeId = bench.StoreBe, locationIds = new[] { bench.LocationBe }, scopeType = "family", scopeFamilyCode = "accessories" });
        Assert.True(partial.Status == HttpStatusCode.Created, partial.Raw);
        var partialId = partial["id"].GetGuid();
        var partialLines = (await bench.GetAsync(admin, $"/api/inventories/{partialId}"))["lines"].EnumerateArray().ToList();
        Assert.DoesNotContain(partialLines, l => l.GetProperty("variantId").GetGuid() == frame);
        Assert.Contains(partialLines, l => l.GetProperty("variantId").GetGuid() == accessory);
        var pv = (await bench.GetAsync(admin, $"/api/inventories/{partialId}"))["summary"].GetProperty("version").GetUInt32();
        var unjustified = await bench.PostAsync(admin, $"/api/inventories/{partialId}/validate", new { requestId = NewId(), version = pv, uncounted = "set_to_zero" });
        output.WriteLine($"validation set_to_zero sans motif : {(int)unjustified.Status} {unjustified.Code}");
        Assert.NotEqual(HttpStatusCode.OK, unjustified.Status);
        Assert.Equal(6, await bench.ScalarAsync<int>("SELECT on_hand FROM stock.balances WHERE variant_id = @v AND location_id = @l", ("v", accessory), ("l", bench.LocationBe)));
        var abandon = await bench.PostAsync(admin, $"/api/inventories/{partialId}/abandon", new { requestId = NewId(), version = (await bench.GetAsync(admin, $"/api/inventories/{partialId}"))["summary"].GetProperty("version").GetUInt32(), reason = "Essai fictif" });
        Assert.True(abandon.Status == HttpStatusCode.OK, abandon.Raw);
        Assert.Equal(6, await bench.ScalarAsync<int>("SELECT on_hand FROM stock.balances WHERE variant_id = @v AND location_id = @l", ("v", accessory), ("l", bench.LocationBe)));
        Assert.Equal(4, await bench.ScalarAsync<int>("SELECT on_hand FROM stock.balances WHERE variant_id = @v AND location_id = @l", ("v", frame), ("l", bench.LocationBe)));
    }

    /// <summary>
    /// Matrice des 16 droits : pour chaque droit, une sonde (identifiant inconnu ou corps invalide : rien n'est écrit)
    /// acceptée par l'autorisation avec l'ensemble requis (statut ≠ 403, &lt; 500), refusée (403) dès que ce droit manque.
    /// </summary>
    [Phase05Fact]
    public async Task SixteenPermissions_EachIsRequiredByItsRoute()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var unknown = Guid.NewGuid();
        var probes = new (string Permission, HttpMethod Method, string Path, object? Body, string[] Required)[]
        {
            (Permissions.CatalogRead, HttpMethod.Get, "/api/catalog/families", null, [Permissions.CatalogRead]),
            (Permissions.CatalogWrite, HttpMethod.Post, "/api/catalog/models", new { }, [Permissions.CatalogWrite]),
            (Permissions.CatalogArchive, HttpMethod.Post, $"/api/catalog/models/{unknown}/archive", new { requestId = NewId(), version = 0 }, [Permissions.CatalogArchive]),
            (Permissions.CatalogConfigure, HttpMethod.Post, "/api/catalog/lens-references/manufacturers", new { }, [Permissions.CatalogConfigure]),
            (Permissions.CatalogImport, HttpMethod.Get, "/api/imports/fields", null, [Permissions.CatalogImport]),
            (Permissions.PricingWrite, HttpMethod.Post, "/api/pricing/preview", new { storeId = bench.StoreBe, purchasePriceHt = "1", coefficient = "2" }, [Permissions.PricingWrite, Permissions.CostsRead]),
            (Permissions.PricingManual, HttpMethod.Put, $"/api/pricing/variants/{unknown}/manual-price", new { }, [Permissions.PricingManual]),
            (Permissions.CostsRead, HttpMethod.Post, "/api/pricing/preview", new { storeId = bench.StoreBe, purchasePriceHt = "1", coefficient = "2" }, [Permissions.PricingWrite, Permissions.CostsRead]),
            (Permissions.CostsWrite, HttpMethod.Put, $"/api/pricing/variants/{unknown}/purchase-price", new { }, [Permissions.CostsWrite]),
            (Permissions.SuppliersRead, HttpMethod.Get, "/api/suppliers", null, [Permissions.SuppliersRead]),
            (Permissions.SuppliersWrite, HttpMethod.Post, "/api/suppliers", new { }, [Permissions.SuppliersWrite]),
            (Permissions.StockRead, HttpMethod.Get, $"/api/stock/balances?storeId={bench.StoreBe}", null, [Permissions.StockRead]),
            (Permissions.StockMovementsWrite, HttpMethod.Post, "/api/stock/movements", new { }, [Permissions.StockMovementsWrite]),
            (Permissions.StockAdjust, HttpMethod.Post, "/api/stock/adjustments", new { }, [Permissions.StockAdjust]),
            (Permissions.InventoryCount, HttpMethod.Post, "/api/inventories", new { }, [Permissions.InventoryCount]),
            (Permissions.InventoryValidate, HttpMethod.Post, $"/api/inventories/{unknown}/validate", new { requestId = NewId(), version = 0, uncounted = "exclude" }, [Permissions.InventoryValidate]),
        };
        Assert.Equal(Permissions.Phase05.Order(StringComparer.Ordinal), probes.Select(p => p.Permission).Order(StringComparer.Ordinal));

        var rows = await bench.ScalarAsync<long>("SELECT (SELECT count(*) FROM catalog.product_models) + (SELECT count(*) FROM catalog.suppliers) + (SELECT count(*) FROM stock.movements) + (SELECT count(*) FROM stock.inventory_sessions) + (SELECT count(*) FROM catalog.lens_references)");
        var number = 0;
        foreach (var (permission, method, path, body, required) in probes)
        {
            number++;
            var allowed = await bench.UserAsync($"d{number:00}", required);
            var granted = await bench.SendAsync(allowed, method, path, body);
            Assert.True(granted.Status != HttpStatusCode.Forbidden && (int)granted.Status < 500, $"{permission} : {method} {path} avec {string.Join("+", required)} -> {(int)granted.Status} {granted.Raw}");

            var others = Permissions.Phase05.Where(p => p != permission).Append(Permissions.SystemAdmin).Append(Permissions.AuditRead).ToArray();
            var missing = await bench.UserAsync($"m{number:00}", others);
            var refused = await bench.SendAsync(missing, method, path, body);
            Assert.True(refused.Status == HttpStatusCode.Forbidden, $"{permission} absent (15 autres droits + system.admin + audit.read) : {method} {path} -> {(int)refused.Status}");
            output.WriteLine($"{permission,-22} {method,-4} {path.Replace(unknown.ToString(), "{inconnu}", StringComparison.Ordinal).Replace(bench.StoreBe.ToString(), "{magasin}", StringComparison.Ordinal)} : avec {(int)granted.Status}, sans 403");
        }

        Assert.Equal(rows, await bench.ScalarAsync<long>("SELECT (SELECT count(*) FROM catalog.product_models) + (SELECT count(*) FROM catalog.suppliers) + (SELECT count(*) FROM stock.movements) + (SELECT count(*) FROM stock.inventory_sessions) + (SELECT count(*) FROM catalog.lens_references)"));
    }
}
