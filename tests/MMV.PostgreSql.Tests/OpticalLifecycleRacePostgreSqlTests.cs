using System.Data;
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
/// O-04-53 / O-04-54 (audit final 04-C) : écriture optique ‖ archivage du client, et confirmation de mesure ‖ désactivation
/// ou retrait d'habilitation du collaborateur, sur PostgreSQL 18 RÉEL (<c>mmv_v2_test</c>, deux connexions, COMMIT réels,
/// données fictives purgées). Les DEUX ordres de validation sont produits par des barrières déterministes, sans délai :
/// <list type="bullet">
/// <item><b>l'autre gagne</b> : le poste A a passé son contrôle (lecture du client ou du collaborateur) et s'arrête juste
/// après ; l'archivage, la désactivation ou le retrait est VALIDÉ ; A reprend → refus, rien n'est écrit (ni ligne, ni
/// version, ni événement d'audit) ;</item>
/// <item><b>l'écriture gagne</b> : A a écrit dans une transaction encore ouverte ; l'archivage, la désactivation ou le
/// retrait doit ATTENDRE (constaté sur son processus serveur : <c>pg_blocking_pids</c>) ; A valide ; l'autre opération
/// aboutit ensuite. Les écritures suivantes sont refusées, la consultation, l'invalidation, le nom du collaborateur et
/// les rectifications conservant le collaborateur historique restent permises.</item>
/// </list>
/// Exécution : <c>--filter "Stage=OpticalWrite"</c>.
/// </summary>
[Trait("Stage", "OpticalWrite")]
public sealed class OpticalLifecycleRacePostgreSqlTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public enum Write
    {
        Create,
        EditDraft,
        ConfirmDraft,
        StartRectification,
        DiscardRectification,
        CreateMeasurement,
    }

    public enum Loss
    {
        Deactivation,
        PermissionRevoked,
    }

    /// <summary>Événements d'audit optiques attendus de chaque écriture réussie.</summary>
    private static int ExpectedEvents(Write write) => write is Write.Create or Write.CreateMeasurement ? 2 : 1;

    [OpticalPostgreSqlTheory]
    [InlineData(Write.Create)]
    [InlineData(Write.EditDraft)]
    [InlineData(Write.ConfirmDraft)]
    [InlineData(Write.StartRectification)]
    [InlineData(Write.DiscardRectification)]
    [InlineData(Write.CreateMeasurement)]
    public async Task ArchiveCommitsAfterTheCheck_WriteIsRefused_NothingWritten(Write write)
    {
        var data = CommittedTestData.Create(output);
        try
        {
            var (author, measurer, clientId) = await SeedAsync(data);
            var target = await PrepareAsync(data, write, author, measurer, clientId);
            var before = await SnapshotAsync(clientId);

            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<Outcome>? taskA = null;
            await using var scopeA = data.NewScope();
            try
            {
                // Poste A : client lu ACTIF (contrôle passé), arrêté avant son enregistrement.
                var clients = new PausedClientStore(scopeA.ServiceProvider.GetRequiredService<IClientStore>(), reached, release.Task);
                taskA = RunAsync(scopeA, write, target, author, measurer, clientId, clients);
                Assert.Same(reached.Task, await Task.WhenAny(reached.Task, taskA).WaitAsync(Timeout));

                // Archivage VALIDÉ (COMMIT) entre le contrôle de A et son enregistrement.
                await using (var scopeB = data.NewScope())
                {
                    Assert.Equal(ClientWriteStatus.Success, await ArchiveAsync(scopeB, clientId, author));
                }

                release.SetResult();
                var outcome = await taskA.WaitAsync(Timeout);
                output.WriteLine($"{write} : archivage validé après le contrôle de A → A = {outcome.Status}");
                Assert.Equal(OpticalWriteStatus.ClientArchived, outcome.Status);
            }
            finally
            {
                release.TrySetResult();
                await SettleAsync(taskA);
            }

            // Rien n'est écrit : mêmes lignes, mêmes versions (xmin), mêmes événements ; aucune écriture postérieure à l'archivage.
            Assert.Equal(before, await SnapshotAsync(clientId));
            Assert.Equal(0, await LateWritesAsync(clientId));
            await AssertArchivedClientStillReadableAndInvalidableAsync(data, clientId, author, target?.Id, write);
        }
        finally
        {
            await CleanupAsync(data);
        }
    }

    [OpticalPostgreSqlTheory]
    [InlineData(Write.Create)]
    [InlineData(Write.EditDraft)]
    [InlineData(Write.ConfirmDraft)]
    [InlineData(Write.StartRectification)]
    [InlineData(Write.DiscardRectification)]
    [InlineData(Write.CreateMeasurement)]
    public async Task WriteHoldsTheClientLock_ArchiveWaits_ThenSucceeds(Write write)
    {
        var data = CommittedTestData.Create(output);
        try
        {
            var (author, measurer, clientId) = await SeedAsync(data);
            var target = await PrepareAsync(data, write, author, measurer, clientId);
            var events = await OpticalEventsAsync(clientId);
            Outcome written;
            DateTime beforeCommit;

            Task<ClientWriteStatus>? archive = null;
            await using var scopeA = data.NewScope();
            await using var scopeB = data.NewScope();
            var contextA = scopeA.ServiceProvider.GetRequiredService<MmvDbContext>();
            var pidA = await BackendPidAsync(contextA);
            var pidB = await BackendPidAsync(scopeB.ServiceProvider.GetRequiredService<MmvDbContext>());
            await using (var transaction = await contextA.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                try
                {
                    // Poste A : écriture faite (verrous pris), transaction OUVERTE.
                    written = await RunAsync(scopeA, write, target, author, measurer, clientId);
                    Assert.Equal(OpticalWriteStatus.Success, written.Status);

                    // Archivage demandé pendant ce temps : il doit attendre la fin de la transaction de A.
                    archive = ArchiveAsync(scopeB, clientId, author);
                    var wait = await RowLockObservation.WaitUntilBlockedAsync(pidB, pidA, output);
                    Assert.True(wait.Blocked, $"L'archivage n'attend pas l'écriture optique en cours : {wait}");
                    Assert.False(archive.IsCompleted);
                    Assert.Null(await ArchivedAtAsync(clientId));

                    beforeCommit = DateTime.UtcNow;
                    await transaction.CommitAsync();
                }
                finally
                {
                    await SettleAsync(archive);
                }
            }

            Assert.Equal(ClientWriteStatus.Success, await archive.WaitAsync(Timeout));
            // Date d'archivage prise APRÈS la validation de A (verrou du client avant lecture) : aucune inversion d'horodatage.
            var archivedAt = await ArchivedAtAsync(clientId);
            Assert.True(archivedAt > beforeCommit, $"archived_at_utc {archivedAt:O} antérieure à la validation de l'écriture ({beforeCommit:O})");
            output.WriteLine($"{write} : écriture validée d'abord, archivage ensuite (attente constatée, archivé à {archivedAt:O} > {beforeCommit:O})");

            // L'écriture de A est entière, sans doublon, et antérieure à l'archivage ; ses événements sont tous présents.
            Assert.Equal(events + ExpectedEvents(write), await OpticalEventsAsync(clientId));
            Assert.Equal(0, await LateWritesAsync(clientId));
            Assert.Equal(1, await CommittedTestData.CountAsync(
                "SELECT count(*) FROM (SELECT id FROM optical.prescriptions WHERE client_id = @c UNION ALL SELECT id FROM optical.measurements WHERE client_id = @c) r", ("c", clientId)));

            // Ensuite : toute nouvelle écriture est refusée, la consultation et l'invalidation motivée restent permises.
            await using (var scope = data.NewScope())
            {
                var after = await scope.ServiceProvider.GetRequiredService<PrescriptionService>().CreateAsync(
                    clientId, OpticalPostgreSqlTests.Prescription(), confirm: true, Guid.CreateVersion7(), author, CancellationToken.None);
                Assert.Equal(OpticalWriteStatus.ClientArchived, after.Status);
            }

            await AssertArchivedClientStillReadableAndInvalidableAsync(data, clientId, author, written.Id, write);
        }
        finally
        {
            await CleanupAsync(data);
        }
    }

    [OpticalPostgreSqlTheory]
    [InlineData(Loss.Deactivation)]
    [InlineData(Loss.PermissionRevoked)]
    public async Task MeasurerLostAfterTheCheck_NewMeasurementIsRefused_NothingWritten(Loss loss)
    {
        var data = CommittedTestData.Create(output);
        try
        {
            var (author, measurer, clientId) = await SeedAsync(data);
            var before = await SnapshotAsync(clientId);

            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<Outcome>? taskA = null;
            await using var scopeA = data.NewScope();
            try
            {
                // Poste A : collaborateur lu ÉLIGIBLE (contrôle passé), arrêté avant son enregistrement.
                var directory = new PausedDirectory(scopeA.ServiceProvider.GetRequiredService<IOpticalDirectory>(), reached, release.Task);
                taskA = RunAsync(scopeA, Write.CreateMeasurement, null, author, measurer, clientId, directory: directory);
                Assert.Same(reached.Task, await Task.WhenAny(reached.Task, taskA).WaitAsync(Timeout));

                // Désactivation ou retrait du droit VALIDÉ entre le contrôle de A et son enregistrement.
                Assert.Equal(1, await CommittedTestData.ExecuteAsync(LossSql(loss), ("u", measurer), ("p", Permissions.OpticalMeasurementsWrite)));

                release.SetResult();
                var outcome = await taskA.WaitAsync(Timeout);
                output.WriteLine($"{loss} validée après le contrôle de A → A = {outcome.Status} {string.Join(",", outcome.Errors?.SelectMany(e => e.Value.Select(v => $"{e.Key}={v}")) ?? [])}");
                Assert.Equal(OpticalWriteStatus.ValidationFailed, outcome.Status);
                Assert.Equal([OpticalFieldErrors.NotEligible], outcome.Errors!["provenance.measuredByUserId"]);
            }
            finally
            {
                release.TrySetResult();
                await SettleAsync(taskA);
            }

            Assert.Equal(before, await SnapshotAsync(clientId));
            Assert.Equal(0, await CommittedTestData.CountAsync("SELECT count(*) FROM optical.measurement_revisions WHERE measured_by_user_id = @u", ("u", measurer)));
        }
        finally
        {
            await CleanupAsync(data);
        }
    }

    [OpticalPostgreSqlTheory]
    [InlineData(Loss.Deactivation)]
    [InlineData(Loss.PermissionRevoked)]
    public async Task MeasurementHoldsTheMeasurerLock_LossWaits_HistoryKept_NewMeasurementsRefused(Loss loss)
    {
        var data = CommittedTestData.Create(output);
        try
        {
            var (author, measurer, clientId) = await SeedAsync(data);
            Outcome written;

            Task<int>? lost = null;
            await using var scopeA = data.NewScope();
            await using var side = await SideConnection.OpenAsync();
            var contextA = scopeA.ServiceProvider.GetRequiredService<MmvDbContext>();
            var pidA = await BackendPidAsync(contextA);
            await using (var transaction = await contextA.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                try
                {
                    // Poste A : mesure confirmée (collaborateur verrouillé), transaction OUVERTE.
                    written = await RunAsync(scopeA, Write.CreateMeasurement, null, author, measurer, clientId);
                    Assert.Equal(OpticalWriteStatus.Success, written.Status);

                    lost = side.ExecuteAsync(LossSql(loss), ("u", measurer), ("p", Permissions.OpticalMeasurementsWrite));
                    var wait = await RowLockObservation.WaitUntilBlockedAsync(side.Pid, pidA, output);
                    Assert.True(wait.Blocked, $"{loss} n'attend pas la confirmation de mesure en cours : {wait}");
                    Assert.False(lost.IsCompleted);

                    await transaction.CommitAsync();
                }
                finally
                {
                    await SettleAsync(lost);
                }
            }

            Assert.Equal(1, await lost.WaitAsync(Timeout));
            output.WriteLine($"{loss} : mesure validée d'abord, perte d'éligibilité ensuite (attente constatée)");
            var id = written.Id!.Value;
            Assert.Equal(1, await CommittedTestData.CountAsync(
                "SELECT count(*) FROM optical.measurement_revisions WHERE measurement_id = @i AND status = 'confirmed' AND revision_no = 1 AND measured_by_user_id = @u",
                ("i", id), ("u", measurer)));

            await using var scope = data.NewScope();
            var service = scope.ServiceProvider.GetRequiredService<MeasurementService>();

            // Nouvelle mesure avec ce collaborateur : refusée, rien n'est écrit.
            var refused = await service.CreateAsync(clientId, Measure(measurer, "+0,75"), confirm: true, Guid.CreateVersion7(), author, CancellationToken.None);
            Assert.Equal(OpticalWriteStatus.ValidationFailed, refused.Status);
            Assert.Equal([OpticalFieldErrors.NotEligible], refused.Errors!["provenance.measuredByUserId"]);
            Assert.Equal(1, await CommittedTestData.CountAsync("SELECT count(*) FROM optical.measurements WHERE client_id = @c", ("c", clientId)));

            // Nom du collaborateur historique toujours servi (citedUsers) ; mesure confirmée intacte.
            var names = await scope.ServiceProvider.GetRequiredService<IOpticalReadStore>().DisplayNamesAsync([measurer], CancellationToken.None);
            Assert.Equal("B3 mesureur", names[measurer]);

            // Rectification conservant le collaborateur historique : admise (désignation passée, règle approuvée).
            var record = (await scope.ServiceProvider.GetRequiredService<OpticalQueryService>().GetMeasurementAsync(id, CancellationToken.None))!;
            var started = await service.StartRectificationAsync(id, 1, "transcription_error", null, record.Version, Guid.CreateVersion7(), author, CancellationToken.None);
            Assert.Equal(OpticalWriteStatus.Success, started.Status);
            var rectified = await service.SaveDraftAsync(id, Measure(measurer, "+1,00"), confirm: true, started.Record!.Version, Guid.CreateVersion7(), author, CancellationToken.None);
            Assert.Equal(OpticalWriteStatus.Success, rectified.Status);
            Assert.Equal("+0,25|+1,00", await ScalarAsync<string>(
                "SELECT string_agg(od_sphere, '|' ORDER BY revision_no) FROM optical.measurement_revisions WHERE measurement_id = @i AND status = 'confirmed' AND measured_by_user_id = @u",
                ("i", id), ("u", measurer)));
        }
        finally
        {
            await CleanupAsync(data);
        }
    }

    private static string LossSql(Loss loss) => loss == Loss.Deactivation
        ? "UPDATE auth.users SET is_active = false WHERE id = @u AND is_active AND @p <> ''"
        : "DELETE FROM auth.user_permissions WHERE user_id = @u AND permission = @p";

    private sealed record Target(Guid Id, uint Version);

    private sealed record Outcome(OpticalWriteStatus Status, Guid? Id, IReadOnlyDictionary<string, string[]>? Errors);

    private static PrescriptionInput Rx(string sphere) => OpticalPostgreSqlTests.Prescription(sphere);

    private static MeasurementInput Measure(Guid measurer, string sphere) =>
        new(new MeasurementProvenanceInput("2026-10-29", measurer, null), new EyeInput(Sphere: sphere), null, null);

    private static async Task<(Guid Author, Guid Measurer, Guid ClientId)> SeedAsync(CommittedTestData data)
    {
        var author = await data.AddUserAsync("auteur", eligible: false, null, Permissions.OpticalPrescriptionsWrite, Permissions.OpticalMeasurementsWrite);
        var measurer = await data.AddUserAsync("mesureur", eligible: false, null, Permissions.OpticalMeasurementsWrite);
        var clientId = await data.AddClientAsync(author, "Course");
        return (author, measurer, clientId);
    }

    /// <summary>État validé préalable à l'écriture étudiée (brouillon, révision confirmée, rectification ouverte).</summary>
    private static async Task<Target?> PrepareAsync(CommittedTestData data, Write write, Guid author, Guid measurer, Guid clientId)
    {
        if (write is Write.Create or Write.CreateMeasurement)
        {
            return null;
        }

        await using var scope = data.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<PrescriptionService>();
        var created = await service.CreateAsync(clientId, Rx("-1,25"), confirm: write is not (Write.EditDraft or Write.ConfirmDraft), Guid.CreateVersion7(), author, CancellationToken.None);
        Assert.Equal(OpticalWriteStatus.Success, created.Status);
        if (write != Write.DiscardRectification)
        {
            return new(created.Record!.Id, created.Record.Version);
        }

        var started = await service.StartRectificationAsync(created.Record!.Id, 1, "transcription_error", null, created.Record.Version, Guid.CreateVersion7(), author, CancellationToken.None);
        Assert.Equal(OpticalWriteStatus.Success, started.Status);
        return new(started.Record!.Id, started.Record.Version);
    }

    /// <summary>Écriture étudiée, par les services réels du scope (dépendances éventuellement suspendues).</summary>
    private static async Task<Outcome> RunAsync(
        AsyncServiceScope scope, Write write, Target? target, Guid author, Guid measurer, Guid clientId, IClientStore? clients = null, IOpticalDirectory? directory = null)
    {
        var services = scope.ServiceProvider;
        clients ??= services.GetRequiredService<IClientStore>();
        var audit = services.GetRequiredService<IAuditRecorder>();
        var requestId = Guid.CreateVersion7();
        if (write == Write.CreateMeasurement)
        {
            var measurements = new MeasurementService(
                services.GetRequiredService<IOpticalRecordStore<Measurement>>(), clients, directory ?? services.GetRequiredService<IOpticalDirectory>(), TimeProvider.System, audit);
            var measured = await measurements.CreateAsync(clientId, Measure(measurer, "+0,25"), confirm: true, requestId, author, CancellationToken.None);
            return new(measured.Status, measured.Record?.Id, measured.Errors);
        }

        var service = new PrescriptionService(services.GetRequiredService<IOpticalRecordStore<Prescription>>(), clients, TimeProvider.System, audit);
        var result = write switch
        {
            Write.Create => await service.CreateAsync(clientId, Rx("-2,00"), confirm: true, requestId, author, CancellationToken.None),
            Write.EditDraft => await service.SaveDraftAsync(target!.Id, Rx("-2,00"), confirm: false, target.Version, requestId, author, CancellationToken.None),
            Write.ConfirmDraft => await service.SaveDraftAsync(target!.Id, null, confirm: true, target.Version, requestId, author, CancellationToken.None),
            Write.StartRectification => await service.StartRectificationAsync(target!.Id, 1, "transcription_error", null, target.Version, requestId, author, CancellationToken.None),
            _ => await service.DiscardRectificationAsync(target!.Id, target.Version, requestId, author, CancellationToken.None),
        };
        return new(result.Status, result.Record?.Id ?? target?.Id, result.Errors);
    }

    private static async Task<ClientWriteStatus> ArchiveAsync(AsyncServiceScope scope, Guid clientId, Guid author)
    {
        var client = (await scope.ServiceProvider.GetRequiredService<IClientStore>().GetAsync(clientId, CancellationToken.None))!;
        return (await scope.ServiceProvider.GetRequiredService<ClientService>().ArchiveAsync(clientId, client.Version, author, CancellationToken.None)).Status;
    }

    /// <summary>Client archivé : dossier toujours consultable ; invalidation motivée permise (D-04-07).</summary>
    private async Task AssertArchivedClientStillReadableAndInvalidableAsync(CommittedTestData data, Guid clientId, Guid author, Guid? recordId, Write write)
    {
        Assert.NotNull(await ArchivedAtAsync(clientId));
        await using var scope = data.NewScope();
        var listed = await scope.ServiceProvider.GetRequiredService<IOpticalReadStore>().ListForClientAsync(clientId, includeInvalidated: true, CancellationToken.None);
        Assert.Equal(recordId is null ? 0 : 1, listed.Count);
        if (recordId is not { } id)
        {
            return;
        }

        var queries = scope.ServiceProvider.GetRequiredService<OpticalQueryService>();
        OpticalWriteStatus invalidated;
        if (write == Write.CreateMeasurement)
        {
            var record = (await queries.GetMeasurementAsync(id, CancellationToken.None))!;
            invalidated = (await scope.ServiceProvider.GetRequiredService<MeasurementService>().InvalidateAsync(
                id, "duplicate", null, record.Version, Guid.CreateVersion7(), author, CancellationToken.None)).Status;
        }
        else
        {
            var record = (await queries.GetPrescriptionAsync(id, CancellationToken.None))!;
            invalidated = (await scope.ServiceProvider.GetRequiredService<PrescriptionService>().InvalidateAsync(
                id, "duplicate", null, record.Version, Guid.CreateVersion7(), author, CancellationToken.None)).Status;
        }

        output.WriteLine($"client archivé : {listed.Count} enregistrement(s) consultable(s), invalidation = {invalidated}");
        Assert.Equal(OpticalWriteStatus.Success, invalidated);
    }

    /// <summary>Lignes, statuts et versions (xmin) des enregistrements optiques du client, et nombre d'événements optiques.</summary>
    private static Task<string> SnapshotAsync(Guid clientId) => ScalarAsync<string>(
        """
        SELECT concat_ws(' | ',
          (SELECT coalesce(string_agg(p.id || '@' || p.xmin::text, ',' ORDER BY p.id), '') FROM optical.prescriptions p WHERE p.client_id = @c),
          (SELECT coalesce(string_agg(r.id || ':' || r.status || ':' || coalesce(r.revision_no::text, '-') || '@' || r.xmin::text, ',' ORDER BY r.id), '')
             FROM optical.prescription_revisions r JOIN optical.prescriptions p ON p.id = r.prescription_id WHERE p.client_id = @c),
          (SELECT coalesce(string_agg(m.id || '@' || m.xmin::text, ',' ORDER BY m.id), '') FROM optical.measurements m WHERE m.client_id = @c),
          (SELECT coalesce(string_agg(r.id || ':' || r.status || ':' || coalesce(r.revision_no::text, '-') || '@' || r.xmin::text, ',' ORDER BY r.id), '')
             FROM optical.measurement_revisions r JOIN optical.measurements m ON m.id = r.measurement_id WHERE m.client_id = @c),
          (SELECT count(*) FROM audit.events WHERE client_id = @c AND module = 'optical'))
        """,
        ("c", clientId));

    private static Task<long> OpticalEventsAsync(Guid clientId) =>
        CommittedTestData.CountAsync("SELECT count(*) FROM audit.events WHERE client_id = @c AND module = @m", ("c", clientId), ("m", AuditActions.OpticalModule));

    /// <summary>En-têtes, révisions ou événements optiques datés APRÈS l'archivage du client (attendu : 0).</summary>
    private static Task<long> LateWritesAsync(Guid clientId) => CommittedTestData.CountAsync(
        """
        SELECT (SELECT count(*) FROM optical.prescriptions p WHERE p.client_id = c.id AND p.updated_at_utc > c.archived_at_utc)
             + (SELECT count(*) FROM optical.prescription_revisions r JOIN optical.prescriptions p ON p.id = r.prescription_id WHERE p.client_id = c.id AND r.updated_at_utc > c.archived_at_utc)
             + (SELECT count(*) FROM optical.measurements m WHERE m.client_id = c.id AND m.updated_at_utc > c.archived_at_utc)
             + (SELECT count(*) FROM optical.measurement_revisions r JOIN optical.measurements m ON m.id = r.measurement_id WHERE m.client_id = c.id AND r.updated_at_utc > c.archived_at_utc)
             + (SELECT count(*) FROM audit.events e WHERE e.client_id = c.id AND e.module = 'optical' AND e.action NOT LIKE '%.invalidated' AND e.occurred_at_utc > c.archived_at_utc)
        FROM clients.clients c WHERE c.id = @c
        """,
        ("c", clientId));

    private static async Task<DateTime?> ArchivedAtAsync(Guid clientId)
    {
        await using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT archived_at_utc FROM clients.clients WHERE id = @c", connection);
        command.Parameters.AddWithValue("c", clientId);
        return await command.ExecuteScalarAsync() is DateTime value ? value : null;
    }

    private static async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> BackendPidAsync(MmvDbContext context)
    {
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

    private async Task CleanupAsync(CommittedTestData data)
    {
        var purged = await OpticalCleanup.PurgeAsync(data.Users, data.Clients);
        output.WriteLine($"nettoyage optique : {purged} ligne(s)");
        await data.DisposeAsync();
        Assert.Equal(0, data.Residue);
        Assert.Equal(0, await OpticalCleanup.CountAsync(data.Users, data.Clients));
    }

    /// <summary>Lecture réelle du client, puis arrêt jusqu'au signal : le contrôle de l'écriture optique est déjà passé.</summary>
    private sealed class PausedClientStore(IClientStore inner, TaskCompletionSource reached, Task release) : IClientStore
    {
        public Task<ClientPage> ListAsync(ClientListQuery query, CancellationToken cancellationToken) => inner.ListAsync(query, cancellationToken);

        public async Task<Client?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var client = await inner.GetAsync(id, cancellationToken);
            reached.TrySetResult();
            await release;
            return client;
        }

        public Task<Client?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) => inner.FindForUpdateAsync(id, cancellationToken);

        public Task<IClientLifecycleLock> LockForLifecycleAsync(Guid id, CancellationToken cancellationToken) => inner.LockForLifecycleAsync(id, cancellationToken);

        public void Add(Client client) => inner.Add(client);

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => inner.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Contrôle réel de l'éligibilité du collaborateur, puis arrêt jusqu'au signal.</summary>
    private sealed class PausedDirectory(IOpticalDirectory inner, TaskCompletionSource reached, Task release) : IOpticalDirectory
    {
        public async Task<bool> IsEligibleMeasurerAsync(Guid userId, CancellationToken cancellationToken)
        {
            var eligible = await inner.IsEligibleMeasurerAsync(userId, cancellationToken);
            reached.TrySetResult();
            await release;
            return eligible;
        }

        public Task<IReadOnlyList<OpticalActorView>> ListMeasurersAsync(CancellationToken cancellationToken) => inner.ListMeasurersAsync(cancellationToken);
    }

    /// <summary>Connexion d'écriture directe sur mmv_v2_test (cible relue), processus serveur connu avant l'instruction.</summary>
    private sealed class SideConnection : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;

        private SideConnection(NpgsqlConnection connection, int pid)
        {
            _connection = connection;
            Pid = pid;
        }

        public int Pid { get; }

        public static async Task<SideConnection> OpenAsync()
        {
            var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString);
            await connection.OpenAsync();
            await using var identity = new NpgsqlCommand(
                "SELECT CASE WHEN current_database() = 'mmv_v2_test' AND current_user = 'mmv_v2_test' AND inet_server_port() = 5433 THEN pg_backend_pid() ELSE -1 END", connection);
            var pid = (int)(await identity.ExecuteScalarAsync())!;
            Assert.True(pid > 0, "Cible d'écriture refusée (mmv_v2_test/5433 attendu).");
            return new SideConnection(connection, pid);
        }

        public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            await using var command = new NpgsqlCommand(sql, _connection);
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            return await command.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}

/// <summary>
/// Attente d'un processus serveur sur un VERROU DE LIGNE (<c>wait_event_type = 'Lock'</c> : transaction ou tuple)
/// détenu par un autre processus, constatée par une connexion d'observation en lecture seule (<c>pg_blocking_pids</c>).
/// </summary>
internal static class RowLockObservation
{
    public sealed record Wait(bool Blocked, string? WaitEventType, string? WaitEvent, int[] BlockingPids)
    {
        public override string ToString() => $"wait_event_type={WaitEventType ?? "∅"} wait_event={WaitEvent ?? "∅"} blocking=[{string.Join(",", BlockingPids)}]";
    }

    public static async Task<Wait> WaitUntilBlockedAsync(int waiterPid, int holderPid, ITestOutputHelper output, int timeoutSeconds = 10)
    {
        await using var observer = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
        await observer.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        Wait last;
        do
        {
            await using var command = new NpgsqlCommand("SELECT a.wait_event_type, a.wait_event, pg_blocking_pids(a.pid) FROM pg_stat_activity a WHERE a.pid = @pid", observer);
            command.Parameters.AddWithValue("pid", waiterPid);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    return new Wait(false, null, null, []);
                }

                var type = reader.IsDBNull(0) ? null : reader.GetString(0);
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                var blocking = reader.GetFieldValue<int[]>(2);
                last = new Wait(type == "Lock" && blocking.Contains(holderPid), type, name, blocking);
            }

            if (last.Blocked)
            {
                output.WriteLine($"processus {waiterPid} en attente du processus {holderPid} : {last}");
                return last;
            }

            await Task.Delay(50);
        }
        while (DateTime.UtcNow < deadline);

        output.WriteLine($"processus {waiterPid} non bloqué après {timeoutSeconds} s : {last}");
        return last;
    }
}
