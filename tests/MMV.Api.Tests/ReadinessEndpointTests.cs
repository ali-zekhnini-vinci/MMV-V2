using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MMV.Api.Health;
using MMV.Infrastructure;

namespace MMV.Api.Tests;

/// <summary>
/// Readiness sans base PostgreSQL : chaîne absente, port fermé, serveur muet (timeout), annulation.
/// Aucune base n'est contactée : le cas « PostgreSQL disponible » est couvert par MMV.PostgreSql.Tests.
/// La chaîne est toujours fixée par le test, pour ne jamais hériter d'un user-secret de développement.
/// </summary>
public sealed class ReadinessEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] AllowedFields = ["checkedAtUtc", "durationMs", "scope", "status"];

    [Fact]
    public async Task GetReady_WithoutConnectionString_Returns503WithoutDetails()
    {
        using var client = CreateClient(connectionString: null);

        using var response = await client.GetAsync(ReadinessEndpoint.Path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertBoundedBodyAsync(response, "Unhealthy");
    }

    [Fact]
    public async Task GetReady_WhenPortIsClosed_Returns503WithoutLeakingTarget()
    {
        var port = GetClosedPort();
        using var client = CreateClient(
            $"Host=127.0.0.1;Port={port};Database=mmv_leak_probe;Username=mmv_leak_probe;Password=leak-probe-secret");

        using var response = await client.GetAsync(ReadinessEndpoint.Path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await AssertBoundedBodyAsync(response, "Unhealthy");
        foreach (var leaked in new[] { "127.0.0.1", port.ToString(System.Globalization.CultureInfo.InvariantCulture), "mmv_leak_probe", "leak-probe-secret" })
        {
            Assert.DoesNotContain(leaked, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetReady_WhenServerNeverAnswers_TimesOutWith503()
    {
        using var silentServer = new TcpListener(IPAddress.Loopback, 0);
        silentServer.Start();
        var port = ((IPEndPoint)silentServer.LocalEndpoint).Port;
        // Timeout Npgsql (30 s) supérieur au timeout du check (3 s) : c'est le check qui doit couper.
        using var client = CreateClient($"Host=127.0.0.1;Port={port};Database=unused;Username=unused;Password=unused;Timeout=30");

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(ReadinessEndpoint.Path);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertBoundedBodyAsync(response, "Unhealthy");
        // Borne haute : timeout du check (3 s) + marge d'hôte de test ; jamais le Timeout=30 de la chaîne.
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), ReadinessEndpoint.Timeout + TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetLive_StaysHealthy_WhenDatabaseIsUnreachable()
    {
        using var client = CreateClient($"Host=127.0.0.1;Port={GetClosedPort()};Database=unused;Username=unused;Password=unused");

        using var live = await client.GetAsync(LivenessEndpoint.Path);
        using var ready = await client.GetAsync(ReadinessEndpoint.Path);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task ReadinessCheck_WhenCanceled_ThrowsInsteadOfReportingHealthy()
    {
        using var silentServer = new TcpListener(IPAddress.Loopback, 0);
        silentServer.Start();
        var port = ((IPEndPoint)silentServer.LocalEndpoint).Port;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mmv"] = $"Host=127.0.0.1;Port={port};Database=unused;Username=unused;Password=unused;Timeout=30",
            })
            .Build();
        await using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var check = new DatabaseReadinessCheck(scope.ServiceProvider);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
    }

    private HttpClient CreateClient(string? connectionString) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Une valeur null en dernière source masque aussi un éventuel user-secret.
                    ["ConnectionStrings:Mmv"] = connectionString,
                })))
            .CreateClient();

    private static async Task<string> AssertBoundedBodyAsync(HttpResponseMessage response, string expectedStatus)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var fields = document.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();

        Assert.Equal(AllowedFields, fields);
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetString());
        Assert.Equal("database", document.RootElement.GetProperty("scope").GetString());
        return body;
    }

    private static int GetClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
