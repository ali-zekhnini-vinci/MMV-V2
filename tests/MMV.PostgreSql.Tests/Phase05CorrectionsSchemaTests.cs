using System.Net;
using MMV.Domain.Identity;
using Xunit.Abstractions;

#pragma warning disable CA1861 // tableaux littéraux des corps de requête de test

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Fonctions de la migration 0008 (COR-05-04 import CSV, COR-05-05 référentiel verres) sur PostgreSQL réel. NOT_TESTED
/// tant que 0008 n'est pas appliquée à la cible : seul le cluster JETABLE l'a reçue avant la revue SQL du propriétaire.
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05CorrectionsSchemaTests(ITestOutputHelper output)
{
    private static Guid NewId() => Guid.NewGuid();

    private static async Task<Guid> Reference(Phase05Bench bench, string user, string kind, object body)
    {
        var created = await bench.PostAsync(user, $"/api/catalog/lens-references/{kind}", body);
        Assert.True(created.Status == HttpStatusCode.Created, $"{kind} : {created.Raw}");
        return created["id"].GetGuid();
    }

    [Phase05CorrectionsFact]
    public async Task LensOffers_SimpleAndAdvancedRules_ThreeOutcomes_Revisions_Costs()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var s = bench.Suffix;
        var admin = await bench.UserAsync("admin", Permissions.CatalogRead, Permissions.CatalogConfigure, Permissions.SuppliersRead, Permissions.SuppliersWrite,
            Permissions.CostsRead, Permissions.CostsWrite);
        var configurator = await bench.UserAsync("config", Permissions.CatalogRead, Permissions.CatalogConfigure, Permissions.SuppliersRead);
        var reader = await bench.UserAsync("lecteur", Permissions.CatalogRead);
        var manufacturer = await Reference(bench, admin, "manufacturers", new { requestId = NewId(), name = $"Fabricant verres {s}" });
        var range = await Reference(bench, admin, "ranges", new { requestId = NewId(), name = $"Gamme progressive {s}", manufacturerId = manufacturer });
        var index = await Reference(bench, admin, "indices", new { requestId = NewId(), name = $"Indice {s}", indexValue = "1.6" });
        var blue = await Reference(bench, admin, "treatments", new { requestId = NewId(), name = $"Anti-lumière bleue {s}" });
        var ar = await Reference(bench, admin, "treatments", new { requestId = NewId(), name = $"Antireflet {s}" });
        var supplier = (await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"LAB{s}", name = $"Laboratoire {s}", countryCode = "BE" }))["id"].GetGuid();

        // Fiche SIMPLE, incomplète : plage SPH seule (exemple FICTIF du mandat, anti-lumière bleue −10,00 à +5,00).
        var create = await bench.PostAsync(admin, "/api/catalog/lens-offers", new
        {
            requestId = NewId(), rangeId = range, supplierId = supplier, lensType = "single_vision", indexId = index, production = "on_order", leadTimeDays = 7,
            priceHt = "42.5000", priceCurrency = "EUR", diameters = new[] { 65, 70 }, sphereMin = "-10.00", sphereMax = "+5.00", treatmentIds = new[] { blue, ar },
            rulesComplete = false, sourceReference = $"Fiche technique fictive {s}", reason = "Création",
        });
        Assert.True(create.Status == HttpStatusCode.Created, create.Raw);
        var offer = create["id"].GetGuid();

        async Task<ApiResult> Check(string user, object body) => await bench.PostAsync(user, $"/api/catalog/lens-offers/{offer}/check", body);
        var inRange = await Check(reader, new { sphere = "-6.00", treatmentIds = new[] { blue } });
        Assert.Equal("to_verify", inRange["status"].GetString()); // dans la plage mais fiche incomplète : jamais « garanti »
        Assert.Contains("rules_incomplete", inRange.Raw, StringComparison.Ordinal);
        Assert.Contains("ni validation clinique", inRange["disclaimer"].GetString(), StringComparison.Ordinal);
        foreach (var outside in new[] { "-11.00", "+6.00" })
        {
            var refused = await Check(reader, new { sphere = outside, treatmentIds = new[] { blue } });
            Assert.Equal("incompatible", refused["status"].GetString());
            Assert.Contains("sphere_out_of_range", refused.Raw, StringComparison.Ordinal);
        }

        Assert.Equal("incompatible", (await Check(reader, new { sphere = "-2.00", diameter = 60 }))["status"].GetString());
        Assert.Equal("to_verify", (await Check(reader, new { cylinder = "-1.00" }))["status"].GetString()); // SPH manquante

        // Coûts : lecteur sans costs.read ⇒ aucun prix servi ; configurateur sans costs.write ⇒ modification de prix refusée.
        var forReader = await bench.GetAsync(reader, $"/api/catalog/lens-offers/{offer}");
        Assert.False(forReader["offer"].TryGetProperty("priceHt", out _));
        Assert.Equal("42.5000", (await bench.GetAsync(admin, $"/api/catalog/lens-offers/{offer}"))["offer"].GetProperty("priceHt").GetString());
        var version = (await bench.GetAsync(admin, $"/api/catalog/lens-offers/{offer}"))["offer"].GetProperty("version").GetUInt32();
        var noCost = await bench.PutAsync(configurator, $"/api/catalog/lens-offers/{offer}", new
        {
            requestId = NewId(), version, lensType = "single_vision", production = "on_order", priceHt = "10", priceCurrency = "EUR", sphereMin = "-10", sphereMax = "5",
        });
        Assert.Equal(HttpStatusCode.Forbidden, noCost.Status);

        // Mode AVANCÉ : fiche déclarée complète, plages CYL, fabrication spéciale −12/−10 avec supplément, restriction structurée.
        var advanced = await bench.PutAsync(admin, $"/api/catalog/lens-offers/{offer}", new
        {
            requestId = NewId(), version, lensType = "single_vision", indexId = index, production = "on_order", leadTimeDays = 7, priceHt = "42.5000", priceCurrency = "EUR",
            diameters = new[] { 65, 70 }, sphereMin = "-10.00", sphereMax = "+5.00", cylinderMin = "-4.00", cylinderMax = "0.00", treatmentIds = new[] { blue, ar },
            rulesComplete = true, sourceReference = $"Fiche technique fictive {s} v2", validFrom = "2026-01-01", validUntil = "2027-12-31", reason = "Fiche complète reçue",
            rules = new object[]
            {
                new { kind = "sphere_range", effect = "allow", manufacturing = "special", range = new { min = "-12.00", max = "-10.25" }, surchargeHt = "15.0000", extraLeadDays = 5 },
                new { kind = "combination_limit", effect = "deny", manufacturing = "standard", maxSphereCylinderSum = "12.00" },
                new { kind = "restriction", effect = "deny", manufacturing = "standard", conditions = new { treatmentIds = new[] { blue }, cylinder = new { min = "-4.00", max = "-3.25" } }, note = "Pas d'anti-lumière bleue au-delà de −3,00 de cylindre" },
            },
        });
        Assert.True(advanced.Status == HttpStatusCode.OK, advanced.Raw);
        Assert.Equal("compatible", (await Check(reader, new { sphere = "-6.00", cylinder = "-1.00", treatmentIds = new[] { blue } }))["status"].GetString());
        var special = await Check(admin, new { sphere = "-11.00", cylinder = "-0.50" });
        Assert.Equal("compatible", special["status"].GetString());
        Assert.Equal("special", special["manufacturing"].GetString());
        Assert.Equal("15.0000", special["surchargeHt"].GetString());
        Assert.Equal(5, special["extraLeadDays"].GetInt32());
        Assert.False((await Check(reader, new { sphere = "-11.00" })).Body.TryGetProperty("surchargeHt", out _));
        Assert.Equal("incompatible", (await Check(reader, new { sphere = "-10.00", cylinder = "-3.00" }))["status"].GetString()); // |−10| + |−3| > 12
        var restricted = await Check(reader, new { sphere = "-2.00", cylinder = "-3.50", treatmentIds = new[] { blue } });
        Assert.Equal("incompatible", restricted["status"].GetString());
        Assert.Contains("restriction", restricted.Raw, StringComparison.Ordinal);

        // Règle non structurée ou expression libre : refusée (membres inconnus, type inconnu).
        version = (await bench.GetAsync(admin, $"/api/catalog/lens-offers/{offer}"))["offer"].GetProperty("version").GetUInt32();
        var arbitrary = await bench.PutAsync(admin, $"/api/catalog/lens-offers/{offer}", new
        {
            requestId = NewId(), version, lensType = "single_vision", production = "on_order", rules = new object[] { new { kind = "expression", effect = "allow", formula = "sph > -10" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, arbitrary.Status);
        var unknownKind = await bench.PutAsync(admin, $"/api/catalog/lens-offers/{offer}", new
        {
            requestId = NewId(), version, lensType = "single_vision", production = "on_order", rules = new object[] { new { kind = "expression", effect = "allow" } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownKind.Status);

        // Historique : deux révisions, append-only en base (UPDATE refusé par déclencheur).
        var detail = await bench.GetAsync(reader, $"/api/catalog/lens-offers/{offer}");
        Assert.Equal(2, detail["offer"].GetProperty("revision").GetInt32());
        Assert.Equal(2, detail["revisions"].GetArrayLength());
        Assert.Equal("Fiche complète reçue", detail["revisions"][0].GetProperty("reason").GetString());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => bench.ExecuteAsync("UPDATE catalog.lens_offer_revisions SET reason = 'x' WHERE offer_id = @o", ("o", offer)));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => bench.ExecuteAsync("DELETE FROM catalog.lens_offers WHERE id = @o", ("o", offer)));
        Assert.Single((await bench.GetAsync(reader, $"/api/catalog/lens-offers?rangeId={range}")).Body.EnumerateArray());
    }

    [Phase05CorrectionsFact]
    public async Task CsvImport_DetectsFormats_MapsSynonyms_SupplierProfile_NoDuplicates()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var s = bench.Suffix;
        var admin = await bench.UserAsync("admin", Permissions.CatalogRead, Permissions.CatalogImport, Permissions.CatalogWrite, Permissions.SuppliersRead, Permissions.SuppliersWrite,
            Permissions.CostsRead, Permissions.CostsWrite, Permissions.PricingWrite);
        var importer = await bench.UserAsync("importeur", Permissions.CatalogRead, Permissions.CatalogImport, Permissions.SuppliersRead);
        var supplier = (await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"FRN{s}", name = $"Fournisseur CSV {s}", countryCode = "BE" }))["id"].GetGuid();

        // Fichier fournisseur « réel » : tabulation, intitulés libres, références à zéros initiaux, aucun SKU, aucune famille.
        var catalogCsv = $"Marque\tModèle\tCouleur\tRef\tEAN\tPA HT\n"
            + $"Banc{s}\tAlpha {s}\tNoir\t000123\t\t12,50\n"
            + $"Banc{s}\tAlpha {s}\tÉcaille\t000124\t\t12,50\n"
            + $"Banc{s}\tBeta {s}\tBleu\t0045-B\t\t1 234,00\n";
        var upload = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "catalog", supplierId = supplier, fileName = "catalogue.txt", content = catalogCsv });
        Assert.True(upload.Status == HttpStatusCode.Created, upload.Raw);
        Assert.Equal("tab", upload["delimiter"].GetString());
        var mapping = upload["suggestedMapping"];
        Assert.Equal("Marque", mapping.GetProperty("brand").GetString());
        Assert.Equal("Modèle", mapping.GetProperty("modelName").GetString());
        Assert.Equal("Couleur", mapping.GetProperty("color").GetString());
        Assert.Equal("Ref", mapping.GetProperty("supplierReference").GetString()); // contexte fournisseur : « Ref » = référence fournisseur
        Assert.Equal("EAN", mapping.GetProperty("barcode").GetString());
        Assert.Contains(upload["suggestions"].EnumerateArray(), x => x.GetProperty("field").GetString() == "supplierReference" && x.GetProperty("confidence").GetString() == "probable");
        var batch = upload["batchId"].GetGuid();

        // Colonnes obligatoires absentes (famille, libellé, politique) : refus tant qu'aucune valeur par défaut n'est choisie.
        var version = (await bench.GetAsync(importer, $"/api/imports/{batch}"))["batch"].GetProperty("version").GetUInt32();
        var missing = await bench.PostAsync(importer, $"/api/imports/{batch}/check", new { requestId = NewId(), version, mapping = new Dictionary<string, string>
        {
            ["brand"] = "Marque", ["modelName"] = "Modèle", ["variantLabel"] = "Couleur", ["color"] = "Couleur", ["supplierReference"] = "Ref",
        } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.Status);
        Assert.Contains("mapping.familyCode", missing.Raw, StringComparison.Ordinal);
        var userMapping = new Dictionary<string, string>
        {
            ["brand"] = "Marque", ["modelName"] = "Modèle", ["variantLabel"] = "Couleur", ["color"] = "Couleur", ["supplierReference"] = "Ref",
            ["@familyCode"] = "optical_frames", ["@stockPolicy"] = "order_only",
        };
        var checkedOk = await bench.PostAsync(importer, $"/api/imports/{batch}/check", new { requestId = NewId(), version, mapping = userMapping });
        Assert.True(checkedOk.Status == HttpStatusCode.OK, checkedOk.Raw);
        Assert.Equal(3, checkedOk["create"].GetInt32());
        Assert.Equal(0, checkedOk["error"].GetInt32());
        version = (await bench.GetAsync(importer, $"/api/imports/{batch}"))["batch"].GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.OK, (await bench.PostAsync(importer, $"/api/imports/{batch}/apply", new { requestId = NewId(), version })).Status);
        Assert.Equal(3L, await bench.ScalarAsync<long>(
            "SELECT count(*) FROM catalog.variant_suppliers l JOIN catalog.product_variants v ON v.id = l.variant_id WHERE l.supplier_id = @s AND v.sku LIKE 'MON-%'", ("s", supplier)));
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.variant_suppliers WHERE supplier_id = @s AND supplier_reference = '000123'", ("s", supplier)));
        Assert.Equal(2L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_models WHERE brand = @b", ("b", $"Banc{s}")));

        // Modèle fournisseur enregistré puis reproposé pour un nouveau fichier du même fournisseur.
        var saveProfile = await bench.PutAsync(importer, $"/api/imports/profiles/{supplier}/catalog", new { requestId = NewId(), mapping = userMapping, headers = new[] { "Marque", "Modèle", "Couleur", "Ref", "EAN", "PA HT" } });
        Assert.True(saveProfile.Status == HttpStatusCode.OK, saveProfile.Raw);

        // Réémission corrigée du même fournisseur (nouvelle ligne, une ligne modifiée) : AUCUN doublon (rapprochement par référence).
        var second = catalogCsv.Replace("Écaille", "Écaille claire", StringComparison.Ordinal) + $"Banc{s}\tBeta {s}\tRouge\t0045-R\t\t9,90\n";
        var upload2 = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "catalog", supplierId = supplier, fileName = "catalogue-v2.txt", content = second });
        Assert.True(upload2["profileApplied"].GetBoolean());
        Assert.Equal("optical_frames", upload2["suggestedMapping"].GetProperty("@familyCode").GetString());
        var batch2 = upload2["batchId"].GetGuid();
        var v2 = (await bench.GetAsync(importer, $"/api/imports/{batch2}"))["batch"].GetProperty("version").GetUInt32();
        var check2 = await bench.PostAsync(importer, $"/api/imports/{batch2}/check", new { requestId = NewId(), version = v2 });
        Assert.True(check2.Status == HttpStatusCode.OK, check2.Raw);
        Assert.Equal(1, check2["create"].GetInt32());
        Assert.Equal(1, check2["update"].GetInt32());
        Assert.Equal(2, check2["unchanged"].GetInt32());

        // Tarifs du même fichier, rapprochés par référence fournisseur : virgule décimale détectée, milliers, devise du magasin.
        var prices = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "prices", supplierId = supplier, fileName = "tarifs.txt", content = catalogCsv });
        Assert.Equal(",", prices["decimalSeparator"].GetString());
        Assert.Equal("Ref", prices["suggestedMapping"].GetProperty("supplierReference").GetString());
        Assert.Equal("PA HT", prices["suggestedMapping"].GetProperty("purchasePriceHt").GetString());
        var pb = prices["batchId"].GetGuid();
        var pv = (await bench.GetAsync(importer, $"/api/imports/{pb}"))["batch"].GetProperty("version").GetUInt32();
        var priceCheck = await bench.PostAsync(importer, $"/api/imports/{pb}/check", new { requestId = NewId(), version = pv });
        Assert.True(priceCheck.Status == HttpStatusCode.OK, priceCheck.Raw);
        Assert.Equal(0, priceCheck["error"].GetInt32());
        pv = (await bench.GetAsync(importer, $"/api/imports/{pb}"))["batch"].GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PostAsync(importer, $"/api/imports/{pb}/apply", new { requestId = NewId(), version = pv })).Status); // coûts : costs.write
        Assert.Equal(HttpStatusCode.OK, (await bench.PostAsync(admin, $"/api/imports/{pb}/apply", new { requestId = NewId(), version = pv })).Status);
        Assert.Equal(1234.00m, await bench.ScalarAsync<decimal>(
            "SELECT p.purchase_price_ht FROM pricing.variant_store_prices p JOIN catalog.variant_suppliers l ON l.variant_id = p.variant_id WHERE l.supplier_reference = '0045-B' AND p.store_id = @st",
            ("st", bench.StoreBe)));

        // Montant AMBIGU (« 1,234 » : milliers ou décimales ?) : signalé tant que le séparateur n'est pas confirmé ; devise étrangère refusée.
        var ambiguousCsv = "Ref;PA HT\n000123;1,234\n000124;USD 10\n";
        var amb = await bench.PostAsync(importer, "/api/imports", new { requestId = NewId(), kind = "prices", supplierId = supplier, fileName = "ambigu.csv", content = ambiguousCsv });
        Assert.True(amb["decimalAmbiguous"].GetBoolean(), amb.Raw);
        var ab = amb["batchId"].GetGuid();
        var av = (await bench.GetAsync(importer, $"/api/imports/{ab}"))["batch"].GetProperty("version").GetUInt32();
        var ambCheck = await bench.PostAsync(importer, $"/api/imports/{ab}/check", new { requestId = NewId(), version = av, mapping = new Dictionary<string, string> { ["supplierReference"] = "Ref", ["purchasePriceHt"] = "PA HT" } });
        Assert.Equal(2, ambCheck["error"].GetInt32());
        var ambReport = await bench.GetAsync(admin, $"/api/imports/{ab}");
        Assert.Contains("ambiguous_number", ambReport.Raw, StringComparison.Ordinal);
        Assert.Contains("currency_mismatch", ambReport.Raw, StringComparison.Ordinal);
        av = ambReport["batch"].GetProperty("version").GetUInt32();
        var confirmed = await bench.PostAsync(importer, $"/api/imports/{ab}/check", new { requestId = NewId(), version = av, mapping = new Dictionary<string, string> { ["supplierReference"] = "Ref", ["purchasePriceHt"] = "PA HT", ["@decimal"] = "," } });
        Assert.Equal(1, confirmed["error"].GetInt32()); // reste : devise USD (aucune conversion)

        // Droits : modèle d'import refusé sans catalog.import.
        var noImport = await bench.UserAsync("sansimport", Permissions.CatalogRead, Permissions.SuppliersRead);
        Assert.Equal(HttpStatusCode.Forbidden, (await bench.PutAsync(noImport, $"/api/imports/profiles/{supplier}/catalog", new { requestId = NewId(), mapping = userMapping, headers = new[] { "Marque" } })).Status);
    }
}
