using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Appointments;
using MMV.Application.Identity;
using MMV.Domain.Appointments;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Infrastructure;
using MMV.Infrastructure.Persistence;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Données fictives VALIDÉES (COMMIT) sur <c>mmv_v2_test</c> UNIQUEMENT (03-B3) : comptes <c>b3-…-{suffixe}</c> et
/// clients « Fictif-B3-{suffixe} », suffixe aléatoire par test. Nettoyage contrôlé en fin de test, dans l'ordre des
/// clés étrangères (rendez-vous, éligibilités, sessions, clients, comptes), cible relue côté serveur avant toute
/// suppression, puis contrôle des résidus (0 attendu). La séquence des références clients avance (O-03-12).
/// </summary>
internal sealed class CommittedTestData : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ITestOutputHelper _output;

    private CommittedTestData(ServiceProvider provider, ITestOutputHelper output)
    {
        _provider = provider;
        _output = output;
    }

    public string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    public List<Guid> Users { get; } = [];

    public List<Guid> Clients { get; } = [];

    public int Residue { get; private set; } = -1;

    public static CommittedTestData Create(ITestOutputHelper output)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString })
            .Build();
        return new CommittedTestData(new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider(), output);
    }

    public AsyncServiceScope NewScope() => _provider.CreateAsyncScope();

    public async Task<Guid> AddUserAsync(string role, bool eligible, string? password = null, params string[] permissions)
    {
        await using var scope = NewScope();
        var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
        Assert.Equal("mmv_v2_test", context.Database.GetDbConnection().Database);
        var now = DateTimeOffset.UtcNow;
        var user = new UserAccount(Guid.CreateVersion7(), UserName(role), $"B3 {role}", now);
        user.SetPasswordHash(password is null ? "hachage-factice" : scope.ServiceProvider.GetRequiredService<IPasswordHashing>().Hash(user, password));
        foreach (var permission in permissions)
        {
            user.Grant(permission);
        }

        context.Users.Add(user);
        if (eligible)
        {
            context.AppointmentResponsibles.Add(AppointmentResponsible.Enable(user.Id, now));
        }

        Users.Add(user.Id);
        await context.SaveChangesAsync();
        return user.Id;
    }

    public string UserName(string role) => $"b3-{role}-{Suffix}";

    public async Task<Guid> AddClientAsync(Guid author, string firstName)
    {
        await using var scope = NewScope();
        var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
        var client = Client.Create(Guid.CreateVersion7(), new ClientDetails(firstName, $"Fictif-B3-{Suffix}", null, null, null, null, null, null, null), author, DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        Clients.Add(client.Id);
        await context.SaveChangesAsync();
        return client.Id;
    }

    /// <summary>Exécution directe validée (cible mmv_v2_test relue), hors transaction de test.</summary>
    public static async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString);
        await connection.OpenAsync();
        await using (var identity = new NpgsqlCommand("SELECT current_database() = 'mmv_v2_test' AND current_user = 'mmv_v2_test' AND inet_server_port() = 5433", connection))
        {
            Assert.True((bool)(await identity.ExecuteScalarAsync())!, "Cible d'écriture refusée (mmv_v2_test/5433 attendu).");
        }

        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<long> CountAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            var users = Users.ToArray();
            var clients = Clients.ToArray();
            var appointments = await ExecuteAsync(
                "DELETE FROM appointments.appointments WHERE client_id = ANY(@c) OR created_by_user_id = ANY(@u) OR updated_by_user_id = ANY(@u) OR responsible_user_id = ANY(@u)",
                ("c", clients), ("u", users));
            var responsibles = await ExecuteAsync("DELETE FROM appointments.responsibles WHERE user_id = ANY(@u)", ("u", users));
            var sessions = await ExecuteAsync("DELETE FROM auth.sessions WHERE user_id = ANY(@u)", ("u", users));
            var deletedClients = await ExecuteAsync("DELETE FROM clients.clients WHERE id = ANY(@c) OR created_by_user_id = ANY(@u) OR updated_by_user_id = ANY(@u)", ("c", clients), ("u", users));
            var deletedUsers = await ExecuteAsync("DELETE FROM auth.users WHERE id = ANY(@u)", ("u", users));
            Residue = (int)await CountAsync(
                """
                SELECT (SELECT count(*) FROM appointments.appointments WHERE client_id = ANY(@c) OR created_by_user_id = ANY(@u))
                     + (SELECT count(*) FROM appointments.responsibles WHERE user_id = ANY(@u))
                     + (SELECT count(*) FROM clients.clients WHERE last_name = @n)
                     + (SELECT count(*) FROM auth.users WHERE user_name LIKE @p)
                     + (SELECT count(*) FROM auth.sessions WHERE user_id = ANY(@u))
                """,
                ("c", clients), ("u", users), ("n", $"Fictif-B3-{Suffix}"), ("p", $"b3-%-{Suffix}"));
            _output.WriteLine(
                $"nettoyage (suffixe {Suffix}) : {appointments} rendez-vous, {responsibles} éligibilité(s), {sessions} session(s), "
                + $"{deletedClients} client(s), {deletedUsers} compte(s) supprimés ; résidus = {Residue}");
        }
        finally
        {
            await _provider.DisposeAsync();
        }
    }
}

/// <summary>
/// B2-REV-04 (audit 03-B2, ajouté en 03-B3) : réservations simultanées sur PostgreSQL RÉEL avec DEUX connexions et
/// un COMMIT réel. Le poste A prend le verrou, passe ses contrôles et s'arrête juste avant d'enregistrer ; le poste B
/// demande le même créneau et doit ATTENDRE (constaté sur son processus serveur) ; A valide (COMMIT) ; B obtient alors
/// le verrou, voit la réservation validée de A et est refusé ; un seul rendez-vous planifié subsiste. Pool d'accueil et
/// même responsable. Exécution : <c>--filter "Stage=AppointmentsWrite"</c>.
/// </summary>
[Trait("Stage", "AppointmentsWrite")]
public sealed class AppointmentsConcurrencyPostgreSqlTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [PostgreSqlTheory(nameof(PostgreSqlTarget.Test))]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TwoStations_SameSlot_ACommits_BWaitsThenIsRefused(bool reception)
    {
        var data = CommittedTestData.Create(output);
        try
        {
            var author = await data.AddUserAsync("auteur", eligible: false);
            var responsible = await data.AddUserAsync("resp", eligible: true);
            var clientA = await data.AddClientAsync(author, "PosteA");
            var clientB = await data.AddClientAsync(author, "PosteB");
            Guid? who = reception ? null : responsible;
            var (reasonA, reasonB) = reception ? ("retrait_commande", "sav_reparation") : ("conseil_devis", "prise_mesures");
            output.WriteLine($"scénario : {(reception ? "pool d'accueil (capacité 1)" : "même responsable")} ; données validées, suffixe {data.Suffix}");

            await using var scopeA = data.NewScope();
            await using var scopeB = data.NewScope();
            var pidA = await BackendPidAsync(scopeA);
            var pidB = await BackendPidAsync(scopeB);
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var serviceA = new AppointmentService(
                new PausedBeforeCommitStore(scopeA.ServiceProvider.GetRequiredService<IAppointmentStore>(), reached, release.Task),
                AppointmentsPostgreSqlTests.Settings, TimeProvider.System);
            var serviceB = new AppointmentService(scopeB.ServiceProvider.GetRequiredService<IAppointmentStore>(), AppointmentsPostgreSqlTests.Settings, TimeProvider.System);
            // Journée propre à ce test (10/03/2027) : aucun autre test n'y réserve.
            var start = AppointmentsPostgreSqlTests.Ten.AddDays(7);

            Task<AppointmentWriteResult>? taskA = null;
            Task<AppointmentWriteResult>? taskB = null;
            try
            {
                // Poste A : verrou pris, contrôles passés, enregistrement suspendu (transaction ouverte, verrou tenu).
                taskA = serviceA.CreateAsync(AppointmentsPostgreSqlTests.In(clientA, who, start, 30, reasonA), author, CancellationToken.None);
                Assert.Same(reached.Task, await Task.WhenAny(reached.Task, taskA).WaitAsync(Timeout));

                // Poste B : créneau recoupant (10:15–10:45 contre 10:00–10:30).
                taskB = serviceB.CreateAsync(AppointmentsPostgreSqlTests.In(clientB, who, start.AddMinutes(15), 30, reasonB), author, CancellationToken.None);
                var wait = await LockObservation.WaitUntilBlockedAsync(pidB, pidA, output);
                Assert.True(wait.Blocked, $"Le poste B n'attend pas le verrou du poste A : {wait}");
                Assert.False(taskB.IsCompleted);
                Assert.Equal(0, await CommittedTestData.CountAsync("SELECT count(*) FROM appointments.appointments WHERE client_id = ANY(@c)", ("c", data.Clients.ToArray())));

                // Poste A : COMMIT réel.
                release.SetResult();
                var resultA = await taskA.WaitAsync(Timeout);
                Assert.Equal(AppointmentWriteStatus.Success, resultA.Status);

                // Poste B : verrou obtenu après le COMMIT de A, réservation de A visible → refus.
                var resultB = await taskB.WaitAsync(Timeout);
                Assert.Equal(reception ? AppointmentWriteStatus.ReceptionCapacityFull : AppointmentWriteStatus.SlotConflict, resultB.Status);
                output.WriteLine($"poste A : {resultA.Status} (rendez-vous {resultA.Appointment!.Appointment.Id}, validé) ; poste B : {resultB.Status}");

                // Une seule réservation planifiée subsiste, celle de A, lue par une autre connexion (donnée validée).
                Assert.Equal(1, await CommittedTestData.CountAsync(
                    "SELECT count(*) FROM appointments.appointments WHERE client_id = ANY(@c) AND status = 'planned'", ("c", data.Clients.ToArray())));
                Assert.Equal(1, await CommittedTestData.CountAsync(
                    "SELECT count(*) FROM appointments.appointments WHERE id = @id AND client_id = @a AND status = 'planned'",
                    ("id", resultA.Appointment.Appointment.Id), ("a", clientA)));
            }
            finally
            {
                // En cas d'échec : A n'est jamais laissé suspendu, aucune connexion ne reste bloquée.
                release.TrySetResult();
                await SettleAsync(taskA);
                await SettleAsync(taskB);
            }
        }
        finally
        {
            await data.DisposeAsync();
        }

        Assert.Equal(0, data.Residue);
    }

    private static async Task<int> BackendPidAsync(AsyncServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
        await context.Database.OpenConnectionAsync();
        Assert.Equal("mmv_v2_test", context.Database.GetDbConnection().Database);
        return await context.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    }

    private static async Task SettleAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.WaitAsync(Timeout);
        }
        catch (Exception exception) when (exception is TimeoutException or NpgsqlException or InvalidOperationException)
        {
            // Déjà signalé par l'assertion qui a échoué.
        }
    }

    /// <summary>Store réel dont l'enregistrement attend un signal : le verrou du poste A reste tenu jusque-là.</summary>
    private sealed class PausedBeforeCommitStore(IAppointmentStore inner, TaskCompletionSource reached, Task release) : IAppointmentStore
    {
        public Task<IReadOnlyList<AppointmentView>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, bool includeCancelled, CancellationToken cancellationToken) =>
            inner.ListAsync(fromUtc, toUtc, includeCancelled, cancellationToken);

        public Task<AppointmentView?> GetAsync(Guid id, CancellationToken cancellationToken) => inner.GetAsync(id, cancellationToken);

        public Task<IReadOnlyList<ResponsibleOption>> ListEligibleResponsiblesAsync(CancellationToken cancellationToken) =>
            inner.ListEligibleResponsiblesAsync(cancellationToken);

        public async Task<IAppointmentWriteSession> BeginWriteAsync(CancellationToken cancellationToken) =>
            new Session(await inner.BeginWriteAsync(cancellationToken), reached, release);

        private sealed class Session(IAppointmentWriteSession inner, TaskCompletionSource reached, Task release) : IAppointmentWriteSession
        {
            public Task<Appointment?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) => inner.FindForUpdateAsync(id, cancellationToken);

            public Task LockAsync(IReadOnlyCollection<AppointmentLock> locks, CancellationToken cancellationToken) => inner.LockAsync(locks, cancellationToken);

            public Task<ClientLockState> LockClientAsync(Guid clientId, CancellationToken cancellationToken) => inner.LockClientAsync(clientId, cancellationToken);

            public Task<bool> IsEligibleResponsibleAsync(Guid userId, CancellationToken cancellationToken) => inner.IsEligibleResponsibleAsync(userId, cancellationToken);

            public Task<bool> HasResponsibleOverlapAsync(Guid responsibleUserId, DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken) =>
                inner.HasResponsibleOverlapAsync(responsibleUserId, startUtc, endUtc, excludeId, cancellationToken);

            public Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> ListUnassignedOverlapsAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken) =>
                inner.ListUnassignedOverlapsAsync(startUtc, endUtc, excludeId, cancellationToken);

            public void Add(Appointment appointment) => inner.Add(appointment);

            public async Task<bool> CommitAsync(CancellationToken cancellationToken)
            {
                reached.TrySetResult();
                await release;
                return await inner.CommitAsync(cancellationToken);
            }

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}

/// <summary>
/// Matrice HTTP Rendez-vous sur le pipeline RÉEL de MMV.Api (authentification, policies, HTTPS, validation, codes
/// d'erreur) et PostgreSQL 18 RÉEL, base <c>mmv_v2_test</c> (03-B3). Paramètres du magasin de TEST passés au seul
/// hôte de test (Europe/Brussels, capacité d'accueil 1) : ce ne sont PAS les paramètres du magasin réel, que le
/// propriétaire doit fournir. Cinq comptes éphémères : system.admin seul, lecture, lecture + écriture, lecture +
/// annulation, complet (éligible). Données validées puis nettoyées (<see cref="CommittedTestData"/>).
/// </summary>
[Trait("Stage", "AppointmentsWrite")]
public sealed class AppointmentsHttpPostgreSqlTests(ITestOutputHelper output)
{
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task AppointmentsHttpMatrix_RightsConflictsAndInputValidation_OnRealPostgreSql()
    {
        var connectionString = PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Mmv", connectionString);
            builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
            builder.UseSetting("Appointments:TimeZone", "Europe/Brussels");
            builder.UseSetting("Appointments:DisplayStartTime", "08:00");
            builder.UseSetting("Appointments:DisplayEndTime", "20:00");
            builder.UseSetting("Appointments:ReceptionCapacity", "1");
        });

        var data = CommittedTestData.Create(output);
        var checks = 0;
        try
        {
            var password = SessionTokens.NewToken();
            var admin = await data.AddUserAsync("admin", eligible: false, password, Permissions.SystemAdmin);
            await data.AddUserAsync("lecture", eligible: false, password, Permissions.AppointmentsRead);
            await data.AddUserAsync("ecriture", eligible: false, password, Permissions.AppointmentsRead, Permissions.AppointmentsWrite);
            await data.AddUserAsync("annulation", eligible: false, password, Permissions.AppointmentsRead, Permissions.AppointmentsCancel);
            var full = await data.AddUserAsync("complet", eligible: true, password, Permissions.AppointmentsRead, Permissions.AppointmentsWrite, Permissions.AppointmentsCancel);
            var other = await data.AddUserAsync("autre", eligible: true);
            var client = await data.AddClientAsync(admin, "Http");
            var archived = await data.AddClientAsync(admin, "Archivee");
            await CommittedTestData.ExecuteAsync("UPDATE clients.clients SET archived_at_utc = created_at_utc WHERE id = @id", ("id", archived));
            output.WriteLine($"comptes et clients éphémères validés sur mmv_v2_test (suffixe {data.Suffix})");

            using var http = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            using var plain = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var role in new[] { "admin", "lecture", "ecriture", "annulation", "complet" })
            {
                tokens[role] = await LoginAsync(http, data.UserName(role), password);
            }

            async Task Expect(HttpStatusCode status, string? code, HttpMethod method, string path, string? role, object? body = null, string? rawBody = null)
            {
                using var response = await SendAsync(http, method, path, role is null ? null : tokens[role], body, rawBody);
                var text = await response.Content.ReadAsStringAsync();
                Assert.True(status == response.StatusCode, $"{method} {path} ({role ?? "anonyme"}) : {(int)response.StatusCode} {text}");
                if (code is not null)
                {
                    // « champ:code » : erreur de validation par champ (422) ; sinon code d'erreur global.
                    using var document = JsonDocument.Parse(text);
                    var root = document.RootElement;
                    var field = code.Contains(':', StringComparison.Ordinal) ? code.Split(':')[0] : null;
                    var actual = field is null
                        ? root.GetProperty("code").GetString()
                        : $"{field}:{root.GetProperty("errors").GetProperty(field)[0].GetString()}";
                    Assert.True(code == actual, $"{method} {path} ({role ?? "anonyme"}) : code {actual} au lieu de {code} — {text}");
                }

                checks++;
            }

            // Journée propre à ce test (mercredi 17/03/2027, Bruxelles UTC+1).
            var ten = AppointmentsPostgreSqlTests.Ten.AddDays(14);
            object Body(DateTimeOffset start, Guid? responsible, string reason = "conseil_devis", int minutes = 30, Guid? clientId = null, string? notes = null, uint? version = null, string? local = null)
            {
                var body = new Dictionary<string, object?>
                {
                    ["clientId"] = clientId ?? client,
                    ["responsibleUserId"] = responsible,
                    ["startsAtUtc"] = AppointmentsPostgreSqlTests.Utc(start),
                    ["startsAtLocal"] = local ?? AppointmentSchedule.LocalWallTime(AppointmentsPostgreSqlTests.Settings.TimeZone!, start),
                    ["durationMinutes"] = minutes,
                    ["reasonCode"] = reason,
                    ["notes"] = notes,
                };
                if (version is not null)
                {
                    body["version"] = version;
                }

                return body;
            }

            // Authentification, HTTPS, policies.
            await Expect(HttpStatusCode.Unauthorized, "unauthenticated", HttpMethod.Get, "/api/appointments/options", null);
            await Expect(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Get, "/api/appointments/options", "admin");
            await Expect(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, "/api/appointments", "admin", Body(ten, full));
            using (var insecure = await SendAsync(plain, HttpMethod.Get, "/api/appointments/options", tokens["lecture"]))
            {
                Assert.Equal(HttpStatusCode.Forbidden, insecure.StatusCode);
                Assert.Contains("https_required", await insecure.Content.ReadAsStringAsync(), StringComparison.Ordinal);
                checks++;
            }

            using (var options = await SendAsync(http, HttpMethod.Get, "/api/appointments/options", tokens["lecture"]))
            {
                var root = await ReadAsync(options);
                Assert.Equal(HttpStatusCode.OK, options.StatusCode);
                Assert.Equal("Europe/Brussels", root.GetProperty("timeZone").GetString());
                var responsibles = root.GetProperty("responsibles").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToArray();
                Assert.Contains(full, responsibles);
                Assert.Contains(other, responsibles);
                checks++;
            }

            await Expect(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, "/api/appointments", "lecture", Body(ten, full));
            await Expect(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, "/api/appointments", "annulation", Body(ten, full));

            // Création (lecture + écriture), puis conflit et capacité.
            using var created = await SendAsync(http, HttpMethod.Post, "/api/appointments", tokens["ecriture"], Body(ten, full, notes: "<script>alert(1)</script>'); DROP TABLE appointments.appointments; --"));
            var detail = await ReadAsync(created);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("<script>alert(1)</script>'); DROP TABLE appointments.appointments; --", detail.GetProperty("notes").GetString());
            Assert.Equal($"B3 ecriture", detail.GetProperty("updatedByDisplayName").GetString());
            var id = detail.GetProperty("id").GetGuid();
            var version = detail.GetProperty("version").GetUInt32();
            checks++;

            await Expect(HttpStatusCode.Conflict, "appointment_conflict", HttpMethod.Post, "/api/appointments", "ecriture", Body(ten.AddMinutes(15), full));
            await Expect(HttpStatusCode.Created, null, HttpMethod.Post, "/api/appointments", "ecriture", Body(ten.AddMinutes(30), full));
            await Expect(HttpStatusCode.Created, null, HttpMethod.Post, "/api/appointments", "ecriture", Body(ten, null, "retrait_commande"));
            await Expect(HttpStatusCode.Conflict, "reception_capacity_full", HttpMethod.Post, "/api/appointments", "ecriture", Body(ten.AddMinutes(20), null, "sav_reparation"));
            await Expect(HttpStatusCode.Conflict, "client_archived", HttpMethod.Post, "/api/appointments", "ecriture", Body(ten.AddHours(3), full, clientId: archived));
            await Expect(HttpStatusCode.UnprocessableEntity, "responsibleUserId:not_eligible", HttpMethod.Post, "/api/appointments", "ecriture", Body(ten.AddHours(3), admin));
            await Expect(HttpStatusCode.UnprocessableEntity, "startsAtLocal:time_zone_mismatch", HttpMethod.Post, "/api/appointments", "ecriture", Body(ten.AddHours(3), full, local: "2027-03-17T14:00"));
            await Expect(HttpStatusCode.UnprocessableEntity, "notes:too_long", HttpMethod.Post, "/api/appointments", "ecriture", Body(ten.AddHours(3), full, notes: new string('x', 1001)));
            var yesterday = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(-1).AddHours(9), TimeSpan.Zero);
            await Expect(HttpStatusCode.UnprocessableEntity, "startsAtUtc:in_past", HttpMethod.Post, "/api/appointments", "ecriture", Body(yesterday, full));

            // Corps et paramètres stricts.
            await Expect(HttpStatusCode.BadRequest, "invalid_body", HttpMethod.Post, "/api/appointments", "ecriture", rawBody: "{\"clientId\":\"" + client + "\",\"isAdmin\":true}");
            await Expect(HttpStatusCode.BadRequest, "invalid_body", HttpMethod.Post, "/api/appointments", "ecriture", rawBody: "{\"durationMinutes\":\"30\"}");
            await Expect(HttpStatusCode.BadRequest, "invalid_body", HttpMethod.Post, "/api/appointments", "ecriture", rawBody: "{not json");
            await Expect(HttpStatusCode.BadRequest, "invalid_query", HttpMethod.Get, "/api/appointments?from=2027-03-01T00:00:00Z';DROP TABLE x;--&to=2027-03-08T00:00:00Z", "lecture");
            await Expect(HttpStatusCode.BadRequest, "invalid_query", HttpMethod.Get, "/api/appointments?from=2027-03-01T00:00:00Z&to=2027-05-01T00:00:00Z", "lecture");
            await Expect(HttpStatusCode.NotFound, "appointment_not_found", HttpMethod.Get, $"/api/appointments/{Guid.CreateVersion7()}", "lecture");
            await Expect(HttpStatusCode.MethodNotAllowed, null, HttpMethod.Delete, $"/api/appointments/{id}", "complet");

            // Liste : tri SQL, décalages servis (03-B3).
            using (var list = await SendAsync(http, HttpMethod.Get, "/api/appointments?from=2027-03-16T23:00:00Z&to=2027-03-17T23:00:00Z", tokens["lecture"]))
            {
                var root = await ReadAsync(list);
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);
                var starts = root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("startsAtUtc").GetDateTimeOffset()).ToArray();
                Assert.Equal(3, starts.Length);
                Assert.Equal(starts.Order(), starts);
                Assert.False(root.GetProperty("items")[0].TryGetProperty("notes", out _));
                var offset = Assert.Single(root.GetProperty("timeZoneOffsets").EnumerateArray());
                Assert.Equal(60, offset.GetProperty("offsetMinutes").GetInt32());
                checks++;
            }

            // Modification, version périmée, déplacement avec responsable retiré de l'éligibilité (B2-REV-01).
            using (var updated = await SendAsync(http, HttpMethod.Put, $"/api/appointments/{id}", tokens["ecriture"], Body(ten, full, notes: "Note modifiée", version: version)))
            {
                var root = await ReadAsync(updated);
                Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
                await Expect(HttpStatusCode.Conflict, "version_conflict", HttpMethod.Put, $"/api/appointments/{id}", "ecriture", Body(ten, full, notes: "Autre poste", version: version));
                version = root.GetProperty("version").GetUInt32();
            }

            await Expect(HttpStatusCode.BadRequest, "version_required", HttpMethod.Put, $"/api/appointments/{id}", "ecriture", Body(ten, full));
            await Expect(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Put, $"/api/appointments/{id}", "annulation", Body(ten, full, version: version));
            await CommittedTestData.ExecuteAsync("UPDATE appointments.responsibles SET is_eligible = false, updated_at_utc = now() WHERE user_id = @u", ("u", full));
            await Expect(HttpStatusCode.UnprocessableEntity, "responsibleUserId:not_eligible", HttpMethod.Put, $"/api/appointments/{id}", "ecriture", Body(ten.AddHours(4), full, version: version));
            await CommittedTestData.ExecuteAsync("UPDATE appointments.responsibles SET is_eligible = true, updated_at_utc = now() WHERE user_id = @u", ("u", full));

            // Annulation (droit cancel), rétablissement, rétablissement sur créneau repris.
            await Expect(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, $"/api/appointments/{id}/cancel", "ecriture", new { version, reason = (string?)null });
            using (var cancelled = await SendAsync(http, HttpMethod.Post, $"/api/appointments/{id}/cancel", tokens["annulation"], new { version, reason = "Client indisponible" }))
            {
                var root = await ReadAsync(cancelled);
                Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
                Assert.Equal("B3 annulation", root.GetProperty("cancelledByDisplayName").GetString());
                version = root.GetProperty("version").GetUInt32();
                checks++;
            }

            await Expect(HttpStatusCode.Conflict, "appointment_cancelled", HttpMethod.Post, $"/api/appointments/{id}/cancel", "annulation", new { version, reason = (string?)null });
            await Expect(HttpStatusCode.Created, null, HttpMethod.Post, "/api/appointments", "ecriture", Body(ten, full, "prise_mesures"));
            await Expect(HttpStatusCode.Conflict, "appointment_conflict", HttpMethod.Post, $"/api/appointments/{id}/restore", "annulation", new { version });

            // Rendez-vous commencé (inséré par SQL dans le passé) : réservation figée, notes corrigeables (B2-REV-02).
            var started = Guid.CreateVersion7();
            await CommittedTestData.ExecuteAsync(
                """
                INSERT INTO appointments.appointments (id, client_id, responsible_user_id, starts_at_utc, ends_at_utc, reason_code, status,
                    created_at_utc, created_by_user_id, updated_at_utc, updated_by_user_id)
                VALUES (@id, @c, @r, date_trunc('hour', now()) - interval '1 hour', date_trunc('hour', now()) - interval '30 minutes',
                    'conseil_devis', 'planned', now() - interval '2 days', @r, now() - interval '2 days', @r)
                """,
                ("id", started), ("c", client), ("r", full));
            using (var past = await SendAsync(http, HttpMethod.Get, $"/api/appointments/{started}", tokens["lecture"]))
            {
                var root = await ReadAsync(past);
                var start = root.GetProperty("startsAtUtc").GetDateTimeOffset();
                var pastVersion = root.GetProperty("version").GetUInt32();
                await Expect(HttpStatusCode.Conflict, "appointment_past", HttpMethod.Put, $"/api/appointments/{started}", "complet", Body(start, other, version: pastVersion));
                await Expect(HttpStatusCode.Conflict, "appointment_past", HttpMethod.Put, $"/api/appointments/{started}", "complet", Body(ten.AddHours(6), full, version: pastVersion));
                await Expect(HttpStatusCode.OK, null, HttpMethod.Put, $"/api/appointments/{started}", "complet", Body(start, full, "choix_equipement", notes: "Monture choisie", version: pastVersion));
                using var after = await SendAsync(http, HttpMethod.Get, $"/api/appointments/{started}", tokens["lecture"]);
                var corrected = await ReadAsync(after);
                Assert.Equal(("choix_equipement", "Monture choisie", "B3 complet", start), (corrected.GetProperty("reasonCode").GetString(), corrected.GetProperty("notes").GetString(), corrected.GetProperty("updatedByDisplayName").GetString(), corrected.GetProperty("startsAtUtc").GetDateTimeOffset()));
                // Annulation d'un rendez-vous passé permise ; rétablissement refusé (début passé).
                using var cancelledPast = await SendAsync(http, HttpMethod.Post, $"/api/appointments/{started}/cancel", tokens["complet"], new { version = corrected.GetProperty("version").GetUInt32(), reason = (string?)null });
                Assert.Equal(HttpStatusCode.OK, cancelledPast.StatusCode);
                checks++;
                await Expect(HttpStatusCode.Conflict, "appointment_past", HttpMethod.Post, $"/api/appointments/{started}/restore", "complet", new { version = (await ReadAsync(cancelledPast)).GetProperty("version").GetUInt32() });
            }

            output.WriteLine($"matrice HTTP : {checks} contrôles PASS");
        }
        finally
        {
            await data.DisposeAsync();
        }

        Assert.Equal(0, data.Residue);
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token, object? body = null, string? rawBody = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (rawBody is not null)
        {
            request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        }
        else if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
