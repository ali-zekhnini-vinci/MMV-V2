using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Api.Cors;

namespace MMV.Api.Tests;

/// <summary>
/// CORS de l'environnement Development (origines de appsettings.Development.json).
/// Les origines réelles de WebView2 restent à relever depuis la fenêtre Tauri.
/// </summary>
public sealed class DesktopCorsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("http://localhost:1420")]
    [InlineData("http://tauri.localhost")]
    public async Task Preflight_FromAllowedOrigin_AllowsGet(string origin)
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("/health/live", origin, "GET"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("GET", response.Headers.GetValues("Access-Control-Allow-Methods"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost:1421")]
    [InlineData("null")]
    public async Task Preflight_FromForeignOrigin_IsNotAllowed(string origin)
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("/health/ready", origin, "GET"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_ForNonGetMethod_IsNotAllowed()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("/health/live", "http://localhost:1420", "POST"));

        // ASP.NET Core renvoie la liste des méthodes autorisées ; le navigateur refuse POST si elle ne l'inclut pas.
        var allowed = response.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods) ? methods : [];
        Assert.DoesNotContain(allowed, m => m.Contains("POST", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SimpleGet_FromAllowedOrigin_ReturnsAllowOrigin()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "http://tauri.localhost");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://tauri.localhost", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example")]
    [InlineData("http://localhost:1420/")]
    [InlineData("localhost:1420")]
    public void AddDesktopCors_RejectsWildcardOrNonExactOrigin(string origin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{DesktopCors.OriginsKey}:0"] = origin })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddDesktopCors(configuration));
    }

    private static HttpRequestMessage Preflight(string path, string origin, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        return request;
    }
}
