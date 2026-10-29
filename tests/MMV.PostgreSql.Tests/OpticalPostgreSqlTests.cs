using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Audit;
using MMV.Application.Clients;
using MMV.Application.Optical;
using MMV.Domain.Audit;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Domain.Optical;
using MMV.Infrastructure.Persistence;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Test exigeant la migration 0006_OpticalModule APPLIQUÉE à la base cible : « Skipped » (NOT_TESTED) tant que le schéma
/// <c>optical</c> n'existe pas (G04-SQL : revue du propriétaire avant toute application), jamais « Passed ».
/// </summary>
public sealed class OpticalPostgreSqlFactAttribute : FactAttribute
{
    public OpticalPostgreSqlFactAttribute()
    {
        Skip = SkipReason();
    }

    internal static string? SkipReason() =>
        !PostgreSqlTarget.Test.IsConfigured ? $"NOT_TESTED : '{PostgreSqlTarget.Test.Key}' non configurée."
        : !OpticalModulePresent() ? "NOT_TESTED : exige la migration 0006_OpticalModule appliquée à mmv_v2_test (G04-SQL, revue du propriétaire)."
        : null;

    private static bool OpticalModulePresent()
    {
        try
        {
            using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
            connection.Open();
            using var command = new NpgsqlCommand("SELECT to_regclass('optical.prescriptions') IS NOT NULL", connection);
            return (bool)command.ExecuteScalar()!;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }
}

/// <summary>Variante paramétrée de <see cref="OpticalPostgreSqlFactAttribute"/> (mêmes conditions, jamais « Passed » sans 0006).</summary>
public sealed class OpticalPostgreSqlTheoryAttribute : TheoryAttribute
{
    public OpticalPostgreSqlTheoryAttribute()
    {
        Skip = OpticalPostgreSqlFactAttribute.SkipReason();
    }
}

/// <summary>
/// Dossier optique (04-B) sur PostgreSQL 18 réel, mmv_v2_test, transaction ANNULÉE (rien n'est conservé). Prouve :
/// enregistrement + événements d'audit dans le même SaveChanges ; valeurs conservées exactement (texte) et valeur
/// numérique exacte dérivée sans arrondi ; contraintes de forme, de cycle et de provenance confirmée ; brouillon unique ;
/// immuabilité (déclencheurs) ; droits optiques admis par la contrainte des permissions ; lecture (listes, détail).
/// Exécution : <c>--filter "Stage=OpticalWrite"</c>, après application de 0006 (G04-SQL).
/// </summary>
[Trait("Stage", "OpticalWrite")]
public sealed class OpticalPostgreSqlTests(ITestOutputHelper output)
{
    private static readonly ClientInput Patient = new("Patient", "Fictif-Optique", null, null, null, null, "Namur", null, null);

    internal static PrescriptionInput Prescription(string? sphere = "-1,25", string? cylinder = null, string? axis = null, string? notes = null) =>
        new(new PrescriptionProvenanceInput("Dr Fictif-PG", null, null, "2026-10-20", "2026-10-21"), new EyeInput(Sphere: sphere, Cylinder: cylinder, Axis: axis), null, notes);

    [OpticalPostgreSqlFact]
    public async Task CreateAndConfirm_WritesRecordRevisionAndEventsInOneSave_ValuesExact()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Optique PG");
        var client = (await fixture.Get<ClientService>().CreateAsync(Patient, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Get<PrescriptionService>().CreateAsync(
            client.Id, Prescription("−2,125", "-0,50", "180", "note fictive"), confirm: true, Guid.CreateVersion7(), author.Id, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Success, result.Status);
        Assert.Matches("^RX-[0-9]{6,}$", result.Record!.Reference);
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM optical.prescription_revisions WHERE prescription_id = @i AND status = 'confirmed' AND revision_no = 1", ("i", result.Record.Id)));
        Assert.Equal("−2,125", await fixture.ScalarAsync<string>("SELECT od_sphere FROM optical.prescription_revisions WHERE prescription_id = @i", ("i", result.Record.Id)));
        Assert.Equal(-2.125m, await fixture.ScalarAsync<decimal>("SELECT od_sphere_value FROM optical.prescription_revisions WHERE prescription_id = @i", ("i", result.Record.Id)));
        Assert.Equal("180", await fixture.ScalarAsync<string>("SELECT od_axis FROM optical.prescription_revisions WHERE prescription_id = @i", ("i", result.Record.Id)));
        Assert.True(await fixture.ScalarAsync<bool>("SELECT og_sphere IS NULL AND og_sphere_value IS NULL FROM optical.prescription_revisions WHERE prescription_id = @i", ("i", result.Record.Id)));
        Assert.Equal("prescription.created,prescription.confirmed", await fixture.ScalarAsync<string>(
            "SELECT string_agg(action, ',' ORDER BY occurred_at_utc, id) FROM audit.events WHERE entity_id = @i", ("i", result.Record.Id)));
        Assert.False(await fixture.ScalarAsync<bool>(
            "SELECT bool_or(changes::text LIKE '%Dr Fictif%' OR changes::text LIKE '%2,125%' OR changes::text LIKE '%note fictive%') FROM audit.events WHERE entity_id = @i",
            ("i", result.Record.Id)));
    }

    [OpticalPostgreSqlFact]
    public async Task Refusals_AndUnchangedSaves_WriteNothing()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Optique PG");
        var client = (await fixture.Get<ClientService>().CreateAsync(Patient, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        var service = fixture.Get<PrescriptionService>();

        var incomplete = await service.CreateAsync(client.Id, Prescription("0,00"), confirm: true, Guid.CreateVersion7(), author.Id, CancellationToken.None);
        Assert.Equal(OpticalWriteStatus.Incomplete, incomplete.Status);
        Assert.Equal(0L, await fixture.ScalarAsync<long>("SELECT count(*) FROM optical.prescriptions WHERE client_id = @c", ("c", client.Id)));

        var draft = (await service.CreateAsync(client.Id, Prescription(), confirm: false, Guid.CreateVersion7(), author.Id, CancellationToken.None)).Record!;
        fixture.Context.ChangeTracker.Clear();
        var events = await fixture.ScalarAsync<long>("SELECT count(*) FROM audit.events WHERE entity_id = @i", ("i", draft.Id));
        var xmin = await fixture.ScalarAsync<long>("SELECT xmin::text::bigint FROM optical.prescriptions WHERE id = @i", ("i", draft.Id));

        var unchanged = await service.SaveDraftAsync(draft.Id, Prescription(), false, draft.Version, Guid.CreateVersion7(), author.Id, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Success, unchanged.Status);
        Assert.Equal(events, await fixture.ScalarAsync<long>("SELECT count(*) FROM audit.events WHERE entity_id = @i", ("i", draft.Id)));
        Assert.Equal(xmin, await fixture.ScalarAsync<long>("SELECT xmin::text::bigint FROM optical.prescriptions WHERE id = @i", ("i", draft.Id)));
    }

    [OpticalPostgreSqlFact]
    public async Task Constraints_AndTriggers_ProtectTheModel()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Optique PG");
        var client = (await fixture.Get<ClientService>().CreateAsync(Patient, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        var service = fixture.Get<PrescriptionService>();
        var confirmed = (await service.CreateAsync(client.Id, Prescription(), true, Guid.CreateVersion7(), author.Id, CancellationToken.None)).Record!;
        var invalidated = (await service.CreateAsync(client.Id, Prescription(), true, Guid.CreateVersion7(), author.Id, CancellationToken.None)).Record!;
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(OpticalWriteStatus.Success,
            (await service.InvalidateAsync(invalidated.Id, "duplicate", null, invalidated.Version, Guid.CreateVersion7(), author.Id, CancellationToken.None)).Status);
        var revision = $"(SELECT id FROM optical.prescription_revisions WHERE prescription_id = '{confirmed.Id}')";

        // Immuabilité : révision confirmée, suppression, troncature, enregistrement invalidé.
        await fixture.ExpectSqlStateAsync("42501", $"UPDATE optical.prescription_revisions SET od_sphere = '-9' WHERE id = {revision}");
        await fixture.ExpectSqlStateAsync("42501", $"DELETE FROM optical.prescription_revisions WHERE id = {revision}");
        await fixture.ExpectSqlStateAsync("42501", $"DELETE FROM optical.prescriptions WHERE id = '{confirmed.Id}'");
        await fixture.ExpectSqlStateAsync("42501", "TRUNCATE optical.prescription_revisions");
        await fixture.ExpectSqlStateAsync("42501", $"UPDATE optical.prescriptions SET updated_at_utc = now() WHERE id = '{invalidated.Id}'");

        // Forme, cycle et provenance confirmée (garde de base, en plus du service).
        var insert = "INSERT INTO optical.prescription_revisions (id, prescription_id, status, started_at_utc, started_by_user_id, updated_at_utc, updated_by_user_id";
        await fixture.ExpectSqlStateAsync("23514", $"{insert}, od_sphere) VALUES (gen_random_uuid(), '{confirmed.Id}', 'draft', now(), '{author.Id}', now(), '{author.Id}', '1e3')");
        await fixture.ExpectSqlStateAsync("23514", $"{insert}, revision_no, confirmed_at_utc, confirmed_by_user_id) VALUES (gen_random_uuid(), '{confirmed.Id}', 'confirmed', now(), '{author.Id}', now(), '{author.Id}', 2, now(), '{author.Id}')");
        await fixture.ExpectSqlStateAsync("23514", $"{insert}, discarded_at_utc, discarded_by_user_id) VALUES (gen_random_uuid(), '{confirmed.Id}', 'discarded', now(), '{author.Id}', now(), '{author.Id}', now(), '{author.Id}')");
        await fixture.ExpectSqlStateAsync("23514", $"{insert}) VALUES (gen_random_uuid(), '{confirmed.Id}', 'archived', now(), '{author.Id}', now(), '{author.Id}')");

        // Au plus un brouillon ouvert par enregistrement.
        await fixture.ExecuteAsync($"{insert}, base_revision_no, reason_code) VALUES (gen_random_uuid(), '{confirmed.Id}', 'draft', now(), '{author.Id}', now(), '{author.Id}', 1, 'other')");
        await fixture.ExpectSqlStateAsync("23505", $"{insert}, base_revision_no, reason_code) VALUES (gen_random_uuid(), '{confirmed.Id}', 'draft', now(), '{author.Id}', now(), '{author.Id}', 1, 'other')");

        // Quatre droits optiques admis, aucun droit documentaire (D-04-16).
        foreach (var permission in new[] { Permissions.OpticalRead, Permissions.OpticalPrescriptionsWrite, Permissions.OpticalMeasurementsWrite, Permissions.OpticalInvalidate })
        {
            await fixture.ExecuteAsync($"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author.Id}', '{permission}')");
        }

        await fixture.ExpectSqlStateAsync("23514", $"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author.Id}', 'optical.documents.read')");
    }

    [OpticalPostgreSqlFact]
    public async Task Reads_ListsDetailAndMeasurers_OnRealPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture, "Auteur Optique PG");
        var measurer = await AddUserAsync(fixture, "Opticien Fictif PG", Permissions.OpticalMeasurementsWrite);
        var client = (await fixture.Get<ClientService>().CreateAsync(Patient, author.Id, CancellationToken.None)).Client!;
        fixture.Context.ChangeTracker.Clear();
        var prescription = (await fixture.Get<PrescriptionService>().CreateAsync(client.Id, Prescription(), true, Guid.CreateVersion7(), author.Id, CancellationToken.None)).Record!;
        fixture.Context.ChangeTracker.Clear();
        var measurement = await fixture.Get<MeasurementService>().CreateAsync(
            client.Id, new MeasurementInput(new MeasurementProvenanceInput("2026-10-27", measurer.Id, null), null, new EyeInput(Acuity: "9/10"), null),
            confirm: false, Guid.CreateVersion7(), author.Id, CancellationToken.None);
        Assert.Equal(OpticalWriteStatus.Success, measurement.Status);
        fixture.Context.ChangeTracker.Clear();
        var reads = fixture.Get<OpticalQueryService>();

        var forClient = await reads.ListForClientAsync(client.Id, includeInvalidated: false, CancellationToken.None);
        var page = await reads.ListAsync(new OpticalListQuery("fictif-pg", null, OpticalStatusFilter.All, false, OpticalSortField.Date, true, 1, 20), CancellationToken.None);
        var detail = await reads.GetPrescriptionAsync(prescription.Id, CancellationToken.None);

        Assert.Equal(2, forClient.Count);
        Assert.Contains(forClient, s => s.Kind == OpticalKind.Measurement && s.Status == OpticalDisplayStatus.Draft && s.Source == "Opticien Fictif PG" && s.Values.Og.Acuity == "9/10");
        Assert.Contains(page.Items, s => s.Id == prescription.Id && s.Status == OpticalDisplayStatus.Confirmed && s.RevisionNo == 1 && s.Values.Od.Sphere == "-1,25");
        Assert.Equal(1, detail!.LastConfirmed!.RevisionNo);
        Assert.Contains(await reads.ListMeasurersAsync(CancellationToken.None), m => m.Id == measurer.Id);
        Assert.DoesNotContain(await reads.ListMeasurersAsync(CancellationToken.None), m => m.Id == author.Id);

        var audit = await fixture.Get<AuditService>().ListForClientAsync(
            new AuditQuery(client.Id, null, null, null, 1, 50) { ExcludedModules = [AuditActions.OpticalModule] }, CancellationToken.None);
        Assert.DoesNotContain(audit.Items, e => e.Event.Module == AuditActions.OpticalModule);
    }

    private static async Task<UserAccount> AddUserAsync(RolledBackScope fixture, string displayName, params string[] permissions)
    {
        var user = new UserAccount(Guid.CreateVersion7(), $"pg-o-{Guid.NewGuid():N}"[..20], displayName, DateTimeOffset.UtcNow);
        user.SetPasswordHash("hachage-factice");
        foreach (var permission in permissions)
        {
            user.Grant(permission);
        }

        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        return user;
    }
}

/// <summary>
/// Concurrence RÉELLE (deux connexions, COMMIT) sur mmv_v2_test : deux postes enregistrent le même brouillon avec la
/// même version — un seul réussit, l'autre reçoit un conflit sans écriture ni événement ; deux réémissions simultanées
/// d'une même création — un seul enregistrement ; deux confirmations simultanées — une seule révision confirmée.
/// Nettoyage démontré : enregistrements optiques fictifs (déclencheurs désactivés dans UNE transaction, propriétaire),
/// puis événements, clients et comptes fictifs ; résidu 0.
/// </summary>
[Trait("Stage", "OpticalWrite")]
public sealed class OpticalConcurrencyPostgreSqlTests(ITestOutputHelper output)
{
    private static readonly string[] Spheres = ["-1,50", "-1,75"];

    [OpticalPostgreSqlFact]
    public async Task TwoStations_SameDraftAndSameRequests_NeverProduceDuplicatesOrTwoConfirmations()
    {
        var data = CommittedTestData.Create(output);
        try
        {
            var author = await data.AddUserAsync("optique", eligible: false, null, Permissions.ClientsRead, Permissions.OpticalRead, Permissions.OpticalPrescriptionsWrite);
            var clientId = await data.AddClientAsync(author, "Optique");

            // Deux réémissions simultanées de la même création (double clic, nouvelle tentative réseau).
            var requestId = Guid.CreateVersion7();
            var creations = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var scope = data.NewScope();
                return await scope.ServiceProvider.GetRequiredService<PrescriptionService>().CreateAsync(
                    clientId, OpticalPostgreSqlTests.Prescription(), confirm: true, requestId, author, CancellationToken.None);
            }));
            Assert.All(creations, c => Assert.Equal(OpticalWriteStatus.Success, c.Status));
            Assert.Equal(1, await CommittedTestData.CountAsync("SELECT count(*) FROM optical.prescriptions WHERE client_id = @c", ("c", clientId)));
            Assert.Equal(1, await CommittedTestData.CountAsync("SELECT count(*) FROM optical.prescription_revisions r JOIN optical.prescriptions p ON p.id = r.prescription_id WHERE p.client_id = @c AND r.status = 'confirmed'", ("c", clientId)));

            // Rectification ouverte, puis deux postes confirment simultanément avec la même version.
            var id = creations[0].Record!.Id;
            uint version;
            await using (var scope = data.NewScope())
            {
                var record = (await scope.ServiceProvider.GetRequiredService<OpticalQueryService>().GetPrescriptionAsync(id, CancellationToken.None))!;
                var started = await scope.ServiceProvider.GetRequiredService<PrescriptionService>().StartRectificationAsync(
                    id, 1, "transcription_error", null, record.Version, Guid.CreateVersion7(), author, CancellationToken.None);
                Assert.Equal(OpticalWriteStatus.Success, started.Status);
                version = started.Record!.Version;
            }

            var events = await CommittedTestData.CountAsync("SELECT count(*) FROM audit.events WHERE entity_id = @i", ("i", id));
            var confirmations = await Task.WhenAll(Spheres.Select(async sphere =>
            {
                await using var scope = data.NewScope();
                return await scope.ServiceProvider.GetRequiredService<PrescriptionService>().SaveDraftAsync(
                    id, OpticalPostgreSqlTests.Prescription(sphere), confirm: true, version, Guid.CreateVersion7(), author, CancellationToken.None);
            }));

            Assert.Equal(1, confirmations.Count(c => c.Status == OpticalWriteStatus.Success));
            Assert.Equal(1, confirmations.Count(c => c.Status == OpticalWriteStatus.VersionConflict));
            Assert.Equal(2, await CommittedTestData.CountAsync("SELECT count(*) FROM optical.prescription_revisions WHERE prescription_id = @i AND status = 'confirmed'", ("i", id)));
            Assert.Equal(0, await CommittedTestData.CountAsync("SELECT count(*) FROM optical.prescription_revisions WHERE prescription_id = @i AND status = 'draft'", ("i", id)));
            Assert.Equal(events + 2, await CommittedTestData.CountAsync("SELECT count(*) FROM audit.events WHERE entity_id = @i", ("i", id)));
        }
        finally
        {
            var purged = await OpticalCleanup.PurgeAsync(data.Users, data.Clients);
            output.WriteLine($"nettoyage optique : {purged} ligne(s)");
            await data.DisposeAsync();
            Assert.Equal(0, data.Residue);
            Assert.Equal(0, await OpticalCleanup.CountAsync(data.Users, data.Clients));
        }
    }
}

/// <summary>
/// Nettoyage des enregistrements optiques FICTIFS validés sur <c>mmv_v2_test</c> UNIQUEMENT (cible relue côté serveur).
/// Les déclencheurs d'immuabilité sont désactivés puis réactivés dans UNE transaction par le rôle propriétaire et leur
/// état est contrôlé avant validation (limite documentée : ils protègent contre une erreur applicative, pas contre le
/// propriétaire). Jamais exécuté sur <c>mmv_v2_dev</c>.
/// </summary>
internal static class OpticalCleanup
{
    private static readonly string[] Tables = ["prescription_revisions", "prescriptions", "measurement_revisions", "measurements"];

    public static async Task<int> PurgeAsync(IReadOnlyCollection<Guid> users, IReadOnlyCollection<Guid> clients)
    {
        await using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await Execute("SELECT 1 WHERE current_database() = 'mmv_v2_test' AND current_user = 'mmv_v2_test' AND inet_server_port() = 5433", expectRow: true);
        foreach (var table in Tables)
        {
            await Execute($"ALTER TABLE optical.{table} DISABLE TRIGGER USER");
        }

        var deleted = 0;
        foreach (var (records, revisions, column) in new[] { ("prescriptions", "prescription_revisions", "prescription_id"), ("measurements", "measurement_revisions", "measurement_id") })
        {
            deleted += await Execute($"DELETE FROM optical.{revisions} WHERE {column} IN (SELECT id FROM optical.{records} WHERE client_id = ANY(@c) OR created_by_user_id = ANY(@u))");
            deleted += await Execute($"DELETE FROM optical.{records} WHERE client_id = ANY(@c) OR created_by_user_id = ANY(@u)");
        }

        foreach (var table in Tables)
        {
            await Execute($"ALTER TABLE optical.{table} ENABLE TRIGGER USER");
        }

        await using (var state = new NpgsqlCommand(
            "SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'optical' AND NOT t.tgisinternal AND t.tgenabled <> 'O'",
            connection, transaction))
        {
            Assert.Equal(0L, (long)(await state.ExecuteScalarAsync())!);
        }

        await transaction.CommitAsync();
        return deleted;

        async Task<int> Execute(string sql, bool expectRow = false)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("u", users.ToArray());
            command.Parameters.AddWithValue("c", clients.ToArray());
            if (expectRow)
            {
                Assert.NotNull(await command.ExecuteScalarAsync());
                return 0;
            }

            return await command.ExecuteNonQueryAsync();
        }
    }

    public static Task<long> CountAsync(IReadOnlyCollection<Guid> users, IReadOnlyCollection<Guid> clients) =>
        CommittedTestData.CountAsync(
            "SELECT (SELECT count(*) FROM optical.prescriptions WHERE client_id = ANY(@c) OR created_by_user_id = ANY(@u)) + (SELECT count(*) FROM optical.measurements WHERE client_id = ANY(@c) OR created_by_user_id = ANY(@u))",
            ("u", users.ToArray()), ("c", clients.ToArray()));
}
