using Microsoft.EntityFrameworkCore;
using MMV.Application.Appointments;
using MMV.Domain.Appointments;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Module Rendez-vous sur PostgreSQL RÉEL, base <c>mmv_v2_test</c> UNIQUEMENT. Exige la migration
/// 0004_AppointmentsModule appliquée (appliquée le 17/10/2026, GO 03-B3).
/// Exécution : <c>dotnet test tests/MMV.PostgreSql.Tests --filter "Stage=AppointmentsWrite"</c>.
/// Données exclusivement fictives, dans des transactions ANNULÉES (ROLLBACK) : aucune ligne ne subsiste. Les
/// réservations simultanées VALIDÉES (COMMIT) et la matrice HTTP sont dans AppointmentsCommittedPostgreSqlTests.cs.
/// </summary>
[Trait("Stage", "AppointmentsWrite")]
public sealed class AppointmentsPostgreSqlTests(ITestOutputHelper output)
{
    // Mercredi 03/03/2027, 10:00 à Bruxelles (UTC+1) : toujours dans le futur à l'exécution prévue.
    internal static readonly DateTimeOffset Ten = new(2027, 3, 3, 9, 0, 0, TimeSpan.Zero);

    internal static readonly AppointmentSettings Settings = AppointmentSettings.From(new AppointmentsOptions
    {
        TimeZone = "Europe/Brussels",
        DisplayStartTime = "08:00",
        DisplayEndTime = "20:00",
        ReceptionCapacity = 1,
    });

    internal static string Utc(DateTimeOffset instant) => instant.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Saisie d'un poste dont les données de fuseaux concordent avec celles du serveur (B2-REV-03).</summary>
    internal static AppointmentInput In(Guid client, Guid? responsible, DateTimeOffset start, int minutes, string reason, string? notes = null) =>
        new(client, responsible, Utc(start), AppointmentSchedule.LocalWallTime(Settings.TimeZone!, start), minutes, reason, notes);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task PersonalAppointment_OverlapConsecutiveCancelRestore_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, responsible, client) = await SeedAsync(fixture, eligible: true);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);

        var created = await service.CreateAsync(In(client, responsible, Ten, 30, "conseil_devis", "Note de test"), author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, created.Status);
        var appointment = created.Appointment!.Appointment;
        Assert.NotEqual(0u, appointment.Version);
        output.WriteLine($"rendez-vous {appointment.Id}, xmin={appointment.Version}");

        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.SlotConflict,
            (await service.CreateAsync(In(client, responsible, Ten.AddMinutes(15), 30, "conseil_devis"), author, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.Success,
            (await service.CreateAsync(In(client, responsible, Ten.AddMinutes(30), 30, "conseil_devis"), author, CancellationToken.None)).Status);

        fixture.Context.ChangeTracker.Clear();
        var cancelled = await service.CancelAsync(appointment.Id, appointment.Version, "Motif de test", author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, cancelled.Status);
        Assert.Equal(author, cancelled.Appointment!.Appointment.CancelledByUserId);
        Assert.NotEqual(appointment.Version, cancelled.Appointment.Appointment.Version);

        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.VersionConflict,
            (await service.RestoreAsync(appointment.Id, appointment.Version, author, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        var restored = await service.RestoreAsync(appointment.Id, cancelled.Appointment.Appointment.Version, author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, restored.Status);
        Assert.Equal(Appointment.StatusPlanned, restored.Appointment!.Appointment.Status);

        fixture.Context.ChangeTracker.Clear();
        var listed = await service.ListAsync(Ten.AddHours(-1), Ten.AddHours(2), includeCancelled: true, CancellationToken.None);
        Assert.Equal(2, listed.Count);
        Assert.All(listed, v => Assert.Equal("Responsable PG", v.ResponsibleDisplayName));
    }

    /// <summary>
    /// Ajouté le 17/10/2026 (03-B3) : la liste est triée PAR POSTGRESQL (début puis identifiant) et porte l'auteur de la
    /// dernière modification. Défaut trouvé à la première exécution réelle : le tri appliqué après la projection
    /// n'était pas traduisible en SQL (exception EF), invisible sur les doubles de test.
    /// </summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task List_IsOrderedByStartThenId_AndCarriesTheLastEditor_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, responsible, client) = await SeedAsync(fixture, eligible: true);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);

        var late = (await service.CreateAsync(In(client, responsible, Ten.AddHours(2), 30, "conseil_devis"), author, CancellationToken.None)).Appointment!;
        fixture.Context.ChangeTracker.Clear();
        var early = (await service.CreateAsync(In(client, null, Ten, 30, "retrait_commande"), author, CancellationToken.None)).Appointment!;
        fixture.Context.ChangeTracker.Clear();
        var edited = await service.UpdateAsync(late.Appointment.Id, In(client, responsible, Ten.AddHours(2), 30, "prise_mesures", "Corrigé"), late.Appointment.Version, responsible, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, edited.Status);

        fixture.Context.ChangeTracker.Clear();
        var listed = await service.ListAsync(Ten.AddHours(-1), Ten.AddHours(4), includeCancelled: false, CancellationToken.None);

        Assert.Equal([early.Appointment.Id, late.Appointment.Id], listed.Select(v => v.Appointment.Id));
        Assert.Equal(["Auteur PG", "Responsable PG"], listed.Select(v => v.UpdatedByDisplayName));
        Assert.Equal("Responsable PG", (await service.GetAsync(late.Appointment.Id, CancellationToken.None))!.UpdatedByDisplayName);
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ReceptionCapacity_IsComputedAtEachInstant_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, _, client) = await SeedAsync(fixture, eligible: true);
        var two = AppointmentSettings.From(new AppointmentsOptions { TimeZone = "Europe/Brussels", DisplayStartTime = "08:00", DisplayEndTime = "20:00", ReceptionCapacity = 2 });
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), two, TimeProvider.System);

        async Task<AppointmentWriteStatus> BookAsync(DateTimeOffset start, int minutes)
        {
            fixture.Context.ChangeTracker.Clear();
            return (await service.CreateAsync(In(client, null, start, minutes, "retrait_commande"), author, CancellationToken.None)).Status;
        }

        Assert.Equal(AppointmentWriteStatus.Success, await BookAsync(Ten, 30));
        Assert.Equal(AppointmentWriteStatus.Success, await BookAsync(Ten.AddMinutes(30), 30));
        Assert.Equal(AppointmentWriteStatus.Success, await BookAsync(Ten, 60));
        Assert.Equal(AppointmentWriteStatus.ReceptionCapacityFull, await BookAsync(Ten.AddMinutes(45), 15));
        Assert.Equal(AppointmentWriteStatus.Success, await BookAsync(Ten.AddMinutes(60), 15));
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ArchivedClientAndIneligibleCollaborator_AreRefused_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, responsible, client) = await SeedAsync(fixture, eligible: false);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);

        var refused = await service.CreateAsync(In(client, responsible, Ten, 30, "prise_mesures"), author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.ValidationFailed, refused.Status);
        Assert.Equal(["not_eligible"], refused.Errors!["responsibleUserId"]);

        await fixture.ExecuteAsync($"UPDATE clients.clients SET archived_at_utc = created_at_utc WHERE id = '{client}'");
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.ClientArchived,
            (await service.CreateAsync(In(client, null, Ten, 30, "retrait_commande"), author, CancellationToken.None)).Status);
    }

    /// <summary>B2-REV-01 sur PostgreSQL réel (03-B3) : déplacer en gardant un collaborateur retiré de l'éligibilité est refusé.</summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task Move_KeepingAResponsibleNoLongerEligible_IsRefused_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, responsible, client) = await SeedAsync(fixture, eligible: true);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);
        var booked = (await service.CreateAsync(In(client, responsible, Ten, 30, "conseil_devis"), author, CancellationToken.None)).Appointment!.Appointment;

        await fixture.ExecuteAsync($"UPDATE appointments.responsibles SET is_eligible = false, updated_at_utc = now() WHERE user_id = '{responsible}'");
        fixture.Context.ChangeTracker.Clear();
        var moved = await service.UpdateAsync(booked.Id, In(client, responsible, Ten.AddHours(1), 30, "conseil_devis"), booked.Version, author, CancellationToken.None);

        Assert.Equal(AppointmentWriteStatus.ValidationFailed, moved.Status);
        Assert.Equal(["not_eligible"], moved.Errors!["responsibleUserId"]);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(Ten, (await service.GetAsync(booked.Id, CancellationToken.None))!.Appointment.StartsAtUtc);
    }

    /// <summary>B2-REV-02 sur PostgreSQL réel (03-B3) : rendez-vous commencé (inséré dans le passé par SQL), réservation figée.</summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task StartedAppointment_KeepsItsBooking_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, responsible, client) = await SeedAsync(fixture, eligible: true);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);
        var id = Guid.CreateVersion7();
        await fixture.ExecuteAsync(
            $"""
            INSERT INTO appointments.appointments (id, client_id, responsible_user_id, starts_at_utc, ends_at_utc, reason_code, status,
                created_at_utc, created_by_user_id, updated_at_utc, updated_by_user_id)
            VALUES ('{id}', '{client}', '{responsible}', date_trunc('hour', now()) - interval '2 hours', date_trunc('hour', now()) - interval '90 minutes',
                'conseil_devis', 'planned', now() - interval '3 days', '{author}', now() - interval '3 days', '{author}')
            """);
        var stored = (await service.GetAsync(id, CancellationToken.None))!.Appointment;
        var start = stored.StartsAtUtc;

        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.AppointmentPast,
            (await service.UpdateAsync(id, In(client, null, start, 30, "sav_reparation"), stored.Version, author, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.AppointmentPast,
            (await service.UpdateAsync(id, In(client, responsible, Ten, 30, "conseil_devis"), stored.Version, author, CancellationToken.None)).Status);

        fixture.Context.ChangeTracker.Clear();
        var corrected = await service.UpdateAsync(id, In(client, responsible, start, 30, "choix_equipement", "Note corrigée"), stored.Version, responsible, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, corrected.Status);
        Assert.Equal(("choix_equipement", "Note corrigée", start, (Guid?)responsible), (corrected.Appointment!.Appointment.ReasonCode, corrected.Appointment.Appointment.Notes, corrected.Appointment.Appointment.StartsAtUtc, corrected.Appointment.Appointment.ResponsibleUserId));
        Assert.Equal((stored.CreatedAtUtc, author, "Responsable PG"), (corrected.Appointment.Appointment.CreatedAtUtc, corrected.Appointment.Appointment.CreatedByUserId, corrected.Appointment.UpdatedByDisplayName));
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task Constraints_AreEnforcedByPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, _, client) = await SeedAsync(fixture, eligible: true);
        string Insert(string status, string reason, string start, string end, string cancelled) =>
            $"""
            INSERT INTO appointments.appointments (id, client_id, starts_at_utc, ends_at_utc, reason_code, status, cancelled_at_utc, cancelled_by_user_id,
                created_at_utc, created_by_user_id, updated_at_utc, updated_by_user_id)
            VALUES (gen_random_uuid(), '{client}', '{start}', '{end}', '{reason}', '{status}', {cancelled}, {(cancelled == "NULL" ? "NULL" : $"'{author}'")},
                now(), '{author}', now(), '{author}')
            """;

        await fixture.ExpectSqlStateAsync("23514", Insert("honored", "autre", "2027-03-03T09:00Z", "2027-03-03T09:30Z", "NULL"));
        await fixture.ExpectSqlStateAsync("23514", Insert("planned", "Motif Libre", "2027-03-03T09:00Z", "2027-03-03T09:30Z", "NULL"));
        await fixture.ExpectSqlStateAsync("23514", Insert("planned", "autre", "2027-03-03T09:30Z", "2027-03-03T09:00Z", "NULL"));
        await fixture.ExpectSqlStateAsync("23514", Insert("cancelled", "autre", "2027-03-03T09:00Z", "2027-03-03T09:30Z", "NULL"));
        await fixture.ExpectSqlStateAsync("23514", Insert("planned", "autre", "2027-03-03T09:00Z", "2027-03-03T09:30Z", "now()"));
        await fixture.ExecuteAsync(Insert("planned", "autre", "2027-03-03T09:00Z", "2027-03-03T09:30Z", "NULL"));
        // Aucune suppression en cascade : un client référencé ne disparaît pas (O-03-14 : 23001 sur PostgreSQL 18).
        await fixture.ExpectSqlStateAsync("23001", $"DELETE FROM clients.clients WHERE id = '{client}'");
        await fixture.ExpectSqlStateAsync("23514", $"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author}', 'appointments.delete')");
        await fixture.ExecuteAsync($"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author}', 'appointments.cancel')");
    }

    /// <summary>
    /// Deux connexions, transaction du poste A ANNULÉE (ROLLBACK) : le poste B attendait le verrou consultatif de A, puis
    /// réserve la place libérée. Modifié le 17/10/2026 (03-B3) : la version 03-B2 comptait les verrous en attente dans
    /// <c>pg_locks</c> par l'espace de clés (0 constaté) et, en échec, laissait la connexion de B bloquée, ce qui faisait
    /// expirer le test suivant ; l'attente est désormais constatée sur le processus serveur de B lui-même
    /// (<c>pg_stat_activity.wait_event = 'advisory'</c>, <c>pg_blocking_pids</c> = A) et la transaction de A est toujours
    /// annulée avant libération. Le scénario avec COMMIT réel est AppointmentsConcurrencyPostgreSqlTests.
    /// </summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task SimultaneousReceptionBookings_TwoConnections_SecondWaitsForTheFirst()
    {
        await using var first = await RolledBackScope.OpenAsync(output);
        await using var second = await RolledBackScope.OpenAsync(output);
        var (authorA, _, clientA) = await SeedAsync(first, eligible: true);
        var (authorB, _, clientB) = await SeedAsync(second, eligible: true);
        var pidA = await first.ScalarAsync<int>("SELECT pg_backend_pid()");
        var pidB = await second.ScalarAsync<int>("SELECT pg_backend_pid()");
        var serviceA = new AppointmentService(first.Get<IAppointmentStore>(), Settings, TimeProvider.System);
        var serviceB = new AppointmentService(second.Get<IAppointmentStore>(), Settings, TimeProvider.System);

        // Poste A : réserve la place d'accueil et garde sa transaction ouverte (verrou consultatif tenu).
        var booked = await serviceA.CreateAsync(In(clientA, null, Ten, 30, "retrait_commande"), authorA, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, booked.Status);

        // Poste B : même pool, même créneau → doit attendre le verrou de A.
        var pending = serviceB.CreateAsync(In(clientB, null, Ten, 30, "sav_reparation"), authorB, CancellationToken.None);
        try
        {
            var wait = await LockObservation.WaitUntilBlockedAsync(pidB, pidA, output);
            Assert.True(wait.Blocked, $"Le second poste n'attend pas le verrou consultatif du premier : {wait}");
            Assert.False(pending.IsCompleted);
        }
        finally
        {
            // A annule (ROLLBACK) : verrou relâché et place libérée — y compris si une assertion a échoué.
            await first.Context.Database.RollbackTransactionAsync();
        }

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(AppointmentWriteStatus.Success, result.Status);
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task SimultaneousReceptionBookings_SameTransactionView_SecondIsRefused()
    {
        // Complément du test précédent : une fois la première réservation visible, la seconde est refusée.
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, _, client) = await SeedAsync(fixture, eligible: true);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);

        Assert.Equal(AppointmentWriteStatus.Success,
            (await service.CreateAsync(In(client, null, Ten, 30, "retrait_commande"), author, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.ReceptionCapacityFull,
            (await service.CreateAsync(In(client, null, Ten.AddMinutes(15), 30, "sav_reparation"), author, CancellationToken.None)).Status);
    }

    /// <summary>Compte auteur, collaborateur (éligible ou non) et client fictifs, dans la transaction du test.</summary>
    /// <summary>
    /// 03-D1 : historique d'un client traduit et exécuté PAR POSTGRESQL (filtres par catégorie, tri croissant et
    /// décroissant, pagination par sous-requête avant jointures, effectifs), autre client exclu.
    /// </summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ClientHistory_FiltersOrdersPaginatesAndCounts_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, responsible, client) = await SeedAsync(fixture, eligible: true);
        var (_, _, otherClient) = await SeedAsync(fixture, eligible: true);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);
        async Task<Appointment> Create(Guid owner, DateTimeOffset start, Guid? person, string reason)
        {
            var result = await service.CreateAsync(In(owner, person, start, 30, reason), author, CancellationToken.None);
            Assert.Equal(AppointmentWriteStatus.Success, result.Status);
            fixture.Context.ChangeTracker.Clear();
            return result.Appointment!.Appointment;
        }

        var a = await Create(client, Ten, responsible, "conseil_devis");
        var b = await Create(client, Ten.AddHours(2), null, "retrait_commande");
        var c = await Create(client, Ten.AddHours(4), responsible, "prise_mesures");
        var d = await Create(client, Ten.AddDays(1), responsible, "conseil_devis");
        await Create(otherClient, Ten.AddHours(6), responsible, "conseil_devis");
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CancelAsync(c.Id, c.Version, null, author, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();

        // « Maintenant » = 12:15 : A terminé (passé), B en cours (à venir), C annulé, D demain.
        var store = fixture.Get<IAppointmentStore>();
        var now = Ten.AddHours(2).AddMinutes(15);
        async Task<ClientAppointmentPage> Page(ClientAppointmentScope scope, bool descending, int page = 1, int size = 20) =>
            await store.ListForClientAsync(new ClientAppointmentQuery(client, scope, descending, page, size), now, CancellationToken.None);

        var upcoming = await Page(ClientAppointmentScope.Upcoming, descending: false);
        Assert.Equal([b.Id, d.Id], upcoming.Items.Select(v => v.Appointment.Id));
        Assert.Equal(new ClientAppointmentCounts(2, 1, 1), upcoming.Counts);
        Assert.Equal("Responsable PG", upcoming.Items[1].ResponsibleDisplayName);
        Assert.Null(upcoming.Items[0].ResponsibleDisplayName);
        Assert.Equal([a.Id], (await Page(ClientAppointmentScope.Past, descending: true)).Items.Select(v => v.Appointment.Id));
        Assert.Equal([c.Id], (await Page(ClientAppointmentScope.Cancelled, descending: true)).Items.Select(v => v.Appointment.Id));
        var all = await Page(ClientAppointmentScope.All, descending: true);
        Assert.Equal([d.Id, c.Id, b.Id, a.Id], all.Items.Select(v => v.Appointment.Id));
        Assert.Equal(4, all.TotalCount);
        Assert.Equal([a.Id, b.Id, c.Id, d.Id], (await Page(ClientAppointmentScope.All, descending: false)).Items.Select(v => v.Appointment.Id));
        var second = await Page(ClientAppointmentScope.All, descending: true, page: 2, size: 1);
        Assert.Equal([c.Id], second.Items.Select(v => v.Appointment.Id));
        Assert.Equal(4, second.TotalCount);
        Assert.Empty((await Page(ClientAppointmentScope.All, descending: true, page: 9, size: 1)).Items);
        output.WriteLine($"historique : {all.Items.Count} rendez-vous, effectifs {upcoming.Counts}");
    }

    private static async Task<(Guid Author, Guid Responsible, Guid Client)> SeedAsync(RolledBackScope fixture, bool eligible)
    {
        var now = DateTimeOffset.UtcNow;
        var author = new UserAccount(Guid.CreateVersion7(), $"pg-a-{Guid.NewGuid():N}"[..20], "Auteur PG", now);
        author.SetPasswordHash("hachage-factice");
        var responsible = new UserAccount(Guid.CreateVersion7(), $"pg-r-{Guid.NewGuid():N}"[..20], "Responsable PG", now);
        responsible.SetPasswordHash("hachage-factice");
        fixture.Context.Users.AddRange(author, responsible);
        var responsibleRow = AppointmentResponsible.Enable(responsible.Id, now);
        if (!eligible)
        {
            responsibleRow.SetEligible(false, now.AddSeconds(1));
        }

        fixture.Context.AppointmentResponsibles.Add(responsibleRow);
        var client = Client.Create(Guid.CreateVersion7(), new ClientDetails("Cliente", "Fictive PG", null, null, null, null, null, null, null), author.Id, now);
        fixture.Context.Clients.Add(client);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        return (author.Id, responsible.Id, client.Id);
    }
}

/// <summary>
/// Attente d'un processus serveur sur un verrou consultatif, constatée par une connexion d'observation en lecture
/// seule : <c>pg_stat_activity</c> (type et nom de l'attente) et <c>pg_blocking_pids</c> (processus bloquant).
/// </summary>
internal static class LockObservation
{
    public sealed record Wait(bool Blocked, string? WaitEventType, string? WaitEvent, int[] BlockingPids, string Locks)
    {
        public override string ToString() =>
            $"wait_event_type={WaitEventType ?? "∅"} wait_event={WaitEvent ?? "∅"} blocking=[{string.Join(",", BlockingPids)}] locks={Locks}";
    }

    public static async Task<Wait> WaitUntilBlockedAsync(int waiterPid, int holderPid, ITestOutputHelper output, int timeoutSeconds = 15)
    {
        await using var observer = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
        await observer.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        Wait last;
        do
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT a.wait_event_type, a.wait_event, pg_blocking_pids(a.pid),
                       coalesce((SELECT string_agg(format('%s/%s/%s/%s/%s', l.locktype, l.classid, l.objid, l.objsubid, l.granted), ';')
                                 FROM pg_locks l WHERE l.pid = a.pid AND l.locktype = 'advisory'), '')
                FROM pg_stat_activity a WHERE a.pid = @pid
                """,
                observer);
            command.Parameters.AddWithValue("pid", waiterPid);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    return new Wait(false, null, null, [], "processus introuvable");
                }

                var type = reader.IsDBNull(0) ? null : reader.GetString(0);
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                var blocking = reader.GetFieldValue<int[]>(2);
                last = new Wait(type == "Lock" && name == "advisory" && blocking.Contains(holderPid), type, name, blocking, reader.GetString(3));
            }

            if (last.Blocked)
            {
                output.WriteLine($"poste B (pid {waiterPid}) en attente du poste A (pid {holderPid}) : {last}");
                return last;
            }

            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);

        output.WriteLine($"poste B (pid {waiterPid}) non bloqué après {timeoutSeconds} s : {last}");
        return last;
    }
}
