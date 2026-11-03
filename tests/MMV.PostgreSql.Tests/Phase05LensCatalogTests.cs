using System.Net;
using MMV.Domain.Identity;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// UX-IA — catalogue des verres unifié (<c>POST /api/catalog/lens-catalog</c>) : une gamme et les offres de plusieurs
/// fournisseurs créées en une transaction, plages PROPRES à chaque fournisseur (exemple du mandat : anti-lumière bleue
/// −10 à +5 chez l'un, autre plage chez l'autre), restrictions conditionnelles en mode avancé, tout ou rien, coûts
/// protégés, réémission sans doublon, historique des révisions conservé. Entités séparées en base (références / offres).
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05LensCatalogTests(ITestOutputHelper output)
{
    private static Guid NewId() => Guid.NewGuid();

    private static async Task<Guid> Reference(Phase05Bench bench, string user, string kind, object body)
    {
        var created = await bench.PostAsync(user, $"/api/catalog/lens-references/{kind}", body);
        Assert.True(created.Status == HttpStatusCode.Created, $"{kind} : {created.Raw}");
        return created["id"].GetGuid();
    }

    [Phase05CorrectionsFact]
    public async Task NewRange_WithSeveralSuppliers_OwnRangesPerSupplier_AllOrNothing_History()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var s = bench.Suffix;
        var admin = await bench.UserAsync("admin", Permissions.CatalogRead, Permissions.CatalogConfigure, Permissions.SuppliersRead, Permissions.SuppliersWrite,
            Permissions.CostsRead, Permissions.CostsWrite);
        var configurator = await bench.UserAsync("config", Permissions.CatalogRead, Permissions.CatalogConfigure, Permissions.SuppliersRead);
        var reader = await bench.UserAsync("lecteur", Permissions.CatalogRead);
        var index = await Reference(bench, admin, "indices", new { requestId = NewId(), name = $"Indice {s}", indexValue = "1.6" });
        var blue = await Reference(bench, admin, "treatments", new { requestId = NewId(), name = $"Anti-lumière bleue {s}" });
        var labA = (await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"LA{s}", name = $"Laboratoire A {s}", countryCode = "BE" }))["id"].GetGuid();
        var labB = (await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"LB{s}", name = $"Laboratoire B {s}", countryCode = "FR" }))["id"].GetGuid();

        // Caractéristiques communes (type, indice, traitements) recopiées par le parcours ; plages, prix et délais par fournisseur.
        object Offer(Guid supplier, string sphereMin, string sphereMax, string? price = null, object[]? rules = null, bool complete = false) => new
        {
            supplierId = supplier, lensType = "single_vision", indexId = index, treatmentIds = new[] { blue }, production = "on_order", leadTimeDays = 6,
            sphereMin, sphereMax, cylinderMin = complete ? "-4.00" : null, cylinderMax = complete ? "0.00" : null,
            priceHt = price, priceCurrency = price is null ? null : "EUR", rules = rules ?? [], rulesComplete = complete,
        };
        var advancedRules = new object[]
        {
            new { kind = "restriction", effect = "deny", manufacturing = "standard", conditions = new { treatmentIds = new[] { blue }, cylinder = new { min = "-4.00", max = "-2.25" } }, note = "Pas d'anti-lumière bleue au-delà de −2,00 de cylindre" },
        };

        // Tout ou rien : la seconde offre est invalide (SPH min > max) → ni fabricant, ni gamme, ni offre.
        var broken = await bench.PostAsync(admin, "/api/catalog/lens-catalog", new
        {
            requestId = NewId(), manufacturerName = $"Fabricant cassé {s}", rangeName = $"Gamme cassée {s}",
            offers = new[] { Offer(labA, "-10.00", "+5.00"), Offer(labB, "+4.00", "-8.00") },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, broken.Status);
        Assert.Contains("offers[1].", broken.Raw, StringComparison.Ordinal);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.lens_references WHERE name IN (@m, @r)", ("m", $"Fabricant cassé {s}"), ("r", $"Gamme cassée {s}")));

        // Coûts sans costs.write : refus AVANT écriture.
        var noCost = await bench.PostAsync(configurator, "/api/catalog/lens-catalog", new
        {
            requestId = NewId(), manufacturerName = $"Fabricant coût {s}", rangeName = $"Gamme coût {s}", offers = new[] { Offer(labA, "-10.00", "+5.00", "40.00") },
        });
        Assert.Equal(HttpStatusCode.Forbidden, noCost.Status);
        Assert.Equal("costs_forbidden", noCost.Code);
        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.lens_references WHERE name = @m", ("m", $"Fabricant coût {s}")));

        // Succès : nouveau fabricant + nouvelle gamme + deux fournisseurs aux plages différentes (mode simple / mode avancé).
        var requestId = NewId();
        object Body() => new
        {
            requestId, manufacturerName = $"Fabricant verres {s}", rangeName = $"Gamme bleue {s}",
            offers = new[] { Offer(labA, "-10.00", "+5.00", "42.5000"), Offer(labB, "-6.00", "+4.00", "39.0000", advancedRules, complete: true) },
        };
        var created = await bench.PostAsync(admin, "/api/catalog/lens-catalog", Body());
        Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
        var range = created["rangeId"].GetGuid();
        var offerIds = created["offerIds"].EnumerateArray().Select(e => e.GetGuid()).ToList();
        Assert.Equal(2, offerIds.Count);

        // Séparation en base conservée : 2 références (fabricant, gamme) + 2 offres distinctes rattachées à la gamme.
        Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.lens_references WHERE id = @r AND manufacturer_id = @m", ("r", range), ("m", created["manufacturerId"].GetGuid())));
        Assert.Equal(2L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.lens_offers WHERE range_id = @r", ("r", range)));

        // Réémission : même résultat, aucun doublon.
        var replay = await bench.PostAsync(admin, "/api/catalog/lens-catalog", Body());
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.True(replay["replayed"].GetBoolean());
        Assert.Equal(2L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.lens_offers WHERE range_id = @r", ("r", range)));

        // Même correction, issues différentes selon le fournisseur : −8,00 dans la plage de A (fiche incomplète : « à vérifier »), hors plage chez B.
        async Task<string> Status(Guid offer, object body) => (await bench.PostAsync(reader, $"/api/catalog/lens-offers/{offer}/check", body))["status"].GetString()!;
        Assert.Equal("to_verify", await Status(offerIds[0], new { sphere = "-8.00", treatmentIds = new[] { blue } }));
        Assert.Equal("incompatible", await Status(offerIds[1], new { sphere = "-8.00", treatmentIds = new[] { blue } }));
        // Restriction conditionnelle de B (mode avancé, fiche complète) : anti-lumière bleue refusée au-delà de −2,00 de cylindre.
        Assert.Equal("compatible", await Status(offerIds[1], new { sphere = "-2.00", cylinder = "-1.00", treatmentIds = new[] { blue } }));
        Assert.Equal("incompatible", await Status(offerIds[1], new { sphere = "-2.00", cylinder = "-3.00", treatmentIds = new[] { blue } }));

        // Gamme EXISTANTE : ajout d'un troisième fournisseur ; fournisseur en double refusé.
        var labC = (await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"LC{s}", name = $"Laboratoire C {s}", countryCode = "BE" }))["id"].GetGuid();
        var duplicate = await bench.PostAsync(admin, "/api/catalog/lens-catalog", new { requestId = NewId(), rangeId = range, offers = new[] { Offer(labC, "-4", "4"), Offer(labC, "-2", "2") } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.Status);
        var third = await bench.PostAsync(admin, "/api/catalog/lens-catalog", new { requestId = NewId(), rangeId = range, offers = new[] { Offer(labC, "-4.00", "+4.00") } });
        Assert.True(third.Status == HttpStatusCode.Created, third.Raw);
        Assert.Equal(3, (await bench.GetAsync(reader, $"/api/catalog/lens-offers?rangeId={range}")).Body.GetArrayLength());

        // Historique conservé : révision n° 1 à la création, n° 2 après modification de l'offre A.
        var version = (await bench.GetAsync(admin, $"/api/catalog/lens-offers/{offerIds[0]}"))["offer"].GetProperty("version").GetUInt32();
        var revised = await bench.PutAsync(admin, $"/api/catalog/lens-offers/{offerIds[0]}", new
        {
            requestId = NewId(), version, lensType = "single_vision", indexId = index, treatmentIds = new[] { blue }, production = "on_order", leadTimeDays = 5,
            sphereMin = "-10.00", sphereMax = "+6.00", priceHt = "42.5000", priceCurrency = "EUR", reason = "Nouvelle fiche fournisseur",
        });
        Assert.True(revised.Status == HttpStatusCode.OK, revised.Raw);
        var detail = await bench.GetAsync(reader, $"/api/catalog/lens-offers/{offerIds[0]}");
        Assert.Equal(2, detail["revisions"].GetArrayLength());
        Assert.False(detail["offer"].TryGetProperty("priceHt", out _)); // lecteur : aucun coût servi
    }
}
