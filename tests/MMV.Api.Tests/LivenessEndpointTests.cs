using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MMV.Api.Tests;

public sealed class LivenessEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetLive_ReturnsHealthyProcessScope()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<LivenessBody>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
        Assert.Equal("process", body.Scope);
        Assert.Equal(Environment.ProcessId, body.ProcessId);
        Assert.True(body.UptimeSeconds >= 0);
    }

    [Fact]
    public async Task GetLive_DoesNotRequireDatabaseConfiguration()
    {
        // Aucune chaîne ConnectionStrings:Mmv n'est fournie au test : la vivacité
        // ne doit ni la lire ni ouvrir de connexion PostgreSQL.
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/clients");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record LivenessBody(string Status, string Scope, int ProcessId, double UptimeSeconds);
}
