using System.Net;
using System.Text.Json;
using MMV.Domain.Identity;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Phase 05 sur PostgreSQL 18 RÉEL et pipeline HTTPS réel de MMV.Api : catalogue, tarifs (coefficient, arrondi, aperçu,
/// prix manuel, historique, EUR / MAD), projection des coûts, fournisseurs, stock (négatif interdit, sorties
/// concurrentes, réceptions partielles, ajustements), inventaires (comptage, mouvements concurrents, validation
/// atomique), import CSV (contrôle à blanc, tout ou rien, réémission), audit sans coût, journaux en ajout seul.
/// Données fictives, purgées (résidu 0). Exécution : <c>--filter "Stage=Phase05"</c>.
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05PostgreSqlTests(ITestOutputHelper output)
{
    private static readonly string[] Everything = [.. Permissions.Phase05, Permissions.AuditRead];

    private static Guid NewId() => Guid.NewGuid();

    private static async Task<(Guid ModelId, Guid[] Variants)> CreateModelAsync(Phase05Bench bench, string user, string family, params (string Sku, string Policy)[] variants)
    {
        var created = await bench.PostAsync(user, "/api/catalog/models", new
        {
            requestId = NewId(),
            familyCode = family,
            brand = "Marque fictive",
            name = $"Modèle {bench.Suffix} {variants[0].Sku}",
            variants = variants.Select(v => new { sku = v.Sku, label = $"Variante {v.Sku}", stockPolicy = v.Policy }).ToArray(),
        });
        Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
        var modelId = created["id"].GetGuid();
        var model = await bench.GetAsync(user, $"/api/catalog/models/{modelId}?storeId={bench.StoreBe}");
        Assert.Equal(HttpStatusCode.OK, model.Status);
        var ids = variants.Select(v => model["variants"].EnumerateArray().Single(x => x.GetProperty("row").GetProperty("sku").GetString() == v.Sku)
            .GetProperty("row").GetProperty("variantId").GetGuid()).ToArray();
        return (modelId, ids);
    }

    private static async Task<uint> PriceVersionAsync(Phase05Bench bench, string user, Guid variant, Guid store) =>
        (await bench.GetAsync(user, $"/api/pricing/variants/{variant}?storeId={store}"))["price"].GetProperty("version").GetUInt32();

    private static async Task<(int OnHand, uint Version)> BalanceAsync(Phase05Bench bench, string user, Guid variant, Guid location)
    {
        var page = await bench.GetAsync(user, $"/api/stock/balances?storeId={bench.StoreBe}&locationId={location}&pageSize=200");
        var row = page["items"].EnumerateArray().Single(r => r.GetProperty("variantId").GetGuid() == variant);
        return (row.GetProperty("onHand").GetInt32(), row.GetProperty("version").GetUInt32());
    }

    [Phase05Fact]
    public async Task Catalog_Pricing_CostsProjection_EurAndMad()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var seller = await bench.UserAsync("vendeur", Permissions.CatalogRead, Permissions.StockRead);
        var costsOnly = await bench.UserAsync("couts", Permissions.CatalogRead, Permissions.CostsRead, Permissions.CostsWrite);

        var (modelId, variants) = await CreateModelAsync(bench, admin, "optical_frames", ($"SKU-{bench.Suffix}-1", "stocked"), ($"SKU-{bench.Suffix}-2", "order_only"));
        var variant = variants[0];

        // SKU unique, insensible à la casse et aux espaces de bord.
        var duplicate = await bench.PostAsync(admin, $"/api/catalog/models/{modelId}/variants", new { requestId = NewId(), sku = $"  sku-{bench.Suffix.ToLowerInvariant()}-1 ", label = "Doublon", stockPolicy = "stocked" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.Status);
        Assert.Contains("\"sku\":[\"duplicate\"]", duplicate.Raw, StringComparison.Ordinal);

        // Politique non admise par la famille (prestation pour une monture) ; code-barres invalide.
        var invalid = await bench.PostAsync(admin, $"/api/catalog/models/{modelId}/variants", new { requestId = NewId(), sku = $"SKU-{bench.Suffix}-9", label = "X", stockPolicy = "service", barcode = "1234567890123" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.Status);
        Assert.Contains("stockPolicy", invalid.Raw, StringComparison.Ordinal);
        Assert.Contains("barcode", invalid.Raw, StringComparison.Ordinal);

        // Aperçu : un seul arrondi au centime, demi vers l'extérieur. 41,2500 × 2,5000 = 103,125 → 103,13.
        var preview = await bench.PostAsync(admin, "/api/pricing/preview", new { storeId = bench.StoreBe, purchasePriceHt = "41.25", coefficient = "2.5" });
        Assert.Equal("103.13", preview["computedSalePriceHt"].GetString());
        Assert.Equal("EUR", preview["currency"].GetString());

        // Aperçu périmé → 409, rien n'est écrit.
        var stale = await bench.PutAsync(admin, $"/api/pricing/variants/{variant}", new { requestId = NewId(), storeId = bench.StoreBe, purchasePriceHt = "41.25", coefficient = "2.5", expectedSalePriceHt = "103.12" });
        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.Equal("price_preview_stale", stale.Code);
        Assert.Equal(0u, await PriceVersionAsync(bench, admin, variant, bench.StoreBe));

        var saved = await bench.PutAsync(admin, $"/api/pricing/variants/{variant}", new { requestId = NewId(), storeId = bench.StoreBe, purchasePriceHt = "41.25", coefficient = "2.5", expectedSalePriceHt = "103.13" });
        Assert.True(saved.Status == HttpStatusCode.OK, saved.Raw);
        Assert.Equal("103.13", saved["price"].GetProperty("salePriceHt").GetString());
        Assert.Equal("41.2500", saved["price"].GetProperty("purchasePriceHt").GetString());
        Assert.Equal("tax_not_validated", saved["price"].GetProperty("ttcStatus").GetString());
        Assert.False(saved["price"].TryGetProperty("salePriceTtc", out _));

        // MAD : même variante, autre magasin (AUTRE SERVEUR, COR-05-01), autre devise, aucune conversion ; devise annoncée différente → refus.
        var mismatch = await bench.PutMaAsync(admin, $"/api/pricing/variants/{variant}", new { requestId = NewId(), storeId = bench.StoreMa, currency = "EUR", purchasePriceHt = "400", coefficient = "2.2", expectedSalePriceHt = "880.00" });
        Assert.Equal("currency_mismatch", mismatch.Code);
        var mad = await bench.PutMaAsync(admin, $"/api/pricing/variants/{variant}", new { requestId = NewId(), storeId = bench.StoreMa, purchasePriceHt = "19.99", coefficient = "2.2", expectedSalePriceHt = "43.98" });
        Assert.Equal("MAD", mad["price"].GetProperty("currency").GetString());
        Assert.Equal("43.98", mad["price"].GetProperty("salePriceHt").GetString());
        Assert.Equal("103.13", (await bench.GetAsync(admin, $"/api/pricing/variants/{variant}?storeId={bench.StoreBe}"))["price"].GetProperty("salePriceHt").GetString());
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM pricing.variant_store_prices WHERE currency_code <> (SELECT currency_code FROM org.stores s WHERE s.id = store_id)"));

        // Vendeur sans costs.read : AUCUNE clé de coût, dans la liste comme dans la fiche et l'historique.
        var list = await bench.GetAsync(seller, $"/api/catalog/variants?storeId={bench.StoreBe}&q={bench.Suffix}");
        Assert.Equal(HttpStatusCode.OK, list.Status);
        foreach (var key in new[] { "\"purchasePriceHt\":", "\"coefficient\":", "\"computedSalePriceHt\":", "\"effectiveCoefficient\":", "\"margin\":", "41.2500", "2.5000", "\"oldValue\":\"41" })
        {
            Assert.DoesNotContain(key, list.Raw, StringComparison.Ordinal);
            Assert.DoesNotContain(key, (await bench.GetAsync(seller, $"/api/catalog/variants/{variant}?storeId={bench.StoreBe}")).Raw, StringComparison.Ordinal);
            Assert.DoesNotContain(key, (await bench.GetAsync(seller, $"/api/pricing/variants/{variant}/history?storeId={bench.StoreBe}")).Raw, StringComparison.Ordinal);
        }

        Assert.Contains("103.13", list.Raw, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PostAsync(seller, "/api/pricing/preview", new { storeId = bench.StoreBe, purchasePriceHt = "1", coefficient = "2" })).Status);

        // Prix d'achat seul (costs.write) : le prix de vente NE CHANGE PAS → recalcul en attente, jamais silencieux.
        var version = await PriceVersionAsync(bench, admin, variant, bench.StoreBe);
        var purchase = await bench.PutAsync(costsOnly, $"/api/pricing/variants/{variant}/purchase-price", new { requestId = NewId(), version, storeId = bench.StoreBe, purchasePriceHt = "50" });
        Assert.True(purchase.Status == HttpStatusCode.OK, purchase.Raw);
        Assert.Equal("103.13", purchase["price"].GetProperty("salePriceHt").GetString());
        Assert.Equal("recalculation_pending", purchase["price"].GetProperty("priceStatus").GetString());
        Assert.Equal("125.00", purchase["price"].GetProperty("computedSalePriceHt").GetString());

        var pending = await bench.GetAsync(admin, $"/api/pricing/recalculations?storeId={bench.StoreBe}");
        var item = pending.Body.EnumerateArray().Single(i => i.GetProperty("variantId").GetGuid() == variant);
        var applied = await bench.PostAsync(admin, "/api/pricing/recalculations/apply", new
        {
            requestId = NewId(), storeId = bench.StoreBe,
            items = new[] { new { variantId = variant, version = item.GetProperty("version").GetUInt32(), expectedSalePriceHt = "125.00" } },
        });
        Assert.True(applied.Status == HttpStatusCode.OK, applied.Raw);
        Assert.Equal("up_to_date", (await bench.GetAsync(admin, $"/api/pricing/variants/{variant}?storeId={bench.StoreBe}"))["price"].GetProperty("priceStatus").GetString());

        // Coefficient < 1 : confirmation explicite exigée.
        version = await PriceVersionAsync(bench, admin, variant, bench.StoreBe);
        var below = await bench.PutAsync(admin, $"/api/pricing/variants/{variant}", new { requestId = NewId(), version, storeId = bench.StoreBe, coefficient = "0.9", expectedSalePriceHt = "45.00" });
        Assert.Equal("coefficient_below_one_unconfirmed", below.Code);

        // Prix manuel : permission dédiée ; coefficient conservé, mode manuel, historique.
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PutAsync(costsOnly, $"/api/pricing/variants/{variant}/manual-price", new { requestId = NewId(), version, storeId = bench.StoreBe, salePriceHt = "99.90" })).Status);
        var manual = await bench.PutAsync(admin, $"/api/pricing/variants/{variant}/manual-price", new { requestId = NewId(), version, storeId = bench.StoreBe, salePriceHt = "99.90", reason = "Promotion fictive" });
        Assert.True(manual.Status == HttpStatusCode.OK, manual.Raw);
        Assert.Equal("manual", manual["price"].GetProperty("pricingMode").GetString());
        Assert.Equal("2.5000", manual["price"].GetProperty("coefficient").GetString());
        Assert.Equal("1.9980", manual["price"].GetProperty("effectiveCoefficient").GetString());

        // Version périmée → 409 (concurrence optimiste xmin).
        var conflict = await bench.PutAsync(admin, $"/api/pricing/variants/{variant}/manual-price", new { requestId = NewId(), version, storeId = bench.StoreBe, salePriceHt = "89.90" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.Status);

        var history = await bench.GetAsync(admin, $"/api/pricing/variants/{variant}/history?storeId={bench.StoreBe}");
        var fields = history.Body.EnumerateArray().Select(h => $"{h.GetProperty("field").GetString()}:{h.GetProperty("origin").GetString()}").ToList();
        Assert.Contains("sale_price_ht:manual_price", fields);
        Assert.Contains("sale_price_ht:recalculation", fields);
        Assert.Contains("purchase_price_ht:edit", fields);

        // Fiche incomplète : variante sur commande sans fournisseur principal.
        Assert.True((await bench.GetAsync(admin, $"/api/catalog/variants/{variants[1]}?storeId={bench.StoreBe}"))["row"].GetProperty("isIncomplete").GetBoolean());

        // Contrôle croisé de l'arrondi C# ↔ PostgreSQL round(numeric, 2).
        foreach (var (p, c) in new[] { ("41.2500", "2.5000"), ("19.9900", "2.2000"), ("0.0050", "1.0000"), ("12.3450", "1.0000"), ("99999999.9999", "100.0000"), ("33.3333", "0.3333") })
        {
            var server = await bench.PostAsync(admin, "/api/pricing/preview", new { storeId = bench.StoreBe, purchasePriceHt = p, coefficient = c });
            var sql = await bench.ScalarAsync<decimal>("SELECT round(@p::numeric * @c::numeric, 2)", ("p", p), ("c", c));
            Assert.Equal(sql.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), server["computedSalePriceHt"].GetString());
        }

        // Audit : aucun montant de coût dans les champs modifiés.
        var audit = await bench.ScalarAsync<string>("SELECT coalesce(string_agg(changes::text, ' '), '') FROM audit.events WHERE entity_id = @v", ("v", variant));
        Assert.DoesNotContain("41.25", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("2.5", audit, StringComparison.Ordinal);
        Assert.Contains("purchasePriceHt", audit, StringComparison.Ordinal);
    }

    [Phase05Fact]
    public async Task Suppliers_Links_Primary_Incomplete_And_ArchivedSupplier()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var linker = await bench.UserAsync("liens", Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.SuppliersRead);
        var (_, variants) = await CreateModelAsync(bench, admin, "contact_lenses_solutions", ($"LEN-{bench.Suffix}", "order_only"));

        var a = await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"F{bench.Suffix}A", name = "Fournisseur fictif A", countryCode = "MA", legalIdentifiers = new { ice = "001234567000089" } });
        Assert.True(a.Status == HttpStatusCode.Created, a.Raw);
        var badLegal = await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"F{bench.Suffix}X", name = "X", countryCode = "MA", legalIdentifiers = new { ice = "12" } });
        Assert.Contains("legalIdentifiers.ice", badLegal.Raw, StringComparison.Ordinal);
        var b = await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"F{bench.Suffix}B", name = "Fournisseur fictif B", countryCode = "BE" });
        Assert.True(b.Status == HttpStatusCode.Created, b.Raw);
        var supplierA = a["id"].GetGuid();
        var supplierB = b["id"].GetGuid();

        // Prix fournisseur sans costs.write → 403, rien n'est écrit.
        var forbidden = await bench.PutAsync(linker, $"/api/catalog/variants/{variants[0]}/suppliers/{supplierA}", new { requestId = NewId(), isPrimary = true, supplierPriceHt = "10" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.variant_suppliers WHERE variant_id = @v", ("v", variants[0])));

        Assert.Equal(HttpStatusCode.OK, (await bench.PutAsync(linker, $"/api/catalog/variants/{variants[0]}/suppliers/{supplierA}", new { requestId = NewId(), isPrimary = true, supplierReference = "REF-A" })).Status);
        Assert.False((await bench.GetAsync(admin, $"/api/catalog/variants/{variants[0]}?storeId={bench.StoreBe}"))["row"].GetProperty("isIncomplete").GetBoolean());
        // Nouveau principal : l'ancien perd son rôle dans la même transaction (au plus un principal).
        Assert.Equal(HttpStatusCode.OK, (await bench.PutAsync(admin, $"/api/catalog/variants/{variants[0]}/suppliers/{supplierB}", new { requestId = NewId(), isPrimary = true, supplierPriceHt = "12.5", supplierCurrency = "EUR" })).Status);
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.variant_suppliers WHERE variant_id = @v AND is_primary", ("v", variants[0])));
        var detail = await bench.GetAsync(admin, $"/api/catalog/variants/{variants[0]}?storeId={bench.StoreBe}");
        Assert.Equal(supplierB, detail["row"].GetProperty("primarySupplier").GetProperty("id").GetGuid());
        Assert.DoesNotContain("12.5", (await bench.GetAsync(linker, $"/api/catalog/variants/{variants[0]}?storeId={bench.StoreBe}")).Raw, StringComparison.Ordinal);

        // Archiver le principal d'une variante sur commande : confirmation exigée, puis variante « incomplète ».
        var version = (await bench.GetAsync(admin, $"/api/suppliers/{supplierB}"))["version"].GetUInt32();
        var refused = await bench.PostAsync(admin, $"/api/suppliers/{supplierB}/archive", new { requestId = NewId(), version });
        Assert.Equal("incomplete_variants_unconfirmed", refused.Code);
        Assert.Equal(HttpStatusCode.OK, (await bench.PostAsync(admin, $"/api/suppliers/{supplierB}/archive", new { requestId = NewId(), version, acceptIncompleteVariants = true })).Status);
        Assert.True((await bench.GetAsync(admin, $"/api/catalog/variants/{variants[0]}?storeId={bench.StoreBe}"))["row"].GetProperty("isIncomplete").GetBoolean());
        var linkVersion = (await bench.GetAsync(admin, $"/api/catalog/variants/{variants[0]}?storeId={bench.StoreBe}"))["suppliers"].EnumerateArray().Single(s => s.GetProperty("supplierId").GetGuid() == supplierB).GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await bench.PutAsync(admin, $"/api/catalog/variants/{variants[0]}/suppliers/{supplierB}", new { requestId = NewId(), version = linkVersion, isPrimary = true })).Status);
    }

    [Phase05Fact]
    public async Task Stock_NegativeForbidden_Receipts_Partial_Adjustments_Policies()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var clerk = await bench.UserAsync("magasinier", Permissions.StockRead, Permissions.StockMovementsWrite, Permissions.SuppliersRead, Permissions.CatalogRead);
        var (_, variants) = await CreateModelAsync(bench, admin, "accessories", ($"ACC-{bench.Suffix}-1", "stocked"), ($"ACC-{bench.Suffix}-2", "order_only"));
        var supplier = (await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"S{bench.Suffix}", name = "Fournisseur stock", countryCode = "BE" }))["id"].GetGuid();

        // Réception partielle : 10 annoncés, 6 reçus ; coût unitaire exige costs.write.
        var noCost = await bench.PostAsync(clerk, "/api/stock/receipts", new { requestId = NewId(), locationId = bench.LocationBe, supplierId = supplier, lines = new[] { new { variantId = variants[0], announcedQuantity = 10, receivedQuantity = 6, unitCostHt = "4.10" } } });
        Assert.Equal(HttpStatusCode.Forbidden, noCost.Status);
        var receipt = await bench.PostAsync(clerk, "/api/stock/receipts", new { requestId = NewId(), locationId = bench.LocationBe, supplierId = supplier, supplierDocumentReference = "BL-FICTIF-1", lines = new[] { new { variantId = variants[0], announcedQuantity = 10, receivedQuantity = 6 } } });
        Assert.True(receipt.Status == HttpStatusCode.Created, receipt.Raw);
        Assert.Equal(4, receipt["outstandingQuantity"].GetInt32());
        var completion = await bench.PostAsync(admin, "/api/stock/receipts", new { requestId = NewId(), locationId = bench.LocationBe, supplierId = supplier, completesReceiptId = receipt["id"].GetGuid(), lines = new[] { new { variantId = variants[0], receivedQuantity = 4, unitCostHt = "4.1000" } } });
        Assert.True(completion.Status == HttpStatusCode.Created, completion.Raw);
        var root = await bench.GetAsync(admin, $"/api/stock/receipts/{receipt["id"].GetGuid()}");
        Assert.Equal(0, root["outstanding"][0].GetProperty("outstanding").GetInt32());
        Assert.Equal(10, (await BalanceAsync(bench, admin, variants[0], bench.LocationBe)).OnHand);
        // Une réception ne modifie jamais le prix d'achat de référence.
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM pricing.variant_store_prices WHERE variant_id = @v", ("v", variants[0])));

        // Aucun stock pour une variante commandée à la demande.
        var orderOnly = await bench.PostAsync(clerk, "/api/stock/movements", new { requestId = NewId(), variantId = variants[1], locationId = bench.LocationBe, type = "manual_in", quantity = 1, reasonCode = "found" });
        Assert.Equal("not_stocked", orderOnly.Code);

        // Stock négatif interdit (et motif obligatoire, commentaire pour « other »).
        var tooMuch = await bench.PostAsync(clerk, "/api/stock/movements", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, type = "manual_out", quantity = 11, reasonCode = "breakage" });
        Assert.Equal("stock_insufficient", tooMuch.Code);
        var noComment = await bench.PostAsync(clerk, "/api/stock/movements", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, type = "manual_out", quantity = 1, reasonCode = "other" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noComment.Status);
        Assert.Contains("comment", noComment.Raw, StringComparison.Ordinal);

        // Idempotence : la même requête réémise ne crée qu'un mouvement.
        var requestId = NewId();
        var first = await bench.PostAsync(clerk, "/api/stock/movements", new { requestId, variantId = variants[0], locationId = bench.LocationBe, type = "manual_out", quantity = 2, reasonCode = "breakage" });
        var replay = await bench.PostAsync(clerk, "/api/stock/movements", new { requestId, variantId = variants[0], locationId = bench.LocationBe, type = "manual_out", quantity = 2, reasonCode = "breakage" });
        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(first["id"].GetGuid(), replay["id"].GetGuid());
        Assert.Equal(8, (await BalanceAsync(bench, admin, variants[0], bench.LocationBe)).OnHand);
        Assert.Equal("request_id_reused", (await bench.PostAsync(clerk, "/api/stock/movements", new { requestId, variantId = variants[0], locationId = bench.LocationBe, type = "manual_out", quantity = 3, reasonCode = "breakage" })).Code);

        // Ajustement : quantité attendue périmée → 409 ; sinon écart calculé sous verrou.
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PostAsync(clerk, "/api/stock/adjustments", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, expectedOnHand = 8, countedQuantity = 7, reasonCode = "loss" })).Status);
        Assert.Equal("stock_changed", (await bench.PostAsync(admin, "/api/stock/adjustments", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, expectedOnHand = 9, countedQuantity = 7, reasonCode = "loss" })).Code);
        var adjusted = await bench.PostAsync(admin, "/api/stock/adjustments", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, expectedOnHand = 8, countedQuantity = 7, reasonCode = "loss" });
        Assert.True(adjusted.Status == HttpStatusCode.OK, adjusted.Raw);
        Assert.Equal(7, (await BalanceAsync(bench, admin, variants[0], bench.LocationBe)).OnHand);

        // Seuil et alertes ; invariant en main = Σ écarts.
        Assert.Equal(HttpStatusCode.OK, (await bench.PutAsync(admin, $"/api/stock/thresholds/{variants[0]}", new { requestId = NewId(), storeId = bench.StoreBe, lowStockThreshold = 7 })).Status);
        var alerts = await bench.GetAsync(clerk, $"/api/stock/alerts?storeId={bench.StoreBe}");
        Assert.Contains(alerts["items"].EnumerateArray(), a => a.GetProperty("variantId").GetGuid() == variants[0] && a.GetProperty("availability").GetString() == "low_stock");
        Assert.Equal(0L, await bench.ScalarAsync<long>(
            "SELECT count(*) FROM stock.balances b WHERE b.variant_id = @v AND b.on_hand <> (SELECT coalesce(sum(quantity_delta), 0) FROM stock.movements m WHERE m.variant_id = b.variant_id AND m.location_id = b.location_id)",
            ("v", variants[0])));

        // Politique et archivage refusés tant que du stock existe.
        var variantVersion = (await bench.GetAsync(admin, $"/api/catalog/variants/{variants[0]}?storeId={bench.StoreBe}"))["row"].GetProperty("version").GetUInt32();
        Assert.Equal("policy_change_refused", (await bench.PostAsync(admin, $"/api/catalog/variants/{variants[0]}/policy", new { requestId = NewId(), version = variantVersion, stockPolicy = "order_only" })).Code);
        Assert.Equal("archive_refused", (await bench.PostAsync(admin, $"/api/catalog/variants/{variants[0]}/archive", new { requestId = NewId(), version = variantVersion })).Code);

        // Journal en ajout seul, stock négatif refusé par la base elle-même.
        await Assert.ThrowsAsync<PostgresException>(() => bench.ExecuteAsync("UPDATE stock.movements SET quantity_delta = 1 WHERE variant_id = @v", ("v", variants[0])));
        await Assert.ThrowsAsync<PostgresException>(() => bench.ExecuteAsync("UPDATE stock.balances SET on_hand = -1 WHERE variant_id = @v", ("v", variants[0])));
        await Assert.ThrowsAsync<PostgresException>(() => bench.ExecuteAsync("DELETE FROM catalog.product_variants WHERE id = @v", ("v", variants[0])));
    }

    /// <summary>Deux postes sortent 3 pièces en même temps sur un stock de 5 : une seule sortie aboutit, jamais de stock négatif.</summary>
    [Phase05Fact]
    public async Task Stock_ConcurrentOutputs_NeverOversell()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var (_, variants) = await CreateModelAsync(bench, admin, "accessories", ($"CON-{bench.Suffix}", "stocked"));
        Assert.Equal(HttpStatusCode.Created, (await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, type = "manual_in", quantity = 5, reasonCode = "found" })).Status);

        for (var round = 0; round < 5; round++)
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, type = "manual_out", quantity = 3, reasonCode = "breakage" })));
            output.WriteLine($"tour {round} : {string.Join(", ", results.Select(r => $"{(int)r.Status} {r.Code}"))}");
            var onHand = (await BalanceAsync(bench, admin, variants[0], bench.LocationBe)).OnHand;
            if (round == 0)
            {
                Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Created));
                Assert.Equal(1, results.Count(r => r.Code == "stock_insufficient"));
                Assert.Equal(2, onHand);
            }
            else
            {
                Assert.All(results, r => Assert.Equal("stock_insufficient", r.Code));
            }

            Assert.True(onHand >= 0);
        }

        Assert.Equal(2L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE variant_id = @v", ("v", variants[0])));
    }

    [Phase05Fact]
    public async Task Inventory_CountValidate_ConcurrentMovement_Permissions_Blind()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var counter = await bench.UserAsync("compteur", Permissions.StockRead, Permissions.InventoryCount);
        var (_, variants) = await CreateModelAsync(bench, admin, "sunglasses", ($"INV-{bench.Suffix}-1", "stocked"), ($"INV-{bench.Suffix}-2", "stocked"));
        foreach (var v in variants)
        {
            Assert.Equal(HttpStatusCode.Created, (await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = v, locationId = bench.LocationBe, type = "manual_in", quantity = 10, reasonCode = "found" })).Status);
        }

        var opened = await bench.PostAsync(counter, "/api/inventories", new { requestId = NewId(), storeId = bench.StoreBe, locationIds = new[] { bench.LocationBe }, scopeType = "selection", variantIds = variants, blind = true });
        Assert.True(opened.Status == HttpStatusCode.Created, opened.Raw);
        var session = opened["id"].GetGuid();
        Assert.Equal("inventory_open", (await bench.PostAsync(admin, "/api/inventories", new { requestId = NewId(), storeId = bench.StoreBe, locationIds = new[] { bench.LocationBe }, scopeType = "selection", variantIds = new[] { variants[0] } })).Code);

        // À l'aveugle : aucune quantité théorique servie pendant la session.
        var blind = await bench.GetAsync(counter, $"/api/inventories/{session}");
        Assert.All(blind["lines"].EnumerateArray(), l => Assert.Equal(JsonValueKind.Null, l.GetProperty("expectedAtCount").ValueKind));

        uint LineVersion(ApiResult detail, Guid variant) => detail["lines"].EnumerateArray().Single(l => l.GetProperty("variantId").GetGuid() == variant).GetProperty("version").GetUInt32();
        var detail = await bench.GetAsync(counter, $"/api/inventories/{session}");
        Assert.Equal(HttpStatusCode.OK, (await bench.PutAsync(counter, $"/api/inventories/{session}/lines/{variants[0]}/{bench.LocationBe}", new { requestId = NewId(), version = LineVersion(detail, variants[0]), countedQuantity = 8 })).Status);
        Assert.Equal(HttpStatusCode.OK, (await bench.PutAsync(counter, $"/api/inventories/{session}/lines/{variants[1]}/{bench.LocationBe}", new { requestId = NewId(), version = LineVersion(detail, variants[1]), countedQuantity = 10 })).Status);
        // Deux postes comptent la même ligne avec la même version : le second est refusé (jamais d'écrasement).
        Assert.Equal(HttpStatusCode.Conflict, (await bench.PutAsync(counter, $"/api/inventories/{session}/lines/{variants[0]}/{bench.LocationBe}", new { requestId = NewId(), version = LineVersion(detail, variants[0]), countedQuantity = 9 })).Status);

        // Mouvement pendant le comptage : 1 sortie sur la variante 1.
        Assert.Equal(HttpStatusCode.Created, (await bench.PostAsync(admin, "/api/stock/movements", new { requestId = NewId(), variantId = variants[0], locationId = bench.LocationBe, type = "manual_out", quantity = 1, reasonCode = "breakage" })).Status);

        // Le compteur ne valide pas (permission distincte).
        var sessionVersion = (await bench.GetAsync(admin, $"/api/inventories/{session}"))["summary"].GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PostAsync(counter, $"/api/inventories/{session}/validate", new { requestId = NewId(), version = sessionVersion, uncounted = "exclude" })).Status);

        // Écart non justifié puis ligne modifiée depuis le comptage : refus, rien n'est appliqué.
        var changed = await bench.PostAsync(admin, $"/api/inventories/{session}/validate", new { requestId = NewId(), version = sessionVersion, uncounted = "exclude" });
        Assert.Equal("stock_changed", changed.Code);
        detail = await bench.GetAsync(admin, $"/api/inventories/{session}");
        Assert.Equal(HttpStatusCode.OK, (await bench.PutAsync(admin, $"/api/inventories/{session}/lines/{variants[0]}/{bench.LocationBe}/reason", new { requestId = NewId(), version = LineVersion(detail, variants[0]), reasonCode = "loss" })).Status);
        sessionVersion = (await bench.GetAsync(admin, $"/api/inventories/{session}"))["summary"].GetProperty("version").GetUInt32();
        var accepted = await bench.PostAsync(admin, $"/api/inventories/{session}/validate", new { requestId = NewId(), version = sessionVersion, uncounted = "exclude", acceptChangedLines = true });
        Assert.True(accepted.Status == HttpStatusCode.OK, accepted.Raw);

        // Écart (8 − 10 = −2) appliqué au stock COURANT (9) : 7 ; la sortie intervenue pendant le comptage est conservée.
        Assert.Equal(7, (await BalanceAsync(bench, admin, variants[0], bench.LocationBe)).OnHand);
        Assert.Equal(10, (await BalanceAsync(bench, admin, variants[1], bench.LocationBe)).OnHand);
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM stock.movements WHERE source_id = @s AND movement_type = 'inventory_adjustment'", ("s", session)));
        var closedVersion = (await bench.GetAsync(admin, $"/api/inventories/{session}"))["summary"].GetProperty("version").GetUInt32();
        Assert.Equal("inventory_closed", (await bench.PostAsync(admin, $"/api/inventories/{session}/validate", new { requestId = NewId(), version = closedVersion, uncounted = "exclude" })).Code);
        await Assert.ThrowsAsync<PostgresException>(() => bench.ExecuteAsync("UPDATE stock.inventory_sessions SET blind = false WHERE id = @s", ("s", session)));
    }

    [Phase05Fact]
    public async Task Import_CheckApply_AllOrNothing_Replay_Permissions()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("tout", Everything);
        var importer = await bench.UserAsync("import", Permissions.CatalogImport, Permissions.CatalogRead);

        var suppliersCsv = $"code;nom;pays\nIMP{bench.Suffix}A;Fournisseur import A;BE\nIMP{bench.Suffix}B;Fournisseur import B;MA\n";
        var upload = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "suppliers", fileName = "fournisseurs.csv", content = suppliersCsv });
        Assert.True(upload.Status == HttpStatusCode.Created, upload.Raw);
        var batch = upload["batchId"].GetGuid();
        Assert.Equal(3, upload["suggestedMapping"].EnumerateObject().Count());
        var report = await bench.GetAsync(importer, $"/api/imports/{batch}");
        var check = await bench.PostAsync(importer, $"/api/imports/{batch}/check", new { requestId = NewId(), version = report["batch"].GetProperty("version").GetUInt32() });
        Assert.True(check.Status == HttpStatusCode.OK, check.Raw);
        Assert.Equal(2, check["create"].GetInt32());
        // Contrôle à blanc : aucune écriture au catalogue.
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.suppliers WHERE code LIKE @c", ("c", $"IMP{bench.Suffix}%")));
        var version = (await bench.GetAsync(importer, $"/api/imports/{batch}"))["batch"].GetProperty("version").GetUInt32();
        var apply = await bench.PostAsync(importer, $"/api/imports/{batch}/apply", new { requestId = NewId(), version });
        Assert.True(apply.Status == HttpStatusCode.OK, apply.Raw);
        Assert.Equal(2L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.suppliers WHERE code LIKE @c", ("c", $"IMP{bench.Suffix}%")));

        // Même fichier réimporté : refusé à l'application (aucun doublon).
        var again = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "suppliers", fileName = "fournisseurs.csv", content = suppliersCsv });
        var againBatch = again["batchId"].GetGuid();
        var againVersion = (await bench.GetAsync(importer, $"/api/imports/{againBatch}"))["batch"].GetProperty("version").GetUInt32();
        var againCheck = await bench.PostAsync(importer, $"/api/imports/{againBatch}/check", new { requestId = NewId(), version = againVersion });
        Assert.Equal(2, againCheck["unchanged"].GetInt32());
        againVersion = (await bench.GetAsync(importer, $"/api/imports/{againBatch}"))["batch"].GetProperty("version").GetUInt32();
        Assert.Equal("import_already_applied", (await bench.PostAsync(importer, $"/api/imports/{againBatch}/apply", new { requestId = NewId(), version = againVersion })).Code);

        // Catalogue : une ligne en erreur → rien n'est appliqué (tout ou rien) ; formule refusée.
        var catalogCsv = $"famille;marque;modele;variante;sku;politique;codefournisseur;principal\n"
            + $"optical_frames;Marque;Import {bench.Suffix};Noir 52;IMPSKU-{bench.Suffix}-1;stocked;IMP{bench.Suffix}A;oui\n"
            + $"optical_frames;Marque;Import {bench.Suffix};=CMD();IMPSKU-{bench.Suffix}-2;stocked;;\n";
        var catalog = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "catalog", fileName = "catalogue.csv", content = catalogCsv });
        var catalogBatch = catalog["batchId"].GetGuid();
        var cv = (await bench.GetAsync(importer, $"/api/imports/{catalogBatch}"))["batch"].GetProperty("version").GetUInt32();
        var catalogCheck = await bench.PostAsync(importer, $"/api/imports/{catalogBatch}/check", new { requestId = NewId(), version = cv });
        Assert.Equal(1, catalogCheck["error"].GetInt32());
        cv = (await bench.GetAsync(importer, $"/api/imports/{catalogBatch}"))["batch"].GetProperty("version").GetUInt32();
        Assert.Equal("import_has_errors", (await bench.PostAsync(importer, $"/api/imports/{catalogBatch}/apply", new { requestId = NewId(), version = cv })).Code);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_variants WHERE sku LIKE @s", ("s", $"IMPSKU-{bench.Suffix}%")));

        var fixedCsv = catalogCsv.Replace("=CMD()", "Écaille 52", StringComparison.Ordinal);
        var fixedUpload = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "catalog", fileName = "catalogue.csv", content = fixedCsv });
        var fixedBatch = fixedUpload["batchId"].GetGuid();
        var fv = (await bench.GetAsync(importer, $"/api/imports/{fixedBatch}"))["batch"].GetProperty("version").GetUInt32();
        Assert.Equal(0, (await bench.PostAsync(importer, $"/api/imports/{fixedBatch}/check", new { requestId = NewId(), version = fv }))["error"].GetInt32());
        fv = (await bench.GetAsync(importer, $"/api/imports/{fixedBatch}"))["batch"].GetProperty("version").GetUInt32();
        var applyRequest = NewId();
        Assert.Equal(HttpStatusCode.OK, (await bench.PostAsync(importer, $"/api/imports/{fixedBatch}/apply", new { requestId = applyRequest, version = fv })).Status);
        // Réémission de la même application : état courant, aucun doublon.
        Assert.Equal(HttpStatusCode.OK, (await bench.PostAsync(importer, $"/api/imports/{fixedBatch}/apply", new { requestId = applyRequest, version = fv })).Status);
        Assert.Equal(2L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_variants WHERE sku LIKE @s", ("s", $"IMPSKU-{bench.Suffix}%")));
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(DISTINCT model_id) FROM catalog.product_variants WHERE sku LIKE @s", ("s", $"IMPSKU-{bench.Suffix}%")));

        // Prix : coûts → costs.write exigé à l'application ; devise différente du magasin → erreur, aucune conversion.
        var pricesCsv = $"sku;prixachatht;coefficient;devise\nIMPSKU-{bench.Suffix}-1;20,00;2.5;EUR\n";
        var prices = await bench.PostMaAsync(importer, "/api/imports", new { requestId = NewId(), kind = "prices", storeId = bench.StoreMa, fileName = "prix.csv", content = pricesCsv });
        var pb = prices["batchId"].GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await bench.GetAsync(importer, $"/api/imports/{pb}")).Status); // lot d'un autre magasin : invisible
        var pv = (await bench.GetMaAsync(importer, $"/api/imports/{pb}"))["batch"].GetProperty("version").GetUInt32();
        var pricesCheck = await bench.PostMaAsync(importer, $"/api/imports/{pb}/check", new { requestId = NewId(), version = pv });
        Assert.Equal(1, pricesCheck["error"].GetInt32());
        var pricesReport = await bench.GetMaAsync(importer, $"/api/imports/{pb}");
        Assert.False(pricesReport["valuesVisible"].GetBoolean());
        Assert.Contains("currency_mismatch", pricesReport.Raw, StringComparison.Ordinal);
        Assert.Contains("purchasePriceHt", pricesReport.Raw, StringComparison.Ordinal);

        var eurCsv = $"sku;prixachatht;coefficient\nIMPSKU-{bench.Suffix}-1;20.00;2.5\n";
        var eur = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "prices", storeId = bench.StoreBe, fileName = "prix.csv", content = eurCsv });
        var eb = eur["batchId"].GetGuid();
        var ev = (await bench.GetAsync(importer, $"/api/imports/{eb}"))["batch"].GetProperty("version").GetUInt32();
        Assert.Equal(1, (await bench.PostAsync(importer, $"/api/imports/{eb}/check", new { requestId = NewId(), version = ev }))["create"].GetInt32());
        ev = (await bench.GetAsync(importer, $"/api/imports/{eb}"))["batch"].GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PostAsync(importer, $"/api/imports/{eb}/apply", new { requestId = NewId(), version = ev })).Status);
        Assert.Equal(HttpStatusCode.OK, (await bench.PostAsync(admin, $"/api/imports/{eb}/apply", new { requestId = NewId(), version = ev })).Status);
        Assert.Equal(50.00m, await bench.ScalarAsync<decimal>(
            "SELECT p.sale_price_ht FROM pricing.variant_store_prices p JOIN catalog.product_variants v ON v.id = p.variant_id WHERE v.sku = @s AND p.store_id = @st",
            ("s", $"IMPSKU-{bench.Suffix}-1"), ("st", bench.StoreBe)));
    }

    [Phase05Fact]
    public async Task Permissions_EachRoute_RequiresItsOwnRight_AndSystemAdminGivesNone()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var none = await bench.UserAsync("aucun");
        var sysadmin = await bench.UserAsync("admin", Permissions.SystemAdmin);
        foreach (var user in new[] { none, sysadmin })
        {
            foreach (var path in new[]
            {
                $"/api/stores", $"/api/catalog/variants?storeId={bench.StoreBe}", "/api/suppliers", $"/api/stock/balances?storeId={bench.StoreBe}",
                "/api/inventories", "/api/imports",
            })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await bench.GetAsync(user, path)).Status);
            }
        }

        var stockOnly = await bench.UserAsync("stock", Permissions.StockRead);
        Assert.Equal(HttpStatusCode.OK, (await bench.GetAsync(stockOnly, "/api/stores")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.GetAsync(stockOnly, $"/api/catalog/variants?storeId={bench.StoreBe}")).Status);
        Assert.Equal(HttpStatusCode.OK, (await bench.GetAsync(stockOnly, $"/api/stock/balances?storeId={bench.StoreBe}")).Status);
    }
}
