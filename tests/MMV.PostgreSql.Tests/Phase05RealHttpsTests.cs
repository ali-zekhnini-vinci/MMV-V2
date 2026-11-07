using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MMV.Domain.Identity;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Intégration finale de la Phase 05 (30/10/2026) : contrôles propres au processus MMV.Api RÉEL désigné par
/// <c>MMV_PG05_API_BASE</c> (Kestrel HTTPS, même base que le banc). Les sept scénarios de
/// <see cref="Phase05PostgreSqlTests"/> passent alors aussi par ce processus. HTTP en clair (<c>MMV_PG05_API_PLAIN</c>,
/// <c>http://localhost:&lt;port&gt;</c> du même processus) : refus <c>403 https_required</c> de toute route de la Phase 05,
/// lecture comme écriture, rien écrit. Sans ces variables : « Skipped » (NOT_TESTED).
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05RealHttpsTests(ITestOutputHelper output)
{
    public const string PlainVariable = "MMV_PG05_API_PLAIN";

    [Phase05RealApiFact]
    public async Task RealProcess_ServesTheBench_AndRefusesPlainHttp_NothingWritten()
    {
        await using var bench = await Phase05Bench.CreateAsync(output);
        Assert.NotNull(bench.RealApi);
        var reader = await bench.UserAsync("lecteur", Permissions.CatalogRead, Permissions.StockRead, Permissions.SuppliersRead, Permissions.SuppliersWrite, Permissions.CatalogImport);
        var families = await bench.GetAsync(reader, "/api/catalog/families");
        Assert.Equal(HttpStatusCode.OK, families.Status);
        Assert.Equal(6, families.Body.GetArrayLength());

        var plain = new Uri(Environment.GetEnvironmentVariable(PlainVariable)!);
        Assert.True(plain.Scheme == Uri.UriSchemeHttp && plain.Host == "localhost" && plain.Port != bench.RealApi!.Port, $"{PlainVariable} : http://localhost:<port> attendu");
        using var http = new HttpClient { BaseAddress = plain };
        using var login = await http.PostAsJsonAsync("/api/auth/login", new { userName = $"p05-lecteur-{bench.Suffix}".ToLowerInvariant(), password = Phase05Bench.Password });
        var loginText = await login.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("https_required", loginText, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", loginText, StringComparison.Ordinal);

        var token = bench.TokenOf(reader);
        foreach (var path in new[] { "/api/stores", "/api/catalog/families", "/api/catalog/variants", "/api/catalog/lens-references", "/api/suppliers", $"/api/stock/balances?storeId={bench.StoreBe}", "/api/inventories", "/api/imports" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"GET {path} en HTTP : {(int)response.StatusCode}");
            Assert.Contains("https_required", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            output.WriteLine($"HTTP en clair GET {path} -> 403 https_required");
        }

        using (var post = new HttpRequestMessage(HttpMethod.Post, "/api/suppliers"))
        {
            post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            post.Content = JsonContent.Create(new { requestId = Guid.NewGuid(), code = $"FH{bench.Suffix}", name = "Fournisseur fictif HTTP", countryCode = "BE" });
            using var response = await http.SendAsync(post);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("https_required", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.suppliers WHERE code = @c", ("c", $"FH{bench.Suffix}")));
        output.WriteLine("HTTP en clair POST /api/suppliers -> 403 https_required, aucune ligne écrite");

        // Même jeton accepté en HTTPS par le processus réel : le refus tient au transport, pas au jeton.
        Assert.Equal(HttpStatusCode.OK, (await bench.GetAsync(reader, "/api/suppliers")).Status);
    }
}

/// <summary>« Skipped » sauf cible Phase 05 résolue ET processus réel (<c>MMV_PG05_API_BASE</c>, <c>MMV_PG05_API_PLAIN</c>).</summary>
public sealed class Phase05RealApiFactAttribute : FactAttribute
{
    public Phase05RealApiFactAttribute() => Skip = Phase05Target.SkipReason
        ?? (Phase05Bench.RealApiBase() is null || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Phase05RealHttpsTests.PlainVariable))
            ? $"NOT_TESTED : processus MMV.Api réel non désigné ({Phase05Bench.ApiBaseVariable}, {Phase05RealHttpsTests.PlainVariable})."
            : null);
}
