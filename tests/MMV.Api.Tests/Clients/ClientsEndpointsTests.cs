using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MMV.Api.Tests.Identity;
using MMV.Application.Clients;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Clients;

/// <summary>
/// Pipeline HTTP réel de MMV.Api pour /api/clients (authentification, policies, HTTPS, CORS, validation, codes
/// d'erreur, journaux) ; seuls les stores sont remplacés par des doubles non persistants. Données fictives.
/// </summary>
public sealed class ClientsEndpointsTests : IDisposable
{
    private const string Password = "Mot-de-passe-test-1";

    private readonly InMemoryIdentityStore _identity = new();
    private readonly InMemoryClientStore _clients = new();
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ClientsEndpointsTests()
    {
        var hashing = RealHashing.Create();
        _identity.AddUser(hashing, "aucun", Password);
        _identity.AddUser(hashing, "admin", Password, Permissions.SystemAdmin);
        _identity.AddUser(hashing, "lecture", Password, Permissions.ClientsRead);
        _identity.AddUser(hashing, "ecriture", Password, Permissions.ClientsRead, Permissions.ClientsWrite);
        _identity.AddUser(hashing, "complet", Password, Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsArchive);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = null }));
            builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(_logs)));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentityStore>();
                services.AddScoped<IIdentityStore>(_ => _identity);
                services.RemoveAll<IClientStore>();
                services.AddScoped<IClientStore>(_ => _clients);
            });
        });
    }

    public void Dispose() => _factory.Dispose();

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", "/api/clients" },
        { "GET", $"/api/clients/{Guid.Empty}" },
        { "POST", "/api/clients" },
        { "PUT", $"/api/clients/{Guid.Empty}" },
        { "POST", $"/api/clients/{Guid.Empty}/archive" },
        { "POST", $"/api/clients/{Guid.Empty}/reactivate" },
    };

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Anonymous_Gets401(string method, string path)
    {
        using var client = Https();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("""{"code":"unauthenticated"}""", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task SystemAdminAlone_OrNoPermission_Gets403(string method, string path)
    {
        foreach (var user in new[] { "admin", "aucun" })
        {
            using var client = await AuthenticatedAsync(user);

            using var response = await client.SendAsync(Json(method, path, new { version = 1 }));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("""{"code":"forbidden"}""", await response.Content.ReadAsStringAsync());
        }

        Assert.Equal(0, _clients.SaveCount);
    }

    [Fact]
    public async Task EachWriteRequiresItsOwnPermission()
    {
        var seeded = _clients.Seed("Jeanne", "Martin");
        using var reader = await AuthenticatedAsync("lecture");
        using var writer = await AuthenticatedAsync("ecriture");

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/clients/{seeded.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.SendAsync(Json("POST", "/api/clients", ValidInput()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.SendAsync(Json("PUT", $"/api/clients/{seeded.Id}", ValidInput(seeded.Version)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.SendAsync(Json("POST", $"/api/clients/{seeded.Id}/archive", new { version = seeded.Version }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.SendAsync(Json("POST", $"/api/clients/{seeded.Id}/reactivate", new { version = seeded.Version }))).StatusCode);
        Assert.False(seeded.IsArchived);
    }

    [Fact]
    public async Task FullLifecycle_CreateReadUpdateArchiveReactivate()
    {
        using var client = await AuthenticatedAsync("complet");

        using var created = await client.SendAsync(Json("POST", "/api/clients", new
        {
            firstName = " Éloïse ",
            lastName = "Lefèvre",
            birthDate = "1985-04-12",
            phone = "0470 12 34 56",
            email = "eloise@exemple.be",
            addressLine = "Rue de l'Exemple 1",
            city = "Namur",
            postalCode = "5000",
            notes = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detail = await ReadAsync(created);
        var id = detail.GetProperty("id").GetString()!;
        Assert.Equal($"/api/clients/{id}", created.Headers.Location!.OriginalString);
        Assert.Equal(
            ["addressLine", "archivedAtUtc", "birthDate", "city", "createdAtUtc", "email", "firstName", "id", "lastName", "notes",
             "phone", "postalCode", "reference", "status", "updatedAtUtc", "version"],
            PropertyNames(detail));
        Assert.Equal("Éloïse", detail.GetProperty("firstName").GetString());
        Assert.Equal("1985-04-12", detail.GetProperty("birthDate").GetString());
        Assert.Equal("active", detail.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("notes").ValueKind);
        Assert.Matches("^CLI-[0-9]{6}$", detail.GetProperty("reference").GetString());
        var version = detail.GetProperty("version").GetUInt32();

        // Liste : colonnes affichées seulement, recherche insensible aux accents.
        var page = await ReadAsync(await client.GetAsync("/api/clients?q=LEFEVRE&status=active&sort=name&dir=asc&page=1&pageSize=20"));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        var summary = page.GetProperty("items")[0];
        Assert.Equal(
            ["city", "createdAtUtc", "email", "firstName", "id", "lastName", "phone", "reference", "status", "version"],
            PropertyNames(summary));

        // Modification versionnée puis version périmée : 409, aucune écriture.
        using var updated = await client.SendAsync(Json("PUT", $"/api/clients/{id}", ValidInput(version, city: "Liège")));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var newVersion = (await ReadAsync(updated)).GetProperty("version").GetUInt32();
        Assert.NotEqual(version, newVersion);
        await AssertErrorAsync(await client.SendAsync(Json("PUT", $"/api/clients/{id}", ValidInput(version, city: "Mons"))), HttpStatusCode.Conflict, "version_conflict");
        Assert.Equal("Liège", (await ReadAsync(await client.GetAsync($"/api/clients/{id}"))).GetProperty("city").GetString());

        // Archivage : lecture seule, exclu de la liste par défaut.
        await AssertErrorAsync(await client.SendAsync(Json("POST", $"/api/clients/{id}/archive", new { version })), HttpStatusCode.Conflict, "version_conflict");
        using var archived = await client.SendAsync(Json("POST", $"/api/clients/{id}/archive", new { version = newVersion }));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        var archivedDetail = await ReadAsync(archived);
        Assert.Equal("archived", archivedDetail.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, archivedDetail.GetProperty("archivedAtUtc").ValueKind);
        var archivedVersion = archivedDetail.GetProperty("version").GetUInt32();
        await AssertErrorAsync(await client.SendAsync(Json("PUT", $"/api/clients/{id}", ValidInput(archivedVersion))), HttpStatusCode.Conflict, "client_archived");
        await AssertErrorAsync(await client.SendAsync(Json("POST", $"/api/clients/{id}/archive", new { version = archivedVersion })), HttpStatusCode.Conflict, "already_archived");
        Assert.Equal(0, (await ReadAsync(await client.GetAsync("/api/clients"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await ReadAsync(await client.GetAsync("/api/clients?status=archived"))).GetProperty("totalCount").GetInt32());

        using var reactivated = await client.SendAsync(Json("POST", $"/api/clients/{id}/reactivate", new { version = archivedVersion }));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal("active", (await ReadAsync(reactivated)).GetProperty("status").GetString());
        await AssertErrorAsync(
            await client.SendAsync(Json("POST", $"/api/clients/{id}/reactivate", new { version = (await ReadAsync(await client.GetAsync($"/api/clients/{id}"))).GetProperty("version").GetUInt32() })),
            HttpStatusCode.Conflict,
            "already_active");
        Assert.Single(_clients.Clients);
    }

    [Fact]
    public async Task Validation_Returns422WithBoundedFieldCodes()
    {
        using var client = await AuthenticatedAsync("complet");

        using var response = await client.SendAsync(Json("POST", "/api/clients", new
        {
            firstName = "  ",
            lastName = new string('x', 101),
            birthDate = "1850-01-01",
            phone = "12",
            email = "pas-un-email",
            addressLine = (string?)null,
            city = (string?)null,
            postalCode = "_",
            notes = (string?)null,
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal("required", errors.GetProperty("firstName")[0].GetString());
        Assert.Equal("too_long", errors.GetProperty("lastName")[0].GetString());
        Assert.Equal("out_of_range", errors.GetProperty("birthDate")[0].GetString());
        Assert.Equal("invalid_format", errors.GetProperty("phone")[0].GetString());
        Assert.Equal("invalid_format", errors.GetProperty("email")[0].GetString());
        Assert.Equal("invalid_format", errors.GetProperty("postalCode")[0].GetString());
        // Aucune valeur saisie renvoyée.
        Assert.DoesNotContain("pas-un-email", body.GetRawText(), StringComparison.Ordinal);
        Assert.Empty(_clients.Clients);
    }

    [Theory]
    [InlineData("""{"firstName":"Jeanne","lastName":"Martin","socialSecurityNumber":"85.04.12-123.45"}""", "invalid_body")]
    [InlineData("""{"firstName":"Jeanne",""", "invalid_body")]
    [InlineData("""{"firstName":12,"lastName":"Martin"}""", "invalid_body")]
    [InlineData("[]", "invalid_body")]
    [InlineData("null", "invalid_body")]
    public async Task UnreadableOrUnknownBody_Is400(string json, string code)
    {
        using var client = await AuthenticatedAsync("complet");

        await AssertErrorAsync(await client.SendAsync(RawJson("POST", "/api/clients", json)), HttpStatusCode.BadRequest, code);
        Assert.Empty(_clients.Clients);
    }

    [Fact]
    public async Task NonJsonContentType_Is415()
    {
        using var client = await AuthenticatedAsync("complet");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/clients")
        {
            Content = new StringContent("""{"firstName":"Jeanne","lastName":"Martin"}""", Encoding.UTF8, "text/plain"),
        };

        // Métadonnée Accepts("application/json") : refus par le routage, avant le gestionnaire.
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Empty(_clients.Clients);
    }

    [Theory]
    [InlineData("""{"firstName":"Jeanne","lastName":"Martin"}""", "version_required")]
    [InlineData("""{"firstName":"Jeanne","lastName":"Martin","version":null}""", "version_required")]
    [InlineData("""{"firstName":"Jeanne","lastName":"Martin","version":-1}""", "invalid_body")]
    [InlineData("""{"firstName":"Jeanne","lastName":"Martin","version":"12"}""", "invalid_body")]
    public async Task Update_RequiresAValidVersion(string json, string code)
    {
        var seeded = _clients.Seed("Jeanne", "Martin");
        using var client = await AuthenticatedAsync("complet");

        await AssertErrorAsync(await client.SendAsync(RawJson("PUT", $"/api/clients/{seeded.Id}", json)), HttpStatusCode.BadRequest, code);
        await AssertErrorAsync(await client.SendAsync(RawJson("POST", $"/api/clients/{seeded.Id}/archive", "{}")), HttpStatusCode.BadRequest, "version_required");
    }

    [Fact]
    public async Task UnknownClient_Is404()
    {
        using var client = await AuthenticatedAsync("complet");
        var id = Guid.CreateVersion7();

        await AssertErrorAsync(await client.GetAsync($"/api/clients/{id}"), HttpStatusCode.NotFound, "client_not_found");
        await AssertErrorAsync(await client.SendAsync(Json("PUT", $"/api/clients/{id}", ValidInput(1))), HttpStatusCode.NotFound, "client_not_found");
        await AssertErrorAsync(await client.SendAsync(Json("POST", $"/api/clients/{id}/archive", new { version = 1 })), HttpStatusCode.NotFound, "client_not_found");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/clients/pas-un-uuid")).StatusCode);
    }

    [Theory]
    [InlineData("?status=deleted")]
    [InlineData("?sort=email")]
    [InlineData("?dir=up")]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?page=1.5")]
    [InlineData("?page=abc")]
    [InlineData("?page=100001")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=%2B20")]
    [InlineData("?q=a")]
    [InlineData("?q=%20%20b%20")]
    [InlineData("?unknown=1")]
    [InlineData("?status=active&status=all")]
    [InlineData("?Status=active")]
    public async Task List_OutsideWhitelist_Is400(string query)
    {
        _clients.Seed("Jeanne", "Martin");
        using var client = await AuthenticatedAsync("lecture");

        await AssertErrorAsync(await client.GetAsync("/api/clients" + query), HttpStatusCode.BadRequest, "invalid_query");
    }

    [Fact]
    public async Task List_TooLongSearch_Is400_AndPaginationIsServerSide()
    {
        for (var i = 0; i < 25; i++)
        {
            _clients.Seed($"Prénom{i:00}", "Dupont", city: i % 2 == 0 ? "Namur" : null);
        }

        using var client = await AuthenticatedAsync("lecture");
        await AssertErrorAsync(await client.GetAsync("/api/clients?q=" + new string('a', 101)), HttpStatusCode.BadRequest, "invalid_query");

        var second = await ReadAsync(await client.GetAsync("/api/clients?sort=createdAt&dir=desc&page=2&pageSize=20"));
        Assert.Equal(25, second.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.Equal(5, second.GetProperty("items").GetArrayLength());
        Assert.Equal(0, (await ReadAsync(await client.GetAsync("/api/clients?page=9"))).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task NoDeleteRoute_AndHttpIsRefused()
    {
        var seeded = _clients.Seed("Jeanne", "Martin");
        using var client = await AuthenticatedAsync("complet");

        using var delete = await client.DeleteAsync($"/api/clients/{seeded.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
        Assert.Single(_clients.Clients);

        using var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        http.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        await AssertErrorAsync(await http.GetAsync("/api/clients"), HttpStatusCode.Forbidden, "https_required");
    }

    [Fact]
    public async Task CorsPreflight_AllowsPutButNotDelete()
    {
        using var client = Https();

        using var put = await client.SendAsync(Preflight($"/api/clients/{Guid.Empty}", "PUT"));
        using var delete = await client.SendAsync(Preflight($"/api/clients/{Guid.Empty}", "DELETE"));

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Contains("PUT", string.Join(",", put.Headers.GetValues("Access-Control-Allow-Methods")), StringComparison.Ordinal);
        var deleteMethods = delete.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods) ? string.Join(",", methods) : string.Empty;
        Assert.DoesNotContain("DELETE", deleteMethods, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_ContainTechnicalIdentifiersOnly()
    {
        using var client = await AuthenticatedAsync("complet");
        using var created = await client.SendAsync(Json("POST", "/api/clients", new
        {
            firstName = "Zéphyrine",
            lastName = "Quintaval",
            birthDate = "1971-03-09",
            phone = "0499 87 65 43",
            email = "zephyrine.quintaval@exemple.be",
            addressLine = "Chemin Secret 42",
            city = "Bouillon",
            postalCode = "6830",
            notes = "Préfère être appelée le matin",
        }));
        var detail = await ReadAsync(created);
        var id = detail.GetProperty("id").GetString()!;
        await client.SendAsync(Json("PUT", $"/api/clients/{id}", ValidInput(detail.GetProperty("version").GetUInt32() + 99)));
        await client.GetAsync("/api/clients?q=quintaval");

        var logs = string.Join(Environment.NewLine, _logs);
        Assert.Contains(id, logs, StringComparison.Ordinal);
        // Y compris le terme de recherche de l'URL (journaux d'hébergement ASP.NET Core au niveau Warning).
        foreach (var personal in new[] { "Zéphyrine", "Zephyrine", "Quintaval", "0499 87", "0499876543", "zephyrine.quintaval@exemple.be", "Chemin Secret", "Bouillon", "1971-03-09", "Préfère" })
        {
            Assert.DoesNotContain(personal, logs, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static object ValidInput(uint? version = null, string? city = null) => new
    {
        firstName = "Jeanne",
        lastName = "Martin",
        birthDate = (string?)null,
        phone = (string?)null,
        email = (string?)null,
        addressLine = (string?)null,
        city,
        postalCode = (string?)null,
        notes = (string?)null,
        version,
    };

    private HttpClient Https() => _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private async Task<HttpClient> AuthenticatedAsync(string userName)
    {
        var client = Https();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { userName, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await ReadAsync(login)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpRequestMessage Json(string method, string path, object body) =>
        new(new HttpMethod(method), path) { Content = JsonContent.Create(body) };

    private static HttpRequestMessage RawJson(string method, string path, string json) =>
        new(new HttpMethod(method), path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpRequestMessage Preflight(string path, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", "http://localhost:1420");
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal($$"""{"code":"{{code}}"}""", await response.Content.ReadAsStringAsync());
        }
    }

    private static string[] PropertyNames(JsonElement element) =>
        [.. element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, sink);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            sink.Enqueue($"{category} {logLevel} {formatter(state, exception)} {exception}");
    }
}
