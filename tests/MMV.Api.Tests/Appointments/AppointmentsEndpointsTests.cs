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
using MMV.Application.Appointments;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Appointments;

/// <summary>
/// Pipeline HTTP réel de MMV.Api pour /api/appointments (authentification, policies, HTTPS, paramètres du magasin,
/// validation, codes d'erreur, minimisation, journaux) ; seuls les stores et l'horloge sont remplacés par des doubles.
/// Données fictives. Magasin : Europe/Brussels, capacité d'accueil 1 ; « maintenant » = 15/07/2026 10:00 locale.
/// </summary>
public sealed class AppointmentsEndpointsTests : IDisposable
{
    private const string Password = "Mot-de-passe-test-1";

    private readonly InMemoryIdentityStore _identity = new();
    private readonly InMemoryAppointmentStore _appointments = new();
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero));
    private readonly List<WebApplicationFactory<Program>> _factories = [];
    private readonly Guid _client;
    private readonly Guid _archivedClient;
    private readonly Guid _alice;

    public AppointmentsEndpointsTests()
    {
        var hashing = RealHashing.Create();
        _identity.AddUser(hashing, "aucun", Password);
        _identity.AddUser(hashing, "admin", Password, Permissions.SystemAdmin);
        _identity.AddUser(hashing, "lecture", Password, Permissions.AppointmentsRead);
        _identity.AddUser(hashing, "clients", Password, Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsArchive);
        _identity.AddUser(hashing, "ecriture", Password, Permissions.AppointmentsRead, Permissions.AppointmentsWrite);
        _identity.AddUser(hashing, "annulation", Password, Permissions.AppointmentsRead, Permissions.AppointmentsCancel);
        var complete = _identity.AddUser(
            hashing, "complet", Password, Permissions.AppointmentsRead, Permissions.AppointmentsWrite, Permissions.AppointmentsCancel, Permissions.ClientsRead);
        _appointments.AddUser("Agent Complet", eligible: false, id: complete.Id);
        _alice = _appointments.AddUser("Alice Opticienne");
        _appointments.AddUser("Compte Désactivé", active: false);
        _appointments.AddUser("Compte Non Éligible", eligible: false);
        _client = _appointments.AddClient("Jeanne", "Martin");
        _archivedClient = _appointments.AddClient("Nadia", "Cherkaoui", archived: true);
    }

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
    }

    /// <summary>
    /// Sans paramètres ni configuration : magasin explicitement NON configuré (indépendant des secrets du poste).
    /// Avec <paramref name="configuration"/> : chemin réel de lecture de la section « Appointments ».
    /// </summary>
    private WebApplicationFactory<Program> Factory(AppointmentSettings? settings = null, IReadOnlyDictionary<string, string?>? configuration = null)
    {
        settings ??= configuration is null ? AppointmentSettings.NotConfigured("Magasin non configuré (test).") : null;
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
            foreach (var (key, value) in configuration ?? new Dictionary<string, string?>())
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = null }));
            builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(_logs)));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentityStore>();
                services.AddScoped<IIdentityStore>(_ => _identity);
                services.RemoveAll<IAppointmentStore>();
                services.AddScoped<IAppointmentStore>(_ => _appointments);
                if (settings is not null)
                {
                    services.RemoveAll<AppointmentSettings>();
                    services.AddSingleton(settings);
                }

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
            });
        });
        _factories.Add(factory);
        return factory;
    }

    private WebApplicationFactory<Program> Configured(int? capacity = 1) => Factory(AppointmentTestSettings.Configured("Europe/Brussels", capacity));

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", "/api/appointments/options" },
        { "GET", "/api/appointments?from=2026-07-16T00:00:00Z&to=2026-07-17T00:00:00Z" },
        { "GET", $"/api/appointments/{Guid.Empty}" },
        { "GET", $"/api/appointments/by-client/{Guid.Empty}" },
        { "POST", "/api/appointments" },
        { "PUT", $"/api/appointments/{Guid.Empty}" },
        { "POST", $"/api/appointments/{Guid.Empty}/cancel" },
        { "POST", $"/api/appointments/{Guid.Empty}/restore" },
    };

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Anonymous_Gets401(string method, string path)
    {
        using var client = Https(Configured());

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task NoAppointmentPermission_AndSystemAdmin_Get403(string method, string path)
    {
        var factory = Configured();
        foreach (var account in new[] { "aucun", "admin" })
        {
            using var client = await AuthenticatedAsync(factory, account);
            using var response = await client.SendAsync(Json(method, path, new { }));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task PermissionsAreDistinct_ReadWriteCancel()
    {
        var factory = Configured();
        var existing = _appointments.Seed(_client, _alice, At(10), 30);
        using var read = await AuthenticatedAsync(factory, "lecture");
        using var write = await AuthenticatedAsync(factory, "ecriture");
        using var cancel = await AuthenticatedAsync(factory, "annulation");

        using (var response = await read.GetAsync($"/api/appointments/{existing.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await AssertStatusAsync(await read.SendAsync(Json("POST", "/api/appointments", Input(At(14)))), HttpStatusCode.Forbidden);
        await AssertStatusAsync(await write.SendAsync(Json("POST", $"/api/appointments/{existing.Id}/cancel", new { version = existing.Version })), HttpStatusCode.Forbidden);
        await AssertStatusAsync(await cancel.SendAsync(Json("PUT", $"/api/appointments/{existing.Id}", Input(At(11), version: existing.Version))), HttpStatusCode.Forbidden);
        await AssertStatusAsync(await cancel.SendAsync(Json("POST", $"/api/appointments/{existing.Id}/cancel", new { version = existing.Version })), HttpStatusCode.OK);
    }

    [Fact]
    public async Task StoreNotConfigured_Gets503AfterAuthorization()
    {
        var factory = Factory();
        using var client = await AuthenticatedAsync(factory, "complet");

        await AssertErrorAsync(await client.GetAsync("/api/appointments/options"), HttpStatusCode.ServiceUnavailable, "appointments_not_configured");
        await AssertErrorAsync(await client.SendAsync(Json("POST", "/api/appointments", Input(At(14)))), HttpStatusCode.ServiceUnavailable, "appointments_not_configured");
        using var anonymous = Https(factory);
        await AssertStatusAsync(await anonymous.GetAsync("/api/appointments/options"), HttpStatusCode.Unauthorized);
        using var none = await AuthenticatedAsync(factory, "aucun");
        await AssertStatusAsync(await none.GetAsync("/api/appointments/options"), HttpStatusCode.Forbidden);
        Assert.Contains(_logs, line => line.Contains("Rendez-vous : service non configuré", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Options_ComeFromTheStoreConfiguration()
    {
        var factory = Factory(configuration: new Dictionary<string, string?>
        {
            ["Appointments:TimeZone"] = "Africa/Casablanca",
            ["Appointments:DisplayStartTime"] = "08:30",
            ["Appointments:DisplayEndTime"] = "21:00",
            ["Appointments:HiddenWeekdays:0"] = "5",
            ["Appointments:ReceptionCapacity"] = "2",
        });
        using var client = await AuthenticatedAsync(factory, "lecture");

        using var response = await client.GetAsync("/api/appointments/options");
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Africa/Casablanca", body.GetProperty("timeZone").GetString());
        Assert.Equal([15, 30, 45, 60, 90, 120], body.GetProperty("durationsMinutes").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(5, body.GetProperty("startStepMinutes").GetInt32());
        Assert.Equal(2, body.GetProperty("receptionCapacity").GetInt32());
        Assert.Equal("08:30", body.GetProperty("displayStartTime").GetString());
        Assert.Equal("21:00", body.GetProperty("displayEndTime").GetString());
        Assert.Equal([5], body.GetProperty("hiddenWeekdays").EnumerateArray().Select(e => e.GetInt32()));
        var reasons = body.GetProperty("reasons").EnumerateArray().ToList();
        Assert.Equal(7, reasons.Count);
        Assert.Equal("""{"code":"retrait_commande","label":"Retrait de commande","responsibleRequired":false}""", reasons[4].GetRawText());
        Assert.Equal("""{"code":"conseil_devis","label":"Conseil / Devis","responsibleRequired":true}""", reasons[0].GetRawText());
        // Seuls les collaborateurs actifs ET éligibles ; identifiant et nom affiché seulement.
        var responsibles = body.GetProperty("responsibles").EnumerateArray().ToList();
        Assert.Equal(["Alice Opticienne"], responsibles.Select(r => r.GetProperty("displayName").GetString()));
        Assert.Equal(["displayName", "id"], PropertyNames(responsibles[0]));
    }

    [Fact]
    public async Task Options_WithoutCapacity_ExposeNull()
    {
        using var client = await AuthenticatedAsync(Configured(capacity: null), "lecture");

        var body = await ReadAsync(await client.GetAsync("/api/appointments/options"));

        Assert.Equal(JsonValueKind.Null, body.GetProperty("receptionCapacity").ValueKind);
        Assert.Empty(body.GetProperty("hiddenWeekdays").EnumerateArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-07-16T00:00:00Z")]
    [InlineData("?from=2026-07-16T00:00:00%2B02:00&to=2026-07-17T00:00:00Z")]
    [InlineData("?from=2026-07-17T00:00:00Z&to=2026-07-16T00:00:00Z")]
    [InlineData("?from=2026-07-01T00:00:00Z&to=2026-08-02T00:00:00Z")]
    [InlineData("?from=2026-07-16T00:00:00Z&to=2026-07-17T00:00:00Z&status=cancelled")]
    [InlineData("?from=2026-07-16T00:00:00Z&to=2026-07-17T00:00:00Z&responsibleId=x")]
    [InlineData("?from=2026-07-16T00:00:00Z&from=2026-07-16T00:00:00Z&to=2026-07-17T00:00:00Z")]
    public async Task List_InvalidQuery_Gets400(string query)
    {
        using var client = await AuthenticatedAsync(Configured(), "lecture");

        await AssertErrorAsync(await client.GetAsync($"/api/appointments{query}"), HttpStatusCode.BadRequest, "invalid_query");
    }

    [Fact]
    public async Task List_ReturnsOverlappingAppointmentsWithoutContactDataOrNotes()
    {
        _appointments.Seed(_client, _alice, At(10), 30);
        _appointments.Seed(_client, null, At(11), 30, "retrait_commande", cancelled: true);
        _appointments.Seed(_client, _alice, At(10).AddDays(2), 30);
        using var client = await AuthenticatedAsync(Configured(), "lecture");

        var planned = await ReadAsync(await client.GetAsync("/api/appointments?from=2026-07-15T22:00:00Z&to=2026-07-16T22:00:00Z"));
        var all = await ReadAsync(await client.GetAsync("/api/appointments?from=2026-07-15T22:00:00Z&to=2026-07-16T22:00:00Z&status=all"));

        var item = Assert.Single(planned.GetProperty("items").EnumerateArray());
        Assert.Equal(
            ["clientArchived", "clientFirstName", "clientId", "clientLastName", "clientReference", "endsAtUtc", "id", "reasonCode", "responsibleDisplayName", "responsibleUserId", "startsAtUtc", "status", "version"],
            PropertyNames(item));
        Assert.Equal("Alice Opticienne", item.GetProperty("responsibleDisplayName").GetString());
        Assert.Equal(2, all.GetProperty("items").GetArrayLength());
        // B2-REV-03 : décalages du serveur sur la période demandée (juillet à Bruxelles : UTC+2, un seul segment).
        var offset = Assert.Single(planned.GetProperty("timeZoneOffsets").EnumerateArray());
        Assert.Equal(["fromUtc", "offsetMinutes", "toUtc"], PropertyNames(offset));
        var midnight = new DateTimeOffset(2026, 7, 15, 22, 0, 0, TimeSpan.Zero);
        Assert.Equal((midnight, midnight.AddDays(1), 120), (offset.GetProperty("fromUtc").GetDateTimeOffset(), offset.GetProperty("toUtc").GetDateTimeOffset(), offset.GetProperty("offsetMinutes").GetInt32()));
    }

    [Fact]
    public async Task List_AcrossTheAutumnTransition_ServesBothOffsetsOfTheServer()
    {
        using var client = await AuthenticatedAsync(Configured(), "lecture");

        var week = await ReadAsync(await client.GetAsync("/api/appointments?from=2026-10-18T22:00:00Z&to=2026-10-25T23:00:00Z"));

        var offsets = week.GetProperty("timeZoneOffsets").EnumerateArray()
            .Select(o => (o.GetProperty("fromUtc").GetString(), o.GetProperty("toUtc").GetString(), o.GetProperty("offsetMinutes").GetInt32()))
            .ToArray();
        Assert.Equal(
            [("2026-10-18T22:00:00+00:00", "2026-10-25T01:00:00+00:00", 120), ("2026-10-25T01:00:00+00:00", "2026-10-25T23:00:00+00:00", 60)],
            offsets);
    }

    [Fact]
    public async Task Write_WithALocalTimeThatDiffersFromTheServer_Is422AndWritesNothing()
    {
        using var client = await AuthenticatedAsync(Configured(), "complet");
        var body = Input(At(10, 30));
        body["startsAtLocal"] = "2026-07-16T09:30";

        using var response = await client.SendAsync(Json("POST", "/api/appointments", body));
        var error = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("time_zone_mismatch", error.GetProperty("errors").GetProperty("startsAtLocal")[0].GetString());
        Assert.Empty(_appointments.Appointments);

        body.Remove("startsAtLocal");
        using var missing = await client.SendAsync(Json("POST", "/api/appointments", body));
        Assert.Equal("required", (await ReadAsync(missing)).GetProperty("errors").GetProperty("startsAtLocal")[0].GetString());
    }

    [Fact]
    public async Task Update_OfAStartedAppointment_RefusesBookingChangesButRecordsTheNotesAuthor()
    {
        // Mercredi 15/07 09:30–10:30 à Bruxelles : commencé (« maintenant » = 10:00), encore en cours.
        var start = At(9, 30).AddDays(-1);
        var started = _appointments.Seed(_client, _alice, start, 60);
        using var client = await AuthenticatedAsync(Configured(), "complet");

        await AssertErrorAsync(
            await client.SendAsync(Json("PUT", $"/api/appointments/{started.Id}", Input(At(14), version: started.Version))), HttpStatusCode.Conflict, "appointment_past");
        await AssertErrorAsync(
            await client.SendAsync(Json("PUT", $"/api/appointments/{started.Id}", Input(start, version: started.Version, reason: "sav_reparation", unassigned: true))),
            HttpStatusCode.Conflict, "appointment_past");

        var corrected = Input(start, version: started.Version, notes: "Monture remise");
        corrected["durationMinutes"] = 60;
        using var response = await client.SendAsync(Json("PUT", $"/api/appointments/{started.Id}", corrected));
        var detail = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Monture remise", detail.GetProperty("notes").GetString());
        Assert.Equal("Agent Complet", detail.GetProperty("updatedByDisplayName").GetString());
        Assert.Equal(_clock.GetUtcNow(), detail.GetProperty("updatedAtUtc").GetDateTimeOffset());
        Assert.Equal(start, detail.GetProperty("startsAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task Create_Returns201WithLocationAndDetail()
    {
        using var client = await AuthenticatedAsync(Configured(), "complet");

        using var response = await client.SendAsync(Json("POST", "/api/appointments", Input(At(10, 30), notes: "Prévoir l'étui")));
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/appointments/{body.GetProperty("id").GetString()}", response.Headers.Location?.OriginalString);
        Assert.Equal("planned", body.GetProperty("status").GetString());
        Assert.Equal(At(10, 30), body.GetProperty("startsAtUtc").GetDateTimeOffset());
        Assert.Equal(At(11), body.GetProperty("endsAtUtc").GetDateTimeOffset());
        Assert.Equal("Prévoir l'étui", body.GetProperty("notes").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("cancelledByDisplayName").ValueKind);
        // Journal : identifiants techniques seulement, jamais nom de client ni note.
        Assert.Contains(_logs, line => line.Contains("Rendez-vous created", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs, line => line.Contains("Martin", StringComparison.Ordinal) || line.Contains("étui", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_ValidationErrorsArePerField()
    {
        using var client = await AuthenticatedAsync(Configured(capacity: null), "complet");

        using var response = await client.SendAsync(Json("POST", "/api/appointments", new
        {
            clientId = _client,
            responsibleUserId = (Guid?)null,
            startsAtUtc = "2026-07-16T08:07:00Z",
            durationMinutes = 20,
            reasonCode = "retrait_commande",
            notes = (string?)null,
        }));
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        Assert.Equal("not_allowed", body.GetProperty("errors").GetProperty("durationMinutes")[0].GetString());
        Assert.Equal("reception_not_configured", body.GetProperty("errors").GetProperty("responsibleUserId")[0].GetString());
    }

    [Fact]
    public async Task Create_ConflictCapacityAndArchivedClientAre409()
    {
        _appointments.Seed(_client, _alice, At(10), 30);
        _appointments.Seed(_client, null, At(14), 30, "retrait_commande");
        using var client = await AuthenticatedAsync(Configured(capacity: 1), "complet");

        await AssertErrorAsync(await client.SendAsync(Json("POST", "/api/appointments", Input(At(10, 15)))), HttpStatusCode.Conflict, "appointment_conflict");
        await AssertErrorAsync(await client.SendAsync(Json("POST", "/api/appointments", Input(At(14, 15), reason: "sav_reparation", unassigned: true))), HttpStatusCode.Conflict, "reception_capacity_full");
        await AssertErrorAsync(await client.SendAsync(Json("POST", "/api/appointments", Input(At(16), clientId: _archivedClient))), HttpStatusCode.Conflict, "client_archived");
        await AssertStatusAsync(await client.SendAsync(Json("POST", "/api/appointments", Input(At(10, 30)))), HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("""{"clientId":"x"}""")]
    [InlineData("""{"durationMinutes":"30"}""")]
    [InlineData("""{"socialSecurityNumber":"1"}""")]
    [InlineData("""[]""")]
    public async Task Create_StrictBody_Gets400(string json)
    {
        using var client = await AuthenticatedAsync(Configured(), "complet");

        await AssertErrorAsync(await client.SendAsync(RawJson("POST", "/api/appointments", json)), HttpStatusCode.BadRequest, "invalid_body");
    }

    [Fact]
    public async Task Update_RequiresVersionAndReturnsTheNewOne()
    {
        var existing = _appointments.Seed(_client, _alice, At(10), 30);
        using var client = await AuthenticatedAsync(Configured(), "complet");

        await AssertErrorAsync(await client.SendAsync(Json("PUT", $"/api/appointments/{existing.Id}", Input(At(11)))), HttpStatusCode.BadRequest, "version_required");
        await AssertErrorAsync(await client.SendAsync(Json("PUT", $"/api/appointments/{existing.Id}", Input(At(11), version: existing.Version + 1))), HttpStatusCode.Conflict, "version_conflict");
        await AssertErrorAsync(await client.SendAsync(Json("PUT", $"/api/appointments/{Guid.CreateVersion7()}", Input(At(11), version: 1))), HttpStatusCode.NotFound, "appointment_not_found");

        using var moved = await client.SendAsync(Json("PUT", $"/api/appointments/{existing.Id}", Input(At(11), version: existing.Version)));
        var body = await ReadAsync(moved);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(At(11), body.GetProperty("startsAtUtc").GetDateTimeOffset());
        Assert.NotEqual(existing.Version - 1, body.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task CancelThenRestore_KeepsAuthorAndChecksTheSlotAgain()
    {
        var existing = _appointments.Seed(_client, _alice, At(10), 30);
        using var client = await AuthenticatedAsync(Configured(), "complet");

        using var cancelled = await client.SendAsync(Json("POST", $"/api/appointments/{existing.Id}/cancel", new { version = existing.Version, reason = "Empêchement" }));
        var body = await ReadAsync(cancelled);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", body.GetProperty("status").GetString());
        Assert.Equal("Empêchement", body.GetProperty("cancellationReason").GetString());
        Assert.Equal("Agent Complet", body.GetProperty("cancelledByDisplayName").GetString());
        var version = body.GetProperty("version").GetUInt32();

        await AssertErrorAsync(await client.SendAsync(Json("POST", $"/api/appointments/{existing.Id}/cancel", new { version })), HttpStatusCode.Conflict, "appointment_cancelled");

        // Le créneau est repris entre-temps : rétablissement refusé.
        await AssertStatusAsync(await client.SendAsync(Json("POST", "/api/appointments", Input(At(10)))), HttpStatusCode.Created);
        await AssertErrorAsync(await client.SendAsync(Json("POST", $"/api/appointments/{existing.Id}/restore", new { version })), HttpStatusCode.Conflict, "appointment_conflict");
        await AssertErrorAsync(await client.SendAsync(Json("POST", $"/api/appointments/{existing.Id}/restore", new { })), HttpStatusCode.BadRequest, "version_required");
        Assert.DoesNotContain(_logs, line => line.Contains("Empêchement", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancel_ReasonTooLong_Gets422()
    {
        var existing = _appointments.Seed(_client, _alice, At(10), 30);
        using var client = await AuthenticatedAsync(Configured(), "complet");

        using var response = await client.SendAsync(Json("POST", $"/api/appointments/{existing.Id}/cancel", new { version = existing.Version, reason = new string('r', 201) }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("too_long", (await ReadAsync(response)).GetProperty("errors").GetProperty("reason")[0].GetString());
    }

    [Fact]
    public async Task Delete_IsNotAllowed()
    {
        var existing = _appointments.Seed(_client, _alice, At(10), 30);
        using var client = await AuthenticatedAsync(Configured(), "complet");

        using var response = await client.DeleteAsync($"/api/appointments/{existing.Id}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Single(_appointments.Appointments);
    }

    [Fact]
    public async Task Http_IsRefusedEvenWithAValidSession()
    {
        var factory = Configured();
        using var https = await AuthenticatedAsync(factory, "complet");
        using var http = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        http.DefaultRequestHeaders.Authorization = https.DefaultRequestHeaders.Authorization;

        await AssertErrorAsync(await http.GetAsync("/api/appointments/options"), HttpStatusCode.Forbidden, "https_required");
    }

    /// <summary>Jeudi 16/07/2026 à <paramref name="hour"/>:<paramref name="minute"/> heure de Bruxelles (UTC+2).</summary>
    [Fact]
    public async Task ClientHistory_SplitsUpcomingPastCancelled_WithServerWallTimesAndMinimizedRows()
    {
        // « Maintenant » = 15/07/2026 10:00 (Bruxelles).
        var past = _appointments.Seed(_client, _alice, new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero), 30);
        var inProgress = _appointments.Seed(_client, null, new DateTimeOffset(2026, 7, 15, 7, 45, 0, TimeSpan.Zero), 30, "retrait_commande");
        var future = _appointments.Seed(_client, _alice, At(10), 45);
        var cancelled = _appointments.Seed(_client, _alice, At(14), 30, cancelled: true);
        var other = _appointments.Seed(_archivedClient, _alice, At(11), 30);
        using var client = await AuthenticatedAsync(Configured(), "lecture");

        var upcoming = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}"));
        Assert.Equal([inProgress.Id, future.Id], upcoming.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal("""{"upcoming":2,"past":1,"cancelled":1}""", upcoming.GetProperty("counts").GetRawText());
        Assert.Equal(2, upcoming.GetProperty("totalCount").GetInt32());
        var row = upcoming.GetProperty("items")[1];
        Assert.Equal("2026-07-16T10:00", row.GetProperty("startsAtLocal").GetString());
        Assert.Equal("2026-07-16T10:45", row.GetProperty("endsAtLocal").GetString());
        Assert.Equal("Alice Opticienne", row.GetProperty("responsibleDisplayName").GetString());
        Assert.Equal("planned", row.GetProperty("status").GetString());
        // Minimisation : ni nom ni coordonnées du client, ni notes, ni motif d'annulation, ni version.
        Assert.Equal(
            ["cancelledAtUtc", "endsAtLocal", "endsAtUtc", "id", "reasonCode", "responsibleDisplayName", "responsibleUserId", "startsAtLocal", "startsAtUtc", "status"],
            PropertyNames(row));

        var pastPage = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}?scope=past"));
        Assert.Equal([past.Id], pastPage.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        var cancelledPage = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}?scope=cancelled"));
        var cancelledRow = Assert.Single(cancelledPage.GetProperty("items").EnumerateArray());
        Assert.Equal(cancelled.Id, cancelledRow.GetProperty("id").GetGuid());
        Assert.Equal("cancelled", cancelledRow.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, cancelledRow.GetProperty("cancelledAtUtc").ValueKind);

        // Tous : du plus récent au plus ancien par défaut ; ordre croissant sur demande ; autre client jamais inclus.
        var all = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}?scope=all"));
        Assert.Equal([cancelled.Id, future.Id, inProgress.Id, past.Id], all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        var ascending = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}?scope=all&dir=asc"));
        Assert.Equal([past.Id, inProgress.Id, future.Id, cancelled.Id], ascending.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(other.Id, all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task ClientHistory_PaginatesAndReturnsEmptyPageForUnknownClient()
    {
        var ids = Enumerable.Range(0, 5).Select(day => _appointments.Seed(_client, _alice, At(10).AddDays(day), 30).Id).ToList();
        using var client = await AuthenticatedAsync(Configured(), "lecture");

        var second = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}?pageSize=2&page=2"));
        Assert.Equal(ids[2..4], second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal(5, second.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.Equal(2, second.GetProperty("pageSize").GetInt32());
        var beyond = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}?pageSize=2&page=9"));
        Assert.Empty(beyond.GetProperty("items").EnumerateArray());

        var unknown = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{Guid.NewGuid()}"));
        Assert.Empty(unknown.GetProperty("items").EnumerateArray());
        Assert.Equal("""{"upcoming":0,"past":0,"cancelled":0}""", unknown.GetProperty("counts").GetRawText());
    }

    [Fact]
    public async Task ClientHistory_RequiresAppointmentsRead_ClientsPermissionsAreNotEnough()
    {
        _appointments.Seed(_client, _alice, At(10), 30);
        var factory = Configured();
        foreach (var account in new[] { "clients", "admin", "aucun" })
        {
            using var client = await AuthenticatedAsync(factory, account);
            using var response = await client.GetAsync($"/api/appointments/by-client/{_client}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("Jeanne", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        using var notConfigured = await AuthenticatedAsync(Factory(), "lecture");
        await AssertErrorAsync(await notConfigured.GetAsync($"/api/appointments/by-client/{_client}"), HttpStatusCode.ServiceUnavailable, "appointments_not_configured");
    }

    [Theory]
    [InlineData("?scope=planned")]
    [InlineData("?dir=up")]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=1e2")]
    [InlineData("?q=martin")]
    [InlineData("?scope=past&scope=all")]
    public async Task ClientHistory_InvalidQuery_Gets400(string query)
    {
        using var client = await AuthenticatedAsync(Configured(), "lecture");

        await AssertErrorAsync(await client.GetAsync($"/api/appointments/by-client/{_client}{query}"), HttpStatusCode.BadRequest, "invalid_query");
    }

    [Fact]
    public async Task ClientHistory_WallTimesFollowTheStoreTimeZone_Morocco()
    {
        _appointments.Seed(_client, _alice, At(10), 30);
        using var client = await AuthenticatedAsync(Factory(AppointmentTestSettings.Configured("Africa/Casablanca")), "lecture");

        var page = await ReadAsync(await client.GetAsync($"/api/appointments/by-client/{_client}"));

        // 08:00 UTC = 09:00 à Casablanca (UTC+1) le 16/07/2026.
        Assert.Equal("2026-07-16T09:00", page.GetProperty("items")[0].GetProperty("startsAtLocal").GetString());
    }

    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 7, 16, hour - 2, minute, 0, TimeSpan.Zero);

    /// <summary>Corps de création (sans <c>version</c> : champ inconnu refusé) ou de modification (avec).</summary>
    private Dictionary<string, object?> Input(DateTimeOffset start, uint? version = null, string? notes = null, Guid? clientId = null, string reason = "conseil_devis", bool unassigned = false)
    {
        var body = new Dictionary<string, object?>
        {
            ["clientId"] = clientId ?? _client,
            ["responsibleUserId"] = unassigned ? null : _alice,
            ["startsAtUtc"] = start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["startsAtLocal"] = AppointmentSchedule.LocalWallTime(TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"), start),
            ["durationMinutes"] = 30,
            ["reasonCode"] = reason,
            ["notes"] = notes,
        };
        if (version is not null)
        {
            body["version"] = version;
        }

        return body;
    }

    private static HttpClient Https(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<HttpClient> AuthenticatedAsync(WebApplicationFactory<Program> factory, string userName)
    {
        var client = Https(factory);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { userName, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await ReadAsync(login)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpRequestMessage Json(string method, string path, object body) =>
        new(new HttpMethod(method), path) { Content = method == "GET" ? null : JsonContent.Create(body) };

    private static HttpRequestMessage RawJson(string method, string path, string json) =>
        new(new HttpMethod(method), path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
        }
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
