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
using MMV.Api.Tests.Audit;
using MMV.Api.Tests.Clients;
using MMV.Api.Tests.Identity;
using MMV.Application.Audit;
using MMV.Application.Clients;
using MMV.Application.Identity;
using MMV.Application.Optical;
using MMV.Domain.Audit;
using MMV.Domain.Identity;
using MMV.Domain.Optical;

namespace MMV.Api.Tests.Optical;

/// <summary>
/// Pipeline HTTP réel de MMV.Api pour /api/optical (authentification, policies cumulatives, HTTPS, validation, codes
/// d'erreur, en-têtes, journaux, filtrage de l'historique) ; seuls les stores sont des doubles non persistants. Données
/// fictives. La persistance PostgreSQL réelle relève de MMV.PostgreSql.Tests (après G04-SQL).
/// </summary>
public sealed class OpticalEndpointsTests : IDisposable
{
    private const string Password = "Mot-de-passe-test-1";
    private const string SecretPrescriber = "Dr Fictif-Secret-Prescripteur";
    private const string SecretNote = "NOTE-FICTIVE-CONFIDENTIELLE";
    private const string SecretSphere = "-7,77";

    private readonly InMemoryIdentityStore _identity = new();
    private readonly InMemoryClientStore _clients = new();
    private readonly InMemoryAuditStore _auditStore = new();
    private readonly InMemoryOpticalStore<Prescription, PrescriptionRevision, PrescriptionContent> _prescriptions;
    private readonly InMemoryOpticalStore<Measurement, MeasurementRevision, MeasurementContent> _measurements;
    private readonly InMemoryOpticalReadStore _reads;
    private readonly InMemoryOpticalDirectory _directory = new();
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Guid _measurerId;

    public OpticalEndpointsTests()
    {
        _prescriptions = new("RX", _clients.Audit);
        _measurements = new("MES", _clients.Audit);
        _reads = new InMemoryOpticalReadStore(_prescriptions, _measurements, _clients, _clients.Audit);
        var hashing = RealHashing.Create();
        _identity.AddUser(hashing, "aucun", Password);
        _identity.AddUser(hashing, "admin", Password, Permissions.SystemAdmin);
        _identity.AddUser(hashing, "clients", Password, Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsArchive);
        _identity.AddUser(hashing, "auditeur", Password, Permissions.ClientsRead, Permissions.AuditRead);
        _identity.AddUser(hashing, "sansclients", Password, Permissions.OpticalRead, Permissions.OpticalPrescriptionsWrite, Permissions.OpticalInvalidate);
        _identity.AddUser(hashing, "lecture", Password, Permissions.ClientsRead, Permissions.OpticalRead);
        _identity.AddUser(hashing, "ordonnances", Password, Permissions.ClientsRead, Permissions.OpticalRead, Permissions.OpticalPrescriptionsWrite);
        _measurerId = _identity.AddUser(hashing, "mesures", Password, Permissions.ClientsRead, Permissions.OpticalRead, Permissions.OpticalMeasurementsWrite).Id;
        _identity.AddUser(hashing, "invalideur", Password, Permissions.ClientsRead, Permissions.OpticalRead, Permissions.OpticalInvalidate);
        _identity.AddUser(hashing, "complet", Password,
            Permissions.ClientsRead, Permissions.AuditRead, Permissions.OpticalRead, Permissions.OpticalPrescriptionsWrite,
            Permissions.OpticalMeasurementsWrite, Permissions.OpticalInvalidate);
        _directory.Measurers.Add(new OpticalActorView(_measurerId, "Fictif-Mesures"));
        foreach (var user in _identity.Users)
        {
            _reads.Names[user.Id] = user.DisplayName;
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = null }));
            builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(_logs)));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentityStore>();
                services.AddScoped<IIdentityStore>(_ => _identity);
                services.RemoveAll<IClientStore>();
                services.AddScoped<IClientStore>(_ => _clients);
                services.RemoveAll<IAuditRecorder>();
                services.AddScoped<IAuditRecorder>(_ => _clients.Audit);
                services.RemoveAll<IAuditStore>();
                services.AddScoped<IAuditStore>(_ => _auditStore);
                services.RemoveAll<IOpticalRecordStore<Prescription>>();
                services.AddScoped<IOpticalRecordStore<Prescription>>(_ => _prescriptions);
                services.RemoveAll<IOpticalRecordStore<Measurement>>();
                services.AddScoped<IOpticalRecordStore<Measurement>>(_ => _measurements);
                services.RemoveAll<IOpticalReadStore>();
                services.AddScoped<IOpticalReadStore>(_ => _reads);
                services.RemoveAll<IOpticalDirectory>();
                services.AddScoped<IOpticalDirectory>(_ => _directory);
            });
        });
    }

    public void Dispose() => _factory.Dispose();

    private static readonly Guid Unknown = Guid.Parse("01900000-0000-7000-8000-000000000001");

    /// <summary>Routes, méthode, et compte ayant EXACTEMENT les droits requis.</summary>
    public static TheoryData<string, string, string> Routes => new()
    {
        { "GET", "/api/optical/records", "lecture" },
        { "GET", $"/api/optical/clients/{Unknown}/records", "lecture" },
        { "GET", "/api/optical/measurers", "lecture" },
        { "GET", $"/api/optical/prescriptions/{Unknown}", "lecture" },
        { "GET", $"/api/optical/measurements/{Unknown}", "lecture" },
        { "POST", "/api/optical/prescriptions", "ordonnances" },
        { "PUT", $"/api/optical/prescriptions/{Unknown}/draft", "ordonnances" },
        { "POST", $"/api/optical/prescriptions/{Unknown}/draft/confirm", "ordonnances" },
        { "POST", $"/api/optical/prescriptions/{Unknown}/rectifications", "ordonnances" },
        { "POST", $"/api/optical/prescriptions/{Unknown}/draft/discard", "ordonnances" },
        { "POST", $"/api/optical/prescriptions/{Unknown}/invalidate", "invalideur" },
        { "POST", "/api/optical/measurements", "mesures" },
        { "PUT", $"/api/optical/measurements/{Unknown}/draft", "mesures" },
        { "POST", $"/api/optical/measurements/{Unknown}/draft/confirm", "mesures" },
        { "POST", $"/api/optical/measurements/{Unknown}/rectifications", "mesures" },
        { "POST", $"/api/optical/measurements/{Unknown}/draft/discard", "mesures" },
        { "POST", $"/api/optical/measurements/{Unknown}/invalidate", "invalideur" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Anonymous_Gets401(string method, string path, string allowed)
    {
        _ = allowed;
        using var client = Https();

        using var response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EachRoute_RefusesEveryAccountWithoutTheExactCombination(string method, string path, string allowed)
    {
        string[] others = ["aucun", "admin", "clients", "auditeur", "sansclients", "lecture", "ordonnances", "mesures", "invalideur"];
        foreach (var userName in others.Where(u => u != allowed && !Satisfies(u, allowed)))
        {
            using var client = await AuthenticatedAsync(userName);
            using var response = await client.SendAsync(Request(method, path));
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{userName} {method} {path} → {(int)response.StatusCode}");
        }

        using var granted = await AuthenticatedAsync(allowed);
        using var accepted = await granted.SendAsync(Request(method, path));
        Assert.NotEqual(HttpStatusCode.Forbidden, accepted.StatusCode);
    }

    // « lecture » est inclus dans les comptes d'écriture et d'invalidation (même lecture) ; aucun autre recouvrement.
    private static bool Satisfies(string user, string allowed) =>
        allowed == "lecture" && user is "ordonnances" or "mesures" or "invalideur";

    [Fact]
    public async Task Forbidden_IsIndistinguishable_ForExistingAndUnknownRecords()
    {
        var created = await CreatePrescriptionAsync(confirm: true);
        using var client = await AuthenticatedAsync("auditeur");

        using var existing = await client.GetAsync($"/api/optical/prescriptions/{created.GetProperty("id").GetString()}");
        using var unknown = await client.GetAsync($"/api/optical/prescriptions/{Unknown}");

        Assert.Equal(HttpStatusCode.Forbidden, existing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unknown.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await existing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveAndConfirm_CreatesRevisionOne_NoStore_AndReplayReturnsTheSameRecord()
    {
        var clientId = _clients.Seed("Fictif", "Patient").Id;
        using var http = await AuthenticatedAsync("ordonnances");
        var body = CreateBody(clientId, confirm: true, Guid.CreateVersion7());

        using var first = await http.SendAsync(Json("POST", "/api/optical/prescriptions", body));
        using var replay = await http.SendAsync(Json("POST", "/api/optical/prescriptions", body));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("no-store", first.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var created = await ReadAsync(first);
        Assert.Equal(created.GetProperty("id").GetString(), (await ReadAsync(replay)).GetProperty("id").GetString());
        Assert.Equal("prescription", created.GetProperty("kind").GetString());
        Assert.Equal(1, created.GetProperty("lastConfirmed").GetProperty("revisionNo").GetInt32());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("openDraft").ValueKind);
        var od = created.GetProperty("lastConfirmed").GetProperty("content").GetProperty("od");
        Assert.Equal(SecretSphere, od.GetProperty("sphere").GetString());
        Assert.Equal(JsonValueKind.Null, od.GetProperty("cylinder").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("lastConfirmed").GetProperty("content").GetProperty("og").GetProperty("sphere").ValueKind);
        Assert.Single(_prescriptions.Records);
    }

    [Fact]
    public async Task SaveAndConfirm_Incomplete_Gets422_WithCodesOnly_AndCreatesNothing()
    {
        var clientId = _clients.Seed("Fictif", "Patient").Id;
        using var http = await AuthenticatedAsync("ordonnances");

        using var response = await http.SendAsync(Json("POST", "/api/optical/prescriptions", new
        {
            requestId = Guid.CreateVersion7(),
            clientId,
            confirm = true,
            content = new { od = new { sphere = "0,00" }, notes = SecretNote },
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretNote, text, StringComparison.Ordinal);
        var error = JsonDocument.Parse(text).RootElement;
        Assert.Equal("incomplete", error.GetProperty("code").GetString());
        Assert.Equal("sphere_zero_only", error.GetProperty("errors").GetProperty("correction")[0].GetString());
        Assert.Equal("required", error.GetProperty("errors").GetProperty("provenance.prescriberName")[0].GetString());
        Assert.Empty(_prescriptions.Records);
    }

    [Fact]
    public async Task Body_Strict_UnknownFieldsAndMissingEnvelopeAreRefused()
    {
        var clientId = _clients.Seed("Fictif", "Patient").Id;
        using var http = await AuthenticatedAsync("ordonnances");

        using var unknownField = await http.SendAsync(RawJson("POST", "/api/optical/prescriptions",
            "{\"requestId\":\"" + Guid.CreateVersion7() + "\",\"clientId\":\"" + clientId + "\",\"content\":{\"od\":{\"etat\":\"x\"}}}"));
        using var noRequestId = await http.SendAsync(Json("POST", "/api/optical/prescriptions", new { clientId, content = new { } }));
        using var wrongKind = await http.SendAsync(RawJson("POST", "/api/optical/prescriptions",
            "{\"requestId\":\"" + Guid.CreateVersion7() + "\",\"clientId\":\"" + clientId + "\",\"content\":{\"provenance\":{\"measuredOn\":\"2026-10-01\"}}}"));
        using var notANumber = await http.SendAsync(Json("POST", "/api/optical/prescriptions", new
        {
            requestId = Guid.CreateVersion7(),
            clientId,
            content = new { od = new { axis = "quatre-vingt" } },
        }));

        await AssertErrorAsync(unknownField, HttpStatusCode.BadRequest, "invalid_body");
        await AssertErrorAsync(noRequestId, HttpStatusCode.BadRequest, "request_id_required");
        await AssertErrorAsync(wrongKind, HttpStatusCode.BadRequest, "invalid_body");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notANumber.StatusCode);
        Assert.Equal("number_expected", (await ReadAsync(notANumber)).GetProperty("errors").GetProperty("od.axis")[0].GetString());
    }

    [Fact]
    public async Task Draft_ConflictsAndArchivedClient_Get409()
    {
        var client = _clients.Seed("Fictif", "Patient");
        var created = await CreatePrescriptionAsync(confirm: false, client.Id);
        var id = created.GetProperty("id").GetString();
        var version = created.GetProperty("version").GetUInt32();
        using var http = await AuthenticatedAsync("ordonnances");

        using var saved = await http.SendAsync(Json("PUT", $"/api/optical/prescriptions/{id}/draft", DraftBody(version, "-1,00")));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var stale = await http.SendAsync(Json("PUT", $"/api/optical/prescriptions/{id}/draft", DraftBody(version, "-2,00")));
        await AssertErrorAsync(stale, HttpStatusCode.Conflict, "version_conflict");

        client.Archive(client.CreatedByUserId, DateTimeOffset.UtcNow);
        var current = (await ReadAsync(saved)).GetProperty("version").GetUInt32();
        using var archived = await http.SendAsync(Json("PUT", $"/api/optical/prescriptions/{id}/draft", DraftBody(current, "-3,00")));
        await AssertErrorAsync(archived, HttpStatusCode.Conflict, "client_archived");

        using var reader = await AuthenticatedAsync("lecture");
        using var read = await reader.GetAsync($"/api/optical/prescriptions/{id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("archived", (await ReadAsync(read)).GetProperty("client").GetProperty("status").GetString());
    }

    [Fact]
    public async Task History_RequiresAuditRead_AndOpticalRead()
    {
        var created = await CreatePrescriptionAsync(confirm: true);
        var id = created.GetProperty("id").GetString();
        using var writer = await AuthenticatedAsync("complet");
        using var started = await writer.SendAsync(Json("POST", $"/api/optical/prescriptions/{id}/rectifications", new
        {
            requestId = Guid.CreateVersion7(),
            version = created.GetProperty("version").GetUInt32(),
            expectedRevisionNo = 1,
            reasonCode = "transcription_error",
        }));
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        using var discarded = await writer.SendAsync(Json("POST", $"/api/optical/prescriptions/{id}/draft/discard", new
        {
            requestId = Guid.CreateVersion7(),
            version = (await ReadAsync(started)).GetProperty("version").GetUInt32(),
        }));
        Assert.Equal(HttpStatusCode.OK, discarded.StatusCode);

        using var reader = await AuthenticatedAsync("lecture");
        var partial = await ReadAsync(await reader.GetAsync($"/api/optical/prescriptions/{id}"));
        var full = await ReadAsync(await writer.GetAsync($"/api/optical/prescriptions/{id}"));

        Assert.False(partial.GetProperty("historyAvailable").GetBoolean());
        Assert.Equal(0, partial.GetProperty("events").GetArrayLength());
        Assert.Equal(1, partial.GetProperty("revisions").GetArrayLength());
        Assert.True(full.GetProperty("historyAvailable").GetBoolean());
        Assert.Equal(2, full.GetProperty("revisions").GetArrayLength());
        Assert.Equal(
            ["prescription.created", "prescription.confirmed", "prescription.rectification_started", "prescription.rectification_discarded"],
            full.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Measurement_AndInvalidation_FullHttpFlow()
    {
        var clientId = _clients.Seed("Fictif", "Patient").Id;
        using var measurer = await AuthenticatedAsync("mesures");
        using var created = await measurer.SendAsync(Json("POST", "/api/optical/measurements", new
        {
            requestId = Guid.CreateVersion7(),
            clientId,
            confirm = true,
            content = new { provenance = new { measuredOn = "2026-10-27", measuredByUserId = _measurerId }, og = new { acuity = "9/10" } },
        }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = await ReadAsync(created);
        Assert.Equal("measurement", record.GetProperty("kind").GetString());

        using var forbidden = await measurer.SendAsync(Json("POST", $"/api/optical/measurements/{record.GetProperty("id").GetString()}/invalidate", new
        {
            requestId = Guid.CreateVersion7(),
            version = record.GetProperty("version").GetUInt32(),
            reasonCode = "duplicate",
        }));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var invalidator = await AuthenticatedAsync("invalideur");
        using var invalidated = await invalidator.SendAsync(Json("POST", $"/api/optical/measurements/{record.GetProperty("id").GetString()}/invalidate", new
        {
            requestId = Guid.CreateVersion7(),
            version = record.GetProperty("version").GetUInt32(),
            reasonCode = "other",
            comment = "commentaire fictif",
        }));
        Assert.Equal(HttpStatusCode.OK, invalidated.StatusCode);
        Assert.Equal("invalidated", (await ReadAsync(invalidated)).GetProperty("lifecycle").GetString());

        using var list = await invalidator.GetAsync($"/api/optical/clients/{clientId}/records");
        Assert.Equal(0, (await ReadAsync(list)).GetArrayLength());
        using var withInvalidated = await invalidator.GetAsync($"/api/optical/clients/{clientId}/records?includeInvalidated=true");
        Assert.Equal("invalidated", (await ReadAsync(withInvalidated))[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Lists_ValidateQueryStrictly()
    {
        using var http = await AuthenticatedAsync("lecture");

        using var ok = await http.GetAsync("/api/optical/records?kind=prescription&status=confirmed&sort=client&dir=asc&page=1&pageSize=8");
        using var unknownParameter = await http.GetAsync("/api/optical/records?etat=x");
        using var badKind = await http.GetAsync("/api/optical/records?kind=lens");
        using var unknownClient = await http.GetAsync($"/api/optical/clients/{Unknown}/records");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("no-store", ok.Headers.CacheControl?.ToString());
        await AssertErrorAsync(unknownParameter, HttpStatusCode.BadRequest, "invalid_query");
        await AssertErrorAsync(badKind, HttpStatusCode.BadRequest, "invalid_query");
        await AssertErrorAsync(unknownClient, HttpStatusCode.NotFound, "client_not_found");
    }

    [Fact]
    public async Task Logs_NeverContainOpticalValuesNamesOrNotes()
    {
        await CreatePrescriptionAsync(confirm: true);
        using var http = await AuthenticatedAsync("ordonnances");
        using var refused = await http.SendAsync(Json("POST", "/api/optical/prescriptions", CreateBody(_clients.Seed("Fictif", "Autre").Id, confirm: true, Guid.CreateVersion7(), sphere: "0,00")));

        Assert.NotEmpty(_logs);
        Assert.Contains(_logs, line => line.Contains("Dossier optique prescription created", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs, line =>
            line.Contains(SecretPrescriber, StringComparison.Ordinal) || line.Contains(SecretNote, StringComparison.Ordinal) || line.Contains(SecretSphere, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuditHistory_OpticalEventsRequireOpticalRead()
    {
        var clientId = _clients.Seed("Fictif", "Patient").Id;
        var actor = _identity.Users[0].Id;
        _auditStore.Events.Add(AuditEvent.Create(Guid.CreateVersion7(), DateTimeOffset.UtcNow, actor, clientId, AuditActions.ClientsModule, AuditActions.ClientCreated, AuditActions.ClientEntity, clientId, null, null, null));
        _auditStore.Events.Add(AuditEvent.Create(Guid.CreateVersion7(), DateTimeOffset.UtcNow, actor, clientId, AuditActions.OpticalModule, "prescription.confirmed", AuditActions.PrescriptionEntity, Guid.CreateVersion7(), null, null, null));
        _identity.AddUser(RealHashing.Create(), "auditoptique", Password, Permissions.ClientsRead, Permissions.AuditRead, Permissions.OpticalRead);

        using var auditOnly = await AuthenticatedAsync("auditeur");
        using var auditAndOptical = await AuthenticatedAsync("auditoptique");
        var filtered = await ReadAsync(await auditOnly.GetAsync($"/api/audit/clients/{clientId}"));
        var full = await ReadAsync(await auditAndOptical.GetAsync($"/api/audit/clients/{clientId}"));
        using var moduleRefused = await auditOnly.GetAsync($"/api/audit/clients/{clientId}?module=optical");

        Assert.Equal(["clients"], filtered.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("module").GetString()));
        Assert.Equal(2, full.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, moduleRefused.StatusCode);
    }

    // ---------- outils ----------

    private static object CreateBody(Guid clientId, bool confirm, Guid requestId, string sphere = SecretSphere) => new
    {
        requestId,
        clientId,
        confirm,
        content = new
        {
            provenance = new { prescriberName = SecretPrescriber, prescribedOn = "2026-10-20" },
            od = new { sphere },
            notes = SecretNote,
        },
    };

    private static object DraftBody(uint version, string sphere) => new
    {
        requestId = Guid.CreateVersion7(),
        version,
        content = new { provenance = new { prescriberName = SecretPrescriber, prescribedOn = "2026-10-20" }, od = new { sphere } },
    };

    private async Task<JsonElement> CreatePrescriptionAsync(bool confirm, Guid? clientId = null)
    {
        using var http = await AuthenticatedAsync("ordonnances");
        using var response = await http.SendAsync(Json("POST", "/api/optical/prescriptions", CreateBody(clientId ?? _clients.Seed("Fictif", "Patient").Id, confirm, Guid.CreateVersion7())));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static HttpRequestMessage Request(string method, string path) =>
        method == "GET" ? new HttpRequestMessage(HttpMethod.Get, path) : RawJson(method, path, "{}");

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
