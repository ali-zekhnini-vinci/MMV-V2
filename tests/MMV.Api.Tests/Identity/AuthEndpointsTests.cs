using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Identity;

/// <summary>
/// Pipeline HTTP réel de MMV.Api (authentification, policies, CORS, limitation de débit, HTTPS) ;
/// seul le store est remplacé par un double non persistant. La même matrice est rejouée sur
/// PostgreSQL (mmv_v2_test) par tests/MMV.PostgreSql.Tests après le GO O-02-01.
/// </summary>
public sealed class AuthEndpointsTests : IDisposable
{
    private const string AdminPassword = "Mot-de-passe-admin-1";
    private const string UserPassword = "Mot-de-passe-user-1";

    private readonly InMemoryIdentityStore _store = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly UserAccount _admin;
    private readonly UserAccount _user;

    public AuthEndpointsTests()
    {
        var hashing = RealHashing.Create();
        _admin = _store.AddUser(hashing, "admin", AdminPassword, Permissions.SystemAdmin);
        _user = _store.AddUser(hashing, "simple", UserPassword);
        _factory = CreateFactory(_store, loginAttemptsPerMinute: 1000);
    }

    public void Dispose() => _factory.Dispose();

    internal static WebApplicationFactory<Program> CreateFactory(IIdentityStore store, int loginAttemptsPerMinute) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:LoginAttemptsPerMinute", loginAttemptsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Une valeur null en dernière source masque un éventuel user-secret : aucune base n'est jamais jointe.
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = null }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentityStore>();
                services.AddScoped(_ => store);
            });
        });

    private HttpClient Https() => _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Get(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    [Theory]
    [InlineData("GET", "/api/auth/session")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("GET", "/api/admin/accounts")]
    public async Task Anonymous_Gets401WithNeutralBody(string method, string path)
    {
        using var client = Https();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal("""{"code":"unauthenticated"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownOrForgedToken_Gets401()
    {
        using var client = Https();

        using var forged = await client.SendAsync(Get("/api/auth/session", SessionTokens.NewToken()));
        using var garbage = await client.SendAsync(Get("/api/auth/session", "pas-un-jeton"));

        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.StatusCode);
    }

    [Fact]
    public async Task LoginFailures_AreNeutral_ForUnknownWrongAndDisabled()
    {
        _user.Deactivate();
        using var client = Https();
        string[] bodies =
        [
            await PostLoginBodyAsync(client, "inconnu", AdminPassword),
            await PostLoginBodyAsync(client, "admin", "mauvais-mot-de-passe"),
            await PostLoginBodyAsync(client, "simple", UserPassword),
        ];

        Assert.All(bodies, b => Assert.Equal("401:{\"code\":\"invalid_credentials\"}", b));
    }

    [Fact]
    public async Task Login_EmptyFields_Gets400WithFieldMessages()
    {
        using var client = Https();

        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = " ", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Veuillez saisir votre identifiant.", body, StringComparison.Ordinal);
        Assert.Contains("Veuillez saisir votre mot de passe.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_ReturnsBearerSession_WithoutCookieOrHash()
    {
        using var client = Https();

        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "ADMIN", password = AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        using var body = JsonDocument.Parse(json);
        var root = body.RootElement;
        Assert.Equal("Bearer", root.GetProperty("tokenType").GetString());
        Assert.Equal(1800, root.GetProperty("idleTimeoutSeconds").GetInt32());
        Assert.Equal("admin", root.GetProperty("user").GetProperty("userName").GetString());
        Assert.Equal("system.admin", root.GetProperty("user").GetProperty("permissions")[0].GetString());
    }

    [Fact]
    public async Task Admin_IsAuthorized_OnAccounts()
    {
        using var client = Https();
        var token = await LoginAsync(client, "admin", AdminPassword);

        using var response = await client.SendAsync(Get("/api/admin/accounts", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"userName\":\"simple\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonAdmin_Gets403_OnAccounts_But200_OnSession()
    {
        using var client = Https();
        var token = await LoginAsync(client, "simple", UserPassword);

        using var session = await client.SendAsync(Get("/api/auth/session", token));
        using var admin = await client.SendAsync(Get("/api/admin/accounts", token));

        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal("""{"code":"forbidden"}""", await admin.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PermissionRemovedDuringSession_IsEnforcedOnNextRequest()
    {
        using var client = Https();
        var token = await LoginAsync(client, "admin", AdminPassword);

        _admin.Revoke(Permissions.SystemAdmin);
        using var response = await client.SendAsync(Get("/api/admin/accounts", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesToken()
    {
        using var client = Https();
        var token = await LoginAsync(client, "admin", AdminPassword);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var logoutResponse = await client.SendAsync(logout);
        using var after = await client.SendAsync(Get("/api/auth/session", token));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Deactivation_RevokesActiveSession()
    {
        using var client = Https();
        var token = await LoginAsync(client, "simple", UserPassword);

        _user.Deactivate();
        using var response = await client.SendAsync(Get("/api/auth/session", token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PlainHttp_IsRefused_ForLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "admin", password = AdminPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("""{"code":"https_required"}""", await response.Content.ReadAsStringAsync());
        Assert.Empty(_store.Sessions);
    }

    [Theory]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("POST", "/api/auth/bootstrap")]
    [InlineData("POST", "/api/admin/bootstrap")]
    [InlineData("POST", "/api/auth/reset-password")]
    // Phase 03-A2 : /api/clients existe désormais (protégé par clients.*, ClientsEndpointsTests) ; les modules
    // non livrés restent absents.
    [InlineData("GET", "/api/appointments")]
    [InlineData("GET", "/api/prescriptions")]
    public async Task NoPublicSignupBootstrapOrBusinessRoute(string method, string path)
    {
        using var client = Https();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_IsRateLimited()
    {
        using var factory = CreateFactory(_store, loginAttemptsPerMinute: 3);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "admin", password = "faux" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
    }

    [Theory]
    [InlineData("/api/auth/login", "POST", "content-type")]
    [InlineData("/api/admin/accounts", "GET", "authorization")]
    public async Task ApiPreflight_FromDesktopOrigin_AllowsMethodAndHeader_WithoutCredentials(string path, string method, string header)
    {
        using var client = Https();
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", "http://localhost:1420");
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", header);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:1420", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains(response.Headers.GetValues("Access-Control-Allow-Headers"), h => h.Contains(header, StringComparison.OrdinalIgnoreCase));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task ApiPreflight_FromForeignOrigin_IsNotAllowed()
    {
        using var client = Https();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task HealthEndpoints_StayAnonymous()
    {
        using var client = Https();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> PostLoginBodyAsync(HttpClient client, string userName, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        return $"{(int)response.StatusCode}:{await response.Content.ReadAsStringAsync()}";
    }
}
