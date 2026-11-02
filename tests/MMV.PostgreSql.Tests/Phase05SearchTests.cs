using System.Net;
using MMV.Domain.Identity;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// COR-05-07 — recherche harmonisée : serveur, sur TOUTES les données (pas la page chargée), insensible à la casse et
/// aux accents, mots combinés (ET), forme compacte (« rayban », « RX 5228 »), coloris, références fournisseur.
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05SearchTests(ITestOutputHelper output)
{
    private static Guid NewId() => Guid.NewGuid();

    [Phase05Fact]
    public async Task Search_Catalog_Stock_Suppliers_AreServerSide_AccentAndCaseInsensitive()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        var admin = await bench.UserAsync("admin", Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.StockRead, Permissions.SuppliersRead, Permissions.SuppliersWrite);
        var s = bench.Suffix;

        async Task<Guid> Model(string brand, string name, params (string Sku, string Label, string Color)[] variants)
        {
            var created = await bench.PostAsync(admin, "/api/catalog/models", new
            {
                requestId = NewId(), familyCode = "optical_frames", brand, name,
                variants = variants.Select(v => new { sku = v.Sku, label = v.Label, stockPolicy = "stocked", attributes = new Dictionary<string, string> { ["color"] = v.Color } }).ToArray(),
            });
            Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
            return created["id"].GetGuid();
        }

        // 55 modèles de remplissage d'abord : les cibles ne sont pas dans la première page triée par nom.
        for (var i = 0; i < 55; i++)
        {
            await Model("Remplissage", $"AAA {s} {i:00}", ($"FIL-{s}-{i:00}", "Standard", "Gris"));
        }

        await Model("Ray-Ban", $"ZZ RX5228 {s}", ($"RB-{s}-1", "Noir mat", "Noir"), ($"RB-{s}-2", "Havane", "Écaille"));
        await Model("Ray-Ban", $"ZZ Clubmaster {s}", ($"RB-{s}-3", "Doré", "Or"));
        await Model("Oakley", $"ZZ Holbrook {s}", ($"OK-{s}-1", "Noir", "Noir"));

        async Task<int> Count(string q, string path = "/api/catalog/variants")
        {
            var page = await bench.GetAsync(admin, $"{path}?q={Uri.EscapeDataString(q + " " + s)}&pageSize=20");
            Assert.True(page.Status == HttpStatusCode.OK, page.Raw);
            return page["totalCount"].GetInt32();
        }

        Assert.Equal(3, await Count("Ray Ban"));
        Assert.Equal(3, await Count("rayban"));
        Assert.Equal(3, await Count("  RAY-BAN  "));
        Assert.Equal(2, await Count("RX5228"));
        Assert.Equal(2, await Count("rx 5228"));
        Assert.Equal(2, await Count("noir"));
        Assert.Equal(1, await Count("ray ban noir"));
        Assert.Equal(1, await Count("ecaille"));
        Assert.Equal(1, await Count("ÉCAILLE"));
        Assert.Equal(1, await Count("dore"));
        Assert.Equal(0, await Count("ray ban introuvable"));
        Assert.Equal(1, await Count($"RB-{s}-3"));
        // Caractères LIKE neutralisés : « % » et « _ » ne sont jamais des jokers.
        Assert.Equal(0, await Count("%"));
        Assert.Equal(0, await Count("r_y"));

        // Stock : même recherche (variantes stockées, une ligne par emplacement actif du magasin : 2 emplacements BE).
        Assert.Equal(2, await Count("ray ban noir", "/api/stock/balances"));

        // Fournisseurs : nom sans accent, référence fournisseur d'un lien actif.
        var supplier = await bench.PostAsync(admin, "/api/suppliers", new { requestId = NewId(), code = $"LUX{s}", name = $"Lunetterie Générale {s}", countryCode = "BE" });
        Assert.Equal(HttpStatusCode.Created, supplier.Status);
        var supplierId = supplier["id"].GetGuid();
        var variant = (await bench.GetAsync(admin, $"/api/catalog/variants?q={Uri.EscapeDataString($"OK-{s}-1")}"))["items"][0].GetProperty("variantId").GetGuid();
        var link = await bench.PutAsync(admin, $"/api/catalog/variants/{variant}/suppliers/{supplierId}", new { requestId = NewId(), supplierReference = $"0045-HB-{s}", isPrimary = true });
        Assert.True(link.Status == HttpStatusCode.OK, link.Raw);
        Assert.Equal(1, await Count("lunetterie generale", "/api/suppliers"));
        Assert.Equal(1, (await bench.GetAsync(admin, $"/api/suppliers?q={Uri.EscapeDataString($"0045-HB-{s}")}"))["totalCount"].GetInt32());
        // Variante retrouvée par la référence fournisseur (zéros initiaux conservés : texte, jamais un nombre).
        Assert.Equal(1, (await bench.GetAsync(admin, $"/api/catalog/variants?q={Uri.EscapeDataString($"0045-hb-{s}")}"))["totalCount"].GetInt32());
        Assert.Equal(0, (await bench.GetAsync(admin, $"/api/catalog/variants?q={Uri.EscapeDataString($"45-HB-{s}x")}"))["totalCount"].GetInt32());
    }
}
