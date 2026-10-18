using System.Globalization;
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
using MMV.Api.Tests.Appointments;
using MMV.Api.Tests.Clients;
using MMV.Api.Tests.Identity;
using MMV.Application.Appointments;
using MMV.Application.Audit;
using MMV.Application.Clients;
using MMV.Application.Identity;
using MMV.Domain.Audit;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Audit;

/// <summary>
/// Journal d'audit (03-D2) au niveau des services : un événement par action RÉUSSIE, auteur = compte connecté (jamais
/// le responsable), aucune trace d'un refus ni d'un échec d'enregistrement, valeurs personnelles masquées. Données
/// fictives. L'atomicité réelle (même transaction PostgreSQL, ROLLBACK) est prouvée par MMV.PostgreSql.Tests.
/// </summary>
public sealed class AuditRecordingTests
{
    private static readonly Guid Author = Guid.Parse("0199a000-0000-7000-8000-00000000000a");
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero));

    private static ClientInput Input(string firstName = "Jeanne", string lastName = "Martin", string? phone = null, string? notes = null) =>
        new(firstName, lastName, null, phone, null, null, null, null, notes);

    [Fact]
    public async Task ClientLifecycle_RecordsOneEventPerSuccessfulAction_WithTheActorAndFieldNamesOnly()
    {
        var store = new InMemoryClientStore(_clock);
        var service = new ClientService(store, _clock, store.Audit);

        var created = (await service.CreateAsync(Input(phone: "06 12 34 56 78", notes: "Note confidentielle"), Author, CancellationToken.None)).Client!;
        var updated = (await service.UpdateAsync(created.Id, Input(firstName: "Jeanne-Marie", phone: "06 99 99 99 99", notes: "Note confidentielle"), created.Version, Author, CancellationToken.None)).Client!;
        var archived = (await service.ArchiveAsync(updated.Id, updated.Version, Author, CancellationToken.None)).Client!;
        await service.ReactivateAsync(archived.Id, archived.Version, Author, CancellationToken.None);

        var events = store.Audit.For(created.Id);
        Assert.Equal([AuditActions.ClientCreated, AuditActions.ClientUpdated, AuditActions.ClientArchived, AuditActions.ClientReactivated], events.Select(e => e.Action));
        Assert.All(events, e =>
        {
            Assert.Equal(Author, e.ActorUserId);
            Assert.Equal(AuditActions.ClientsModule, e.Module);
            Assert.Equal(AuditActions.ClientEntity, e.EntityType);
            Assert.Equal(created.Id, e.EntityId);
            Assert.All(e.Changes, c => Assert.True(c.Masked && c.Old is null && c.New is null, c.Field));
        });
        Assert.Equal(["firstName", "lastName", "phone", "notes"], events[0].Changes.Select(c => c.Field));
        Assert.Equal(["firstName", "phone"], events[1].Changes.Select(c => c.Field));
        var json = string.Join(" ", events.Select(e => AuditChanges.Serialize(e.Changes)));
        foreach (var personal in new[] { "Jeanne", "Martin", "06 12", "06 99", "confidentielle" })
        {
            Assert.DoesNotContain(personal, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ClientRefusalsAndFailedSaves_RecordNothing()
    {
        var store = new InMemoryClientStore(_clock);
        var service = new ClientService(store, _clock, store.Audit);
        var client = store.Seed("Jeanne", "Martin");

        Assert.Equal(ClientWriteStatus.ValidationFailed, (await service.CreateAsync(Input(firstName: ""), Author, CancellationToken.None)).Status);
        Assert.Equal(ClientWriteStatus.VersionConflict, (await service.UpdateAsync(client.Id, Input(firstName: "Autre"), client.Version + 1, Author, CancellationToken.None)).Status);
        // Modification sans changement : enregistrée (auteur, date) mais aucune action à journaliser.
        Assert.Equal(ClientWriteStatus.Success, (await service.UpdateAsync(client.Id, Input(), client.Version, Author, CancellationToken.None)).Status);
        // Écriture concurrente détectée À L'ENREGISTREMENT : l'événement préparé est abandonné avec elle (en dernier :
        // le double partage l'objet modifié, là où EF vide son suivi).
        store.FailNextSave = true;
        Assert.Equal(ClientWriteStatus.VersionConflict, (await service.UpdateAsync(client.Id, Input(firstName: "Autre"), client.Version, Author, CancellationToken.None)).Status);
        Assert.Empty(store.Audit.Committed);
    }

    private sealed class AppointmentBench
    {
        public InMemoryAppointmentStore Store { get; } = new();

        public Guid Client { get; }

        public Guid Other { get; }

        public Guid Alice { get; }

        public Guid Bruno { get; }

        public AppointmentService Service { get; }

        public AppointmentBench(ManualClock clock)
        {
            Client = Store.AddClient("Jeanne", "Martin");
            Other = Store.AddClient("Omar", "Benali");
            Alice = Store.AddUser("Alice Opticienne");
            Bruno = Store.AddUser("Bruno Opticien");
            Service = new AppointmentService(Store, AppointmentTestSettings.Configured(), clock, Store.Audit);
        }

        /// <summary>16/07/2026 à <paramref name="hour"/>:<paramref name="minute"/> heure de Bruxelles (UTC+2).</summary>
        public static DateTimeOffset At(int hour, int minute = 0) => new(2026, 7, 16, hour - 2, minute, 0, TimeSpan.Zero);

        public AppointmentInput Input(Guid? responsible, DateTimeOffset start, int minutes = 30, string reason = "conseil_devis", string? notes = null, Guid? client = null) =>
            new(client ?? Client, responsible, start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                AppointmentSchedule.LocalWallTime(TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"), start), minutes, reason, notes);
    }

    [Fact]
    public async Task AppointmentCreation_NamesTheResponsibleInTheFields_AndTheActorAsAuthor()
    {
        var bench = new AppointmentBench(_clock);

        var created = (await bench.Service.CreateAsync(bench.Input(bench.Alice, AppointmentBench.At(10), notes: "Note interne"), Author, CancellationToken.None)).Appointment!;

        var e = Assert.Single(bench.Store.Audit.Committed);
        Assert.Equal(AuditActions.AppointmentCreated, e.Action);
        Assert.Equal(Author, e.ActorUserId);
        Assert.NotEqual(bench.Alice, e.ActorUserId);
        Assert.Equal(bench.Client, e.ClientId);
        Assert.Equal(created.Appointment.Id, e.EntityId);
        Assert.Equal(
            ["startsAtUtc=2026-07-16T08:00:00Z", "durationMinutes=30", "reasonCode=conseil_devis", $"responsibleUserId={bench.Alice}", "notes=masqué"],
            e.Changes.Select(c => c.Masked ? $"{c.Field}=masqué" : $"{c.Field}={c.New}"));
    }

    [Fact]
    public async Task AppointmentUpdate_IsSplitIntoMoveResponsibleChangeAndModification()
    {
        var bench = new AppointmentBench(_clock);
        var appointment = (await bench.Service.CreateAsync(bench.Input(bench.Alice, AppointmentBench.At(10)), Author, CancellationToken.None)).Appointment!.Appointment;
        bench.Store.Audit.Committed.Clear();

        var moved = (await bench.Service.UpdateAsync(appointment.Id, bench.Input(bench.Alice, AppointmentBench.At(11), 45), appointment.Version, Author, CancellationToken.None)).Appointment!.Appointment;
        var e = Assert.Single(bench.Store.Audit.Committed);
        Assert.Equal(AuditActions.AppointmentMoved, e.Action);
        Assert.Equal(
            [("startsAtUtc", "2026-07-16T08:00:00Z", "2026-07-16T09:00:00Z"), ("durationMinutes", "30", "45")],
            e.Changes.Select(c => (c.Field, c.Old, c.New)));
        bench.Store.Audit.Committed.Clear();

        // Une seule saisie : nouvelle heure, autre responsable, motif et notes → trois événements, dans cet ordre.
        await bench.Service.UpdateAsync(moved.Id, bench.Input(bench.Bruno, AppointmentBench.At(14), 45, "prise_mesures", "Note"), moved.Version, Author, CancellationToken.None);
        Assert.Equal(
            [AuditActions.AppointmentMoved, AuditActions.AppointmentResponsibleChanged, AuditActions.AppointmentUpdated],
            bench.Store.Audit.Committed.Select(r => r.Action));
        var responsible = bench.Store.Audit.Committed[1].Changes.Single();
        Assert.Equal(("responsibleUserId", bench.Alice.ToString(), bench.Bruno.ToString(), false), (responsible.Field, responsible.Old, responsible.New, responsible.Masked));
        Assert.Equal(["reasonCode:conseil_devis>prise_mesures", "notes:masqué"],
            bench.Store.Audit.Committed[2].Changes.Select(c => c.Masked ? $"{c.Field}:masqué" : $"{c.Field}:{c.Old}>{c.New}"));
        Assert.All(bench.Store.Audit.Committed, r => Assert.Equal(Author, r.ActorUserId));
    }

    [Fact]
    public async Task AppointmentClientChange_IsVisibleInBothDossiers_WithoutValues()
    {
        var bench = new AppointmentBench(_clock);
        var appointment = (await bench.Service.CreateAsync(bench.Input(bench.Alice, AppointmentBench.At(10)), Author, CancellationToken.None)).Appointment!.Appointment;
        bench.Store.Audit.Committed.Clear();

        await bench.Service.UpdateAsync(appointment.Id, bench.Input(bench.Alice, AppointmentBench.At(10), client: bench.Other), appointment.Version, Author, CancellationToken.None);

        Assert.Equal([bench.Other, bench.Client], bench.Store.Audit.Committed.Select(r => r.ClientId!.Value));
        Assert.All(bench.Store.Audit.Committed, r => Assert.Equal(new AuditChange("clientId", null, null, true), Assert.Single(r.Changes)));
    }

    [Fact]
    public async Task CancelAndRestore_AreRecorded_TheCancellationReasonIsMasked()
    {
        var bench = new AppointmentBench(_clock);
        var appointment = (await bench.Service.CreateAsync(bench.Input(bench.Alice, AppointmentBench.At(10)), Author, CancellationToken.None)).Appointment!.Appointment;
        var cancelled = (await bench.Service.CancelAsync(appointment.Id, appointment.Version, "Motif personnel fictif", Author, CancellationToken.None)).Appointment!.Appointment;
        await bench.Service.RestoreAsync(cancelled.Id, cancelled.Version, Author, CancellationToken.None);

        Assert.Equal([AuditActions.AppointmentCreated, AuditActions.AppointmentCancelled, AuditActions.AppointmentRestored], bench.Store.Audit.Committed.Select(r => r.Action));
        Assert.Equal(new AuditChange("cancellationReason", null, null, true), Assert.Single(bench.Store.Audit.Committed[1].Changes));
        Assert.Empty(bench.Store.Audit.Committed[2].Changes);
        Assert.DoesNotContain("personnel", AuditChanges.Serialize(bench.Store.Audit.Committed[1].Changes)!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppointmentRefusalsAndFailedCommits_RecordNothing()
    {
        var bench = new AppointmentBench(_clock);
        var existing = (await bench.Service.CreateAsync(bench.Input(bench.Alice, AppointmentBench.At(10)), Author, CancellationToken.None)).Appointment!.Appointment;
        bench.Store.Audit.Committed.Clear();

        Assert.Equal(AppointmentWriteStatus.SlotConflict, (await bench.Service.CreateAsync(bench.Input(bench.Alice, AppointmentBench.At(10, 15)), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.ValidationFailed, (await bench.Service.CreateAsync(bench.Input(bench.Alice, AppointmentBench.At(10), 31), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.VersionConflict, (await bench.Service.CancelAsync(existing.Id, existing.Version + 1, null, Author, CancellationToken.None)).Status);
        // Aucune modification réelle : rien à journaliser.
        var same = (await bench.Service.UpdateAsync(existing.Id, bench.Input(bench.Alice, AppointmentBench.At(10)), existing.Version, Author, CancellationToken.None)).Appointment!.Appointment;
        // Conflit détecté à la validation de la transaction : l'événement préparé disparaît avec elle.
        bench.Store.FailNextCommit = true;
        Assert.Equal(AppointmentWriteStatus.VersionConflict, (await bench.Service.UpdateAsync(same.Id, bench.Input(bench.Alice, AppointmentBench.At(12)), same.Version, Author, CancellationToken.None)).Status);
        Assert.Empty(bench.Store.Audit.Committed);
    }

    [Fact]
    public void RecorderIds_AreStrictlyIncreasing_WithinOneInstant_InPostgreSqlUuidOrder()
    {
        var recorder = new MMV.Infrastructure.Audit.AuditRecorder(null!, new NoAuditContext());
        var at = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 500).Select(_ => recorder.NextId(at)).ToList();

        // PostgreSQL compare les uuid octet par octet dans l'ordre canonique (celui du texte) : comparaison ordinale du texte.
        for (var index = 1; index < ids.Count; index++)
        {
            Assert.True(string.CompareOrdinal(ids[index - 1].ToString(), ids[index].ToString()) < 0, $"{ids[index - 1]} !< {ids[index]}");
        }

        Assert.All(ids, id => Assert.Equal(7, id.Version));
    }

    [Fact]
    public void Domain_RejectsInvalidCodes_AndThePolicyMasksEveryFieldOutsideItsWhitelist()
    {
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Author, null, "Clients", AuditActions.ClientCreated, "client", Guid.NewGuid(), null, null, null));
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Author, null, "clients", "client created", "client", Guid.NewGuid(), null, null, null));
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.Empty, null, "clients", AuditActions.ClientCreated, "client", Guid.NewGuid(), null, null, null));
        Assert.All(AuditActions.All, action => AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Author, null, "clients", action, "client", Guid.NewGuid(), null, null, null));
        foreach (var field in new[] { "firstName", "phone", "email", "notes", "cancellationReason", "birthDate", "clientId", "inconnu" })
        {
            Assert.Equal(new AuditChange(field, null, null, true), AuditFieldPolicy.Change(field, "valeur", "autre"));
        }

        Assert.Equal(new AuditChange("reasonCode", "a", "b", false), AuditFieldPolicy.Change("reasonCode", "a", "b"));
        var longTrace = new string('x', 200);
        Assert.Equal(AuditEvent.TraceIdMaxLength, AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Author, null, "clients", AuditActions.ClientCreated, "client", Guid.NewGuid(), null, null, longTrace).TraceId!.Length);
    }
}

/// <summary>
/// Pipeline HTTP réel de MMV.Api pour /api/audit (authentification, policy dédiée <c>audit.read</c>, HTTPS, liste
/// blanche, libellés calculés par le serveur, minimisation) ; seuls les stores sont doublés. Données fictives.
/// </summary>
public sealed class AuditEndpointsTests : IDisposable
{
    private const string Password = "Mot-de-passe-test-1";

    private readonly InMemoryIdentityStore _identity = new();
    private readonly InMemoryAuditStore _audit = new();
    private readonly List<WebApplicationFactory<Program>> _factories = [];
    private readonly Guid _client = Guid.Parse("0199c111-0000-7000-8000-000000000001");
    private readonly Guid _alice = Guid.Parse("0199b111-0000-7000-8000-000000000001");
    private readonly Guid _actor;

    public AuditEndpointsTests()
    {
        var hashing = RealHashing.Create();
        _identity.AddUser(hashing, "aucun", Password);
        _identity.AddUser(hashing, "admin", Password, Permissions.SystemAdmin);
        _identity.AddUser(hashing, "clients", Password, Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsArchive);
        _identity.AddUser(hashing, "rdv", Password, Permissions.AppointmentsRead, Permissions.AppointmentsWrite, Permissions.AppointmentsCancel);
        _actor = _identity.AddUser(hashing, "auditeur", Password, Permissions.AuditRead).Id;
        _audit.Names[_actor] = "Agent Auditeur";
        _audit.Names[_alice] = "Alice Opticienne";
        Add(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero), AuditActions.ClientsModule, AuditActions.ClientCreated, "client", _client,
            [new AuditChange("firstName", null, null, true), new AuditChange("phone", null, null, true)]);
        Add(new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.Zero), AuditActions.AppointmentsModule, AuditActions.AppointmentCreated, "appointment", Guid.NewGuid(),
            [new AuditChange("startsAtUtc", null, "2026-07-16T08:00:00Z", false), new AuditChange("durationMinutes", null, "30", false),
             new AuditChange("reasonCode", null, "conseil_devis", false), new AuditChange("responsibleUserId", null, _alice.ToString(), false)]);
        Add(new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero), AuditActions.AppointmentsModule, AuditActions.AppointmentResponsibleChanged, "appointment", Guid.NewGuid(),
            [new AuditChange("responsibleUserId", _alice.ToString(), null, false)]);
        Add(new DateTimeOffset(2026, 7, 4, 8, 0, 0, TimeSpan.Zero), AuditActions.ClientsModule, AuditActions.ClientArchived, "client", Guid.NewGuid(), [], client: Guid.NewGuid());
    }

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
    }

    private void Add(DateTimeOffset at, string module, string action, string entityType, Guid entityId, IReadOnlyList<AuditChange> changes, Guid? client = null) =>
        _audit.Events.Add(AuditEvent.Create(Guid.CreateVersion7(at), at, _actor, client ?? _client, module, action, entityType, entityType == "client" && client is null ? _client : entityId,
            AuditChanges.Serialize(changes), Guid.NewGuid(), "trace-test"));

    private WebApplicationFactory<Program> Factory()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = null }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentityStore>();
                services.AddScoped<IIdentityStore>(_ => _identity);
                services.RemoveAll<IAuditStore>();
                services.AddScoped<IAuditStore>(_ => _audit);
                services.RemoveAll<AppointmentSettings>();
                services.AddSingleton(AppointmentTestSettings.Configured("Europe/Brussels"));
            });
        });
        _factories.Add(factory);
        return factory;
    }

    private static async Task<HttpClient> AuthenticatedAsync(WebApplicationFactory<Program> factory, string userName)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { userName, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", document.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }
    }

    [Fact]
    public async Task OnlyAuditRead_GivesAccess_BusinessRightsAndSystemAdminDoNot()
    {
        var factory = Factory();
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using (var response = await anonymous.GetAsync($"/api/audit/clients/{_client}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        foreach (var account in new[] { "aucun", "admin", "clients", "rdv" })
        {
            using var client = await AuthenticatedAsync(factory, account);
            using var response = await client.GetAsync($"/api/audit/clients/{_client}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("Alice", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        using var auditor = await AuthenticatedAsync(factory, "auditeur");
        using var http = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        http.DefaultRequestHeaders.Authorization = auditor.DefaultRequestHeaders.Authorization;
        using (var plain = await http.GetAsync($"/api/audit/clients/{_client}"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, plain.StatusCode);
        }

        Assert.Equal(3, (await ReadAsync(await auditor.GetAsync($"/api/audit/clients/{_client}"))).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task History_IsNewestFirst_WithServerLabels_AndNeverServesMaskedValuesOrTechnicalIds()
    {
        using var client = await AuthenticatedAsync(Factory(), "auditeur");

        var page = await ReadAsync(await client.GetAsync($"/api/audit/clients/{_client}"));

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([AuditActions.AppointmentResponsibleChanged, AuditActions.AppointmentCreated, AuditActions.ClientCreated], items.Select(i => i.GetProperty("action").GetString()));
        Assert.All(items, i => Assert.Equal("Agent Auditeur", i.GetProperty("actorDisplayName").GetString()));
        Assert.Equal(
            ["action", "actorDisplayName", "actorUserId", "changes", "entityId", "entityType", "id", "module", "occurredAtUtc"],
            items[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        var created = items[1].GetProperty("changes").EnumerateArray().ToDictionary(c => c.GetProperty("field").GetString()!, c => c.GetProperty("newDisplay").GetString());
        Assert.Equal("16/07/2026 10:00", created["startsAtUtc"]);
        Assert.Equal("30 min", created["durationMinutes"]);
        Assert.Equal("Conseil / Devis", created["reasonCode"]);
        Assert.Equal("Alice Opticienne", created["responsibleUserId"]);
        var change = Assert.Single(items[0].GetProperty("changes").EnumerateArray());
        Assert.Equal(("Alice Opticienne", "Non attribué"), (change.GetProperty("oldDisplay").GetString(), change.GetProperty("newDisplay").GetString()));
        var masked = items[2].GetProperty("changes").EnumerateArray().ToList();
        Assert.All(masked, c =>
        {
            Assert.True(c.GetProperty("masked").GetBoolean());
            Assert.Equal(JsonValueKind.Null, c.GetProperty("old").ValueKind);
            Assert.Equal(JsonValueKind.Null, c.GetProperty("newDisplay").ValueKind);
        });
        Assert.DoesNotContain("trace-test", page.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Filters_ModuleAndPeriod_AndPagination()
    {
        using var client = await AuthenticatedAsync(Factory(), "auditeur");

        var clients = await ReadAsync(await client.GetAsync($"/api/audit/clients/{_client}?module=clients"));
        Assert.Equal([AuditActions.ClientCreated], clients.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()));
        var period = await ReadAsync(await client.GetAsync($"/api/audit/clients/{_client}?from=2026-07-02T00:00:00Z&to=2026-07-03T00:00:00Z"));
        Assert.Equal([AuditActions.AppointmentCreated], period.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()));
        var second = await ReadAsync(await client.GetAsync($"/api/audit/clients/{_client}?pageSize=2&page=2"));
        Assert.Equal([AuditActions.ClientCreated], second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()));
        Assert.Equal(3, second.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("?module=stock")]
    [InlineData("?from=2026-07-02")]
    [InlineData("?from=2026-07-03T00:00:00Z&to=2026-07-02T00:00:00Z")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?action=client.created")]
    [InlineData("?module=clients&module=appointments")]
    public async Task InvalidQuery_Is400(string query)
    {
        using var client = await AuthenticatedAsync(Factory(), "auditeur");

        using var response = await client.GetAsync($"/api/audit/clients/{_client}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("""{"code":"invalid_query"}""", await response.Content.ReadAsStringAsync());
    }
}
