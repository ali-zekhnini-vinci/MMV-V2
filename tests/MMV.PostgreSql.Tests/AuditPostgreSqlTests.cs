using Microsoft.EntityFrameworkCore;
using MMV.Application.Appointments;
using MMV.Application.Audit;
using MMV.Application.Clients;
using MMV.Domain.Appointments;
using MMV.Domain.Audit;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Infrastructure.Persistence;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Journal d'audit (03-D2) sur PostgreSQL 18 réel, mmv_v2_test, transaction annulée. Tant que 0005_AuditJournal n'est
/// pas appliquée (revue du propriétaire), son script exact est exécuté DANS la transaction du test
/// (<see cref="RolledBackScope.AuditJournalSimulated"/>) puis annulé : rien n'est conservé. Prouve : événement écrit
/// par le même enregistrement que la modification ; échec de l'un = absence de l'autre ; conflit de version sans
/// événement ; journal en ajout seul ; lecture triée avec l'auteur ; aucune valeur personnelle stockée.
/// </summary>
[Trait("Stage", "AuditWrite")]
public sealed class AuditPostgreSqlTests(ITestOutputHelper output)
{
    private static readonly ClientInput Eloise = new("Éloïse", "Lefèvre", "1985-04-12", "0470 12 34 56", "eloise@exemple.be", null, "Namur", "5000", "Note fictive");

    private static Task<long> CountAsync(RolledBackScope fixture, Guid clientId) =>
        fixture.ScalarAsync<long>("SELECT count(*) FROM audit.events WHERE client_id = @c", ("c", clientId));

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ClientWrites_InsertTheirEventInTheSameSave_WithActorSessionAndNoPersonalValue()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        output.WriteLine($"0005 simulée dans la transaction : {fixture.AuditJournalSimulated}");
        var author = await AddUserAsync(fixture, "Auteur Audit PG");
        var service = fixture.Get<ClientService>();

        var client = (await service.CreateAsync(Eloise, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        var updated = (await service.UpdateAsync(client.Id, Eloise with { City = "Liège", Phone = "0470 99 99 99" }, client.Version, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        // Conflit de version (xmin) : refus SANS événement.
        Assert.Equal(ClientWriteStatus.VersionConflict, (await service.UpdateAsync(client.Id, Eloise with { City = "Mons" }, client.Version, author.Id, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        await service.ArchiveAsync(client.Id, updated.Version, author.Id, CancellationToken.None);

        var rows = await fixture.Context.AuditEvents.AsNoTracking().Where(e => e.ClientId == client.Id).OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id).ToListAsync();
        Assert.Equal([AuditActions.ClientCreated, AuditActions.ClientUpdated, AuditActions.ClientArchived], rows.Select(r => r.Action));
        Assert.All(rows, r => Assert.Equal(author.Id, r.ActorUserId));
        Assert.Equal(["phone", "city"], AuditChanges.Deserialize(rows[1].Changes).Select(c => c.Field));
        var stored = await fixture.ScalarAsync<string>("SELECT string_agg(coalesce(changes::text, ''), ' ') FROM audit.events WHERE client_id = @c", ("c", client.Id));
        foreach (var personal in new[] { "Éloïse", "Lefèvre", "0470", "eloise@", "Namur", "Liège", "Note fictive", "1985" })
        {
            Assert.DoesNotContain(personal, stored, StringComparison.Ordinal);
        }

        Assert.Equal("array", await fixture.ScalarAsync<string>("SELECT jsonb_typeof(changes) FROM audit.events WHERE id = @i", ("i", rows[0].Id)));
        output.WriteLine($"{rows.Count} événements ; champs : {stored}");
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task FailedSave_WritesNeitherTheBusinessChangeNorItsEvent()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Audit PG");
        var existing = (await fixture.Get<ClientService>().CreateAsync(Eloise, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        var before = await fixture.ScalarAsync<long>("SELECT count(*) FROM audit.events");
        var transaction = fixture.Context.Database.CurrentTransaction!;

        // (1) La modification métier échoue (clé primaire déjà prise) : l'événement préparé n'est pas écrit.
        await transaction.CreateSavepointAsync("business_failure");
        var recorder = fixture.Get<IAuditRecorder>();
        var duplicate = Client.Create(existing.Id, new ClientDetails("Double", "Fictif", null, null, null, null, null, null, null), author.Id, DateTimeOffset.UtcNow);
        fixture.Context.Clients.Add(duplicate);
        recorder.Record(new AuditRecord(author.Id, DateTimeOffset.UtcNow, existing.Id, AuditActions.ClientsModule, AuditActions.ClientCreated, AuditActions.ClientEntity, existing.Id, []));
        var businessError = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
        Assert.Equal("23505", ((PostgresException)businessError.InnerException!).SqlState);
        await transaction.RollbackToSavepointAsync("business_failure");
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(before, await fixture.ScalarAsync<long>("SELECT count(*) FROM audit.events"));

        // (2) L'événement échoue (auteur inexistant, clé étrangère) : la modification métier n'est pas écrite non plus.
        await transaction.CreateSavepointAsync("audit_failure");
        var orphan = Client.Create(Guid.CreateVersion7(), new ClientDetails("Orphelin", "Fictif", null, null, null, null, null, null, null), author.Id, DateTimeOffset.UtcNow);
        fixture.Context.Clients.Add(orphan);
        recorder.Record(new AuditRecord(Guid.CreateVersion7(), DateTimeOffset.UtcNow, orphan.Id, AuditActions.ClientsModule, AuditActions.ClientCreated, AuditActions.ClientEntity, orphan.Id, []));
        var auditError = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
        Assert.Equal("23503", ((PostgresException)auditError.InnerException!).SqlState);
        await transaction.RollbackToSavepointAsync("audit_failure");
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(0L, await fixture.ScalarAsync<long>("SELECT count(*) FROM clients.clients WHERE id = @i", ("i", orphan.Id)));
        Assert.Equal(before, await fixture.ScalarAsync<long>("SELECT count(*) FROM audit.events"));
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task AppointmentWrites_AreAuditedInTheirTransaction_RefusalsAreNot()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Audit PG");
        var alice = await AddUserAsync(fixture, "Responsable Audit PG", eligible: true);
        var client = (await fixture.Get<ClientService>().CreateAsync(Eloise, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        var service = new AppointmentService(fixture.Get<IAppointmentStore>(), AppointmentsPostgreSqlTests.Settings, TimeProvider.System, fixture.Get<IAuditRecorder>());
        var ten = AppointmentsPostgreSqlTests.Ten;
        var created = (await service.CreateAsync(AppointmentsPostgreSqlTests.In(client.Id, alice.Id, ten, 30, "conseil_devis", "Note"), author.Id, CancellationToken.None)).Appointment!.Appointment;
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.SlotConflict,
            (await service.CreateAsync(AppointmentsPostgreSqlTests.In(client.Id, alice.Id, ten.AddMinutes(15), 30, "conseil_devis"), author.Id, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        var moved = (await service.UpdateAsync(created.Id, AppointmentsPostgreSqlTests.In(client.Id, null, ten.AddHours(1), 30, "retrait_commande", "Note"), created.Version, author.Id, CancellationToken.None)).Appointment!.Appointment;
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentWriteStatus.VersionConflict, (await service.CancelAsync(created.Id, created.Version, null, author.Id, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        await service.CancelAsync(moved.Id, moved.Version, "Motif fictif", author.Id, CancellationToken.None);

        var page = await fixture.Get<AuditService>().ListForClientAsync(new AuditQuery(client.Id, AuditActions.AppointmentsModule, null, null, 1, 20), CancellationToken.None);
        Assert.Equal(
            [AuditActions.AppointmentCancelled, AuditActions.AppointmentUpdated, AuditActions.AppointmentResponsibleChanged, AuditActions.AppointmentMoved, AuditActions.AppointmentCreated],
            page.Items.Select(i => i.Event.Action));
        Assert.All(page.Items, i => Assert.Equal("Auteur Audit PG", i.ActorDisplayName));
        var responsible = AuditChanges.Deserialize(page.Items[2].Event.Changes).Single();
        Assert.Equal((alice.Id.ToString(), (string?)null), (responsible.Old, responsible.New));
        Assert.Equal(5, page.TotalCount);
        var clientsOnly = await fixture.Get<AuditService>().ListForClientAsync(new AuditQuery(client.Id, AuditActions.ClientsModule, null, null, 1, 20), CancellationToken.None);
        Assert.Equal([AuditActions.ClientCreated], clientsOnly.Items.Select(i => i.Event.Action));
        var names = await fixture.Get<AuditService>().DisplayNamesAsync([alice.Id], CancellationToken.None);
        Assert.Equal("Responsable Audit PG", names[alice.Id]);
    }

    /// <summary>
    /// 03-D3 : modification sans changement réel (client, rendez-vous) → aucune ligne réécrite. Dans une transaction
    /// unique, xmin ne distingue pas une réécriture : la preuve est l'emplacement physique (<c>ctid</c>) inchangé, que
    /// toute mise à jour PostgreSQL déplace (nouvelle version de ligne), avec la date de modification et le journal.
    /// La version xmin validée est contrôlée par les matrices HTTP réelles (Stage=ClientsWrite / AppointmentsWrite).
    /// </summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task UpdatesWithoutRealChange_RewriteNoRow_AndRecordNoEvent()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Audit PG");
        var other = await AddUserAsync(fixture, "Autre Auteur PG");
        var alice = await AddUserAsync(fixture, "Responsable Audit PG", eligible: true);
        var clients = fixture.Get<ClientService>();
        var client = (await clients.CreateAsync(Eloise, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        var appointments = new AppointmentService(fixture.Get<IAppointmentStore>(), AppointmentsPostgreSqlTests.Settings, TimeProvider.System, fixture.Get<IAuditRecorder>());
        var input = AppointmentsPostgreSqlTests.In(client.Id, alice.Id, AppointmentsPostgreSqlTests.Ten, 30, "conseil_devis", "Note");
        var appointment = (await appointments.CreateAsync(input, author.Id, CancellationToken.None)).Appointment!.Appointment;
        fixture.Context.ChangeTracker.Clear();

        const string clientRow = "SELECT ctid::text || '|' || updated_at_utc::text || '|' || updated_by_user_id::text FROM clients.clients WHERE id = @i";
        const string appointmentRow = "SELECT ctid::text || '|' || updated_at_utc::text || '|' || updated_by_user_id::text FROM appointments.appointments WHERE id = @i";
        var (clientBefore, appointmentBefore) = (await fixture.ScalarAsync<string>(clientRow, ("i", client.Id)), await fixture.ScalarAsync<string>(appointmentRow, ("i", appointment.Id)));
        var events = await CountAsync(fixture, client.Id);

        var sameClient = await clients.UpdateAsync(client.Id, Eloise with { FirstName = " Éloïse ", City = "Namur  " }, client.Version, other.Id, CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();
        var sameAppointment = await appointments.UpdateAsync(appointment.Id, input with { Notes = "  Note " }, appointment.Version, other.Id, CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();

        Assert.Equal((ClientWriteStatus.Success, client.Version), (sameClient.Status, sameClient.Client!.Version));
        Assert.Equal((AppointmentWriteStatus.Success, appointment.Version), (sameAppointment.Status, sameAppointment.Appointment!.Appointment.Version));
        Assert.Equal(clientBefore, await fixture.ScalarAsync<string>(clientRow, ("i", client.Id)));
        Assert.Equal(appointmentBefore, await fixture.ScalarAsync<string>(appointmentRow, ("i", appointment.Id)));
        Assert.Equal(events, await CountAsync(fixture, client.Id));

        // Témoin : un changement réel déplace la ligne et produit son événement.
        await clients.UpdateAsync(client.Id, Eloise with { City = "Liège" }, client.Version, other.Id, CancellationToken.None);
        Assert.NotEqual(clientBefore, await fixture.ScalarAsync<string>(clientRow, ("i", client.Id)));
        Assert.Equal(events + 1, await CountAsync(fixture, client.Id));
        output.WriteLine($"client {clientBefore} ; rendez-vous {appointmentBefore} ; événements {events} → {events + 1} après le témoin");
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task Journal_IsAppendOnly_AndAcceptsTheAuditReadPermission()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Audit PG");
        var client = (await fixture.Get<ClientService>().CreateAsync(Eloise, author.Id, CancellationToken.None)).Client!;
        Assert.Equal(1L, await CountAsync(fixture, client.Id));

        await fixture.ExpectSqlStateAsync("42501", $"UPDATE audit.events SET action = 'client.updated' WHERE client_id = '{client.Id}'");
        await fixture.ExpectSqlStateAsync("42501", $"DELETE FROM audit.events WHERE client_id = '{client.Id}'");
        await fixture.ExpectSqlStateAsync("42501", "TRUNCATE audit.events");
        await fixture.ExpectSqlStateAsync("23514", $"INSERT INTO audit.events (id, occurred_at_utc, actor_user_id, module, action, entity_type, entity_id) VALUES (gen_random_uuid(), now(), '{author.Id}', 'clients', 'Client Créé', 'client', gen_random_uuid())");
        await fixture.ExpectSqlStateAsync("23514", $"INSERT INTO audit.events (id, occurred_at_utc, actor_user_id, module, action, entity_type, entity_id, changes) VALUES (gen_random_uuid(), now(), '{author.Id}', 'clients', 'client.created', 'client', gen_random_uuid(), '{{}}')");
        Assert.Equal(1L, await CountAsync(fixture, client.Id));

        await fixture.ExecuteAsync($"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author.Id}', '{Permissions.AuditRead}')");
        await fixture.ExpectSqlStateAsync("23514", $"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author.Id}', 'audit.write')");
    }

    /// <summary>
    /// Le script exact 0005 produit EXACTEMENT l'état attendu par <see cref="SchemaState.AuditJournalDifferences"/>
    /// (relations, colonnes, contraintes, index, fonction, déclencheurs, historique), relu dans les catalogues de la
    /// transaction annulée : le contrôle « PostMigration » sera juste le jour où le propriétaire appliquera 0005.
    /// Phase 04-C (O-04-50) : sur une base déjà migrée en 0006, l'état attendu est celui de 0006 (le script 0005,
    /// gardé par l'historique, n'y change rien).
    /// </summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task AuditJournalScript_ProducesExactlyTheExpectedSchema()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var connection = (NpgsqlConnection)fixture.Context.Database.GetDbConnection();
        var opticalApplied = await fixture.ScalarAsync<bool>("SELECT to_regclass('optical.prescriptions') IS NOT NULL");
        IReadOnlyList<string> Differences(SchemaState s) => opticalApplied
            ? s.OpticalModuleDifferences(PostgreSqlTarget.Test.Role)
            : s.AuditJournalDifferences(PostgreSqlTarget.Test.Role);

        var state = await SchemaInspection.ReadAsync(connection, CancellationToken.None);

        output.WriteLine($"0005 simulée : {fixture.AuditJournalSimulated} ; 0006 appliquée : {opticalApplied}");
        Assert.Empty(Differences(state));

        // Idempotence : une seconde exécution du script à appliquer ne change rien.
        await fixture.ExecutePendingScriptAsync(RolledBackScope.AuditJournalScript);
        var again = await SchemaInspection.ReadAsync(connection, CancellationToken.None);
        Assert.Empty(Differences(again));
        Assert.Equal(state.ToString(), again.ToString());
    }

    private static async Task<UserAccount> AddUserAsync(RolledBackScope fixture, string displayName, bool eligible = false)
    {
        var user = new UserAccount(Guid.CreateVersion7(), $"pg-d-{Guid.NewGuid():N}"[..20], displayName, DateTimeOffset.UtcNow);
        user.SetPasswordHash("hachage-factice");
        fixture.Context.Users.Add(user);
        if (eligible)
        {
            fixture.Context.AppointmentResponsibles.Add(AppointmentResponsible.Enable(user.Id, DateTimeOffset.UtcNow));
        }

        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        return user;
    }
}
