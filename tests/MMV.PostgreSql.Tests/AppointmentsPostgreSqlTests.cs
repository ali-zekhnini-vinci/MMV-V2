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
/// 0004_AppointmentsModule appliquée (revue SQL et GO du propriétaire) : à lancer APRÈS l'application, jamais avant —
/// tant qu'elle ne l'est pas, ces tests sont rapportés NOT_TESTED.
/// Exécution : <c>dotnet test tests/MMV.PostgreSql.Tests --filter "Stage=AppointmentsWrite"</c>.
/// Données exclusivement fictives, dans des transactions ANNULÉES (ROLLBACK) : aucune ligne ne subsiste.
/// Réservations simultanées : deux connexions (deux postes) ; la seconde doit ATTENDRE le verrou consultatif de la
/// première (constaté dans <c>pg_locks</c> par une troisième connexion en lecture seule).
/// </summary>
[Trait("Stage", "AppointmentsWrite")]
public sealed class AppointmentsPostgreSqlTests(ITestOutputHelper output)
{
    // Mercredi 03/03/2027, 10:00 à Bruxelles (UTC+1) : toujours dans le futur à l'exécution prévue.
    private static readonly DateTimeOffset Ten = new(2027, 3, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly AppointmentSettings Settings = AppointmentSettings.From(new AppointmentsOptions
    {
        TimeZone = "Europe/Brussels",
        DisplayStartTime = "08:00",
        DisplayEndTime = "20:00",
        ReceptionCapacity = 1,
    });

    private static string Utc(DateTimeOffset instant) => instant.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task PersonalAppointment_OverlapConsecutiveCancelRestore_OnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var (author, responsible, client) = await SeedAsync(fixture, eligible: true);
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), Settings, TimeProvider.System);

        var created = await service.CreateAsync(new AppointmentInput(client, responsible, Utc(Ten), 30, "conseil_devis", "Note de test"), author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, created.Status);
        var appointment = created.Appointment!.Appointment;
        Assert.NotEqual(0u, appointment.Version);
        output.WriteLine($"rendez-vous {appointment.Id}, xmin={appointment.Version}");

        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.SlotConflict,
            (await service.CreateAsync(new AppointmentInput(client, responsible, Utc(Ten.AddMinutes(15)), 30, "conseil_devis", null), author, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.Success,
            (await service.CreateAsync(new AppointmentInput(client, responsible, Utc(Ten.AddMinutes(30)), 30, "conseil_devis", null), author, CancellationToken.None)).Status);

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
            return (await service.CreateAsync(new AppointmentInput(client, null, Utc(start), minutes, "retrait_commande", null), author, CancellationToken.None)).Status;
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

        var refused = await service.CreateAsync(new AppointmentInput(client, responsible, Utc(Ten), 30, "prise_mesures", null), author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.ValidationFailed, refused.Status);
        Assert.Equal(["not_eligible"], refused.Errors!["responsibleUserId"]);

        await fixture.ExecuteAsync($"UPDATE clients.clients SET archived_at_utc = created_at_utc WHERE id = '{client}'");
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.ClientArchived,
            (await service.CreateAsync(new AppointmentInput(client, null, Utc(Ten), 30, "retrait_commande", null), author, CancellationToken.None)).Status);
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

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task SimultaneousReceptionBookings_TwoConnections_SecondWaitsForTheFirst()
    {
        await using var first = await RolledBackScope.OpenAsync(output);
        await using var second = await RolledBackScope.OpenAsync(output);
        var (authorA, _, clientA) = await SeedAsync(first, eligible: true);
        var (authorB, _, clientB) = await SeedAsync(second, eligible: true);
        var serviceA = new AppointmentService(first.Get<IAppointmentStore>(), Settings, TimeProvider.System);
        var serviceB = new AppointmentService(second.Get<IAppointmentStore>(), Settings, TimeProvider.System);

        // Poste A : réserve la place d'accueil et garde sa transaction ouverte (verrou consultatif tenu).
        var booked = await serviceA.CreateAsync(new AppointmentInput(clientA, null, Utc(Ten), 30, "retrait_commande", null), authorA, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, booked.Status);

        // Poste B : même pool, même créneau → doit attendre le verrou de A.
        var pending = serviceB.CreateAsync(new AppointmentInput(clientB, null, Utc(Ten), 30, "sav_reparation", null), authorB, CancellationToken.None);
        await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.False(pending.IsCompleted, "Le second poste n'a pas attendu le verrou du premier.");

        await using (var observer = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString()))
        {
            await observer.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT count(*)::int FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND classid = 1296913746", observer);
            var waiting = (int)(await command.ExecuteScalarAsync())!;
            output.WriteLine($"verrous consultatifs en attente : {waiting}");
            Assert.Equal(1, waiting);
        }

        // A annule (ROLLBACK) : verrou relâché et place libérée, B obtient le verrou et réserve.
        await first.Context.Database.RollbackTransactionAsync();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
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
            (await service.CreateAsync(new AppointmentInput(client, null, Utc(Ten), 30, "retrait_commande", null), author, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.ReceptionCapacityFull,
            (await service.CreateAsync(new AppointmentInput(client, null, Utc(Ten.AddMinutes(15)), 30, "sav_reparation", null), author, CancellationToken.None)).Status);
    }

    /// <summary>Compte auteur, collaborateur (éligible ou non) et client fictifs, dans la transaction du test.</summary>
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
