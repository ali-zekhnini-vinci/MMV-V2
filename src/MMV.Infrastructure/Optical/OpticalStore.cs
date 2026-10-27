using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MMV.Application.Optical;
using MMV.Domain.Audit;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Domain.Optical;
using MMV.Infrastructure.Persistence;
using Npgsql;

namespace MMV.Infrastructure.Optical;

/// <summary>
/// Persistance d'un type d'enregistrement : l'agrégat (en-tête + révisions) et les événements d'audit ajoutés au contexte
/// PARTAGÉ de la requête sont écrits par un seul <c>SaveChanges</c>, donc dans une seule transaction PostgreSQL.
/// </summary>
internal sealed class OpticalRecordStore<TRecord, TRevision, TContent>(MmvDbContext context) : IOpticalRecordStore<TRecord>
    where TRecord : OpticalRecord<TRevision, TContent>
    where TRevision : OpticalRevision<TContent>
    where TContent : OpticalContent
{
    public Task<TRecord?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<TRecord>().Include(r => r.Revisions).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<TRecord?> FindByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.Set<TRecord>().AsNoTracking().Include(r => r.Revisions).SingleOrDefaultAsync(r => r.CreationRequestId == requestId, cancellationToken);

    public void Add(TRecord record) => context.Set<TRecord>().Add(record);

    public async Task<OpticalCommitResult> SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Transaction propre (sauf transaction déjà ouverte par l'appelant) : en-tête verrouillé et version revérifiée AVANT
        // toute écriture, pour qu'un second poste soit refusé proprement (409) au lieu d'atteindre une révision déjà figée.
        var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        try
        {
            if (!await LockModifiedRecordsAsync(cancellationToken))
            {
                context.ChangeTracker.Clear();
                return OpticalCommitResult.VersionConflict;
            }

            // UPDATE … WHERE id = @id AND xmin = @version_lue : aucune ligne touchée → conflit, rien n'est écrit.
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return OpticalCommitResult.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return OpticalCommitResult.VersionConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique)
        {
            // Unicité de la requête de création (réémission concurrente) ; brouillon unique ou numéro de révision déjà pris
            // (écriture concurrente) : transaction annulée, rien n'est écrit.
            context.ChangeTracker.Clear();
            return unique.ConstraintName?.EndsWith("_creation_request_id", StringComparison.Ordinal) == true
                ? OpticalCommitResult.DuplicateCreationRequest
                : OpticalCommitResult.VersionConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.InsufficientPrivilege } frozen
            && frozen.MessageText.StartsWith("optical", StringComparison.Ordinal))
        {
            // Écriture concurrente : l'autre poste a confirmé ou invalidé entre la lecture et l'enregistrement, la révision
            // (ou l'enregistrement) est désormais figée par déclencheur — même issue qu'un conflit de version, rien n'est écrit.
            context.ChangeTracker.Clear();
            return OpticalCommitResult.VersionConflict;
        }
        finally
        {
            // Non validée (refus, erreur) : annulée à la libération, verrous relâchés.
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// <c>SELECT xmin … FOR UPDATE</c> sur chaque en-tête modifié : attend un poste concurrent jusqu'à sa validation, puis
    /// compare la version lue par ce poste ; différente → conflit, aucune écriture tentée.
    /// </summary>
    private async Task<bool> LockModifiedRecordsAsync(CancellationToken cancellationToken)
    {
        var entity = context.Model.FindEntityType(typeof(TRecord))!;
        // Nom de table issu du modèle EF (jamais d'une saisie) ; identifiant passé en paramètre.
        var sql = "SELECT xmin::text::bigint AS \"Value\" FROM " + entity.GetSchema() + "." + entity.GetTableName() + " WHERE id = @p0 FOR UPDATE";
        foreach (var entry in context.ChangeTracker.Entries<TRecord>().Where(e => e.State == EntityState.Modified).ToList())
        {
            var current = await context.Database
                .SqlQueryRaw<long>(sql, new NpgsqlParameter("p0", entry.Entity.Id))
                .ToListAsync(cancellationToken);
            if (current is not [var version] || version != entry.Property(r => r.Version).OriginalValue)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Comptes cités par le dossier optique.</summary>
internal sealed class OpticalDirectory(MmvDbContext context) : IOpticalDirectory
{
    public Task<bool> IsEligibleMeasurerAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().AnyAsync(
            u => u.Id == userId && u.IsActive && u.Permissions.Any(p => p.Permission == Permissions.OpticalMeasurementsWrite), cancellationToken);

    public async Task<IReadOnlyList<OpticalActorView>> ListMeasurersAsync(CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Permissions.Any(p => p.Permission == Permissions.OpticalMeasurementsWrite))
            .OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Select(u => new OpticalActorView(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
}

/// <summary>
/// Lecture du dossier optique. Listes : projection LÉGÈRE des en-têtes et révisions non abandonnées (sans valeurs), statut
/// et révision affichée dérivés, filtre / recherche / tri / pagination, puis lecture des valeurs OD/OG de la seule page.
/// Recherche : mots repliés (sans accents, minuscules) dans le nom et la référence du client, la référence de
/// l'enregistrement et la source (prescripteur ou collaborateur ayant mesuré). Volume à mesurer en pilote (O-04-41).
/// </summary>
internal sealed class OpticalReadStore(MmvDbContext context) : IOpticalReadStore
{
    private static readonly StringComparer NameOrder = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), CompareOptions.IgnoreCase);

    private sealed record Row(
        OpticalKind Kind, Guid Id, string Reference, Guid ClientId, bool Invalidated, Guid RevisionId, OpticalDisplayStatus Status, int? RevisionNo,
        DateOnly? Date, string? Source, DateTimeOffset RecordedAtUtc);

    private sealed record RevisionRow(Guid Id, Guid RecordId, OpticalRevisionStatus Status, int? RevisionNo, DateOnly? Date, string? SourceName, Guid? SourceUserId, DateTimeOffset StartedAtUtc);

    private sealed record HeaderRow(Guid Id, string Reference, Guid ClientId, bool Invalidated);

    public async Task<OpticalSummaryPage> ListAsync(OpticalListQuery query, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(query.Kind, null, cancellationToken);
        var clients = await ClientsAsync(rows.Select(r => r.ClientId), cancellationToken);
        IEnumerable<Row> filtered = rows.Where(r => OpticalPresentation.Matches(r.Status, query.Status, query.IncludeInvalidated));

        var words = ClientSearch.Fold(query.Search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= OpticalListQuery.SearchWordMinLength).Distinct(StringComparer.Ordinal).ToList();
        if (words.Count > 0)
        {
            filtered = filtered.Where(r =>
            {
                var client = clients[r.ClientId];
                var haystack = ClientSearch.Fold($"{client.DisplayName} {client.Reference} {r.Source} {r.Reference}");
                return words.All(w => haystack.Contains(w, StringComparison.Ordinal));
            });
        }

        var list = filtered.ToList();
        var ordered = Order(list, clients, query.Sort, query.Descending).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new OpticalSummaryPage(await SummariesAsync(ordered, clients, cancellationToken), query.Page, query.PageSize, list.Count);
    }

    public async Task<IReadOnlyList<OpticalSummaryView>> ListForClientAsync(Guid clientId, bool includeInvalidated, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(null, clientId, cancellationToken);
        var clients = await ClientsAsync([clientId], cancellationToken);
        var ordered = rows.Where(r => includeInvalidated || r.Status != OpticalDisplayStatus.Invalidated)
            .OrderByDescending(r => r.RecordedAtUtc).ThenByDescending(r => r.Id).ToList();
        return await SummariesAsync(ordered, clients, cancellationToken);
    }

    public Task<Prescription?> GetPrescriptionAsync(Guid id, CancellationToken cancellationToken) =>
        context.Prescriptions.AsNoTracking().Include(r => r.Revisions).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Measurement?> GetMeasurementAsync(Guid id, CancellationToken cancellationToken) =>
        context.Measurements.AsNoTracking().Include(r => r.Revisions).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<OpticalClientView?> GetClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        (await ClientsAsync([clientId], cancellationToken)).GetValueOrDefault(clientId);

    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

    public async Task<IReadOnlyList<AuditEvent>> EventsAsync(string entityType, Guid entityId, CancellationToken cancellationToken) =>
        await context.AuditEvents.AsNoTracking()
            .Where(e => e.Module == AuditActions.OpticalModule && e.EntityType == entityType && e.EntityId == entityId)
            .OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

    private async Task<List<Row>> RowsAsync(OpticalKind? kind, Guid? clientId, CancellationToken cancellationToken)
    {
        var rows = new List<Row>();
        if (kind is null or OpticalKind.Prescription)
        {
            var headers = context.Prescriptions.AsNoTracking().Where(p => clientId == null || p.ClientId == clientId);
            var revisions = context.Set<PrescriptionRevision>().AsNoTracking()
                .Where(r => r.Status != OpticalRevisionStatus.Discarded && headers.Any(p => p.Id == r.RecordId))
                .Select(r => new RevisionRow(r.Id, r.RecordId, r.Status, r.RevisionNo, r.PrescribedOn, r.PrescriberName, null, r.StartedAtUtc));
            rows.AddRange(Combine(
                OpticalKind.Prescription,
                await headers.Select(p => new HeaderRow(p.Id, p.Reference, p.ClientId, p.InvalidatedAtUtc != null)).ToListAsync(cancellationToken),
                await revisions.ToListAsync(cancellationToken),
                new Dictionary<Guid, string>()));
        }

        if (kind is null or OpticalKind.Measurement)
        {
            var headers = context.Measurements.AsNoTracking().Where(m => clientId == null || m.ClientId == clientId);
            var revisions = await context.Set<MeasurementRevision>().AsNoTracking()
                .Where(r => r.Status != OpticalRevisionStatus.Discarded && headers.Any(m => m.Id == r.RecordId))
                .Select(r => new RevisionRow(r.Id, r.RecordId, r.Status, r.RevisionNo, r.MeasuredOn, null, r.MeasuredByUserId, r.StartedAtUtc))
                .ToListAsync(cancellationToken);
            var measurers = await DisplayNamesAsync([.. revisions.Where(r => r.SourceUserId is not null).Select(r => r.SourceUserId!.Value).Distinct()], cancellationToken);
            rows.AddRange(Combine(
                OpticalKind.Measurement,
                await headers.Select(m => new HeaderRow(m.Id, m.Reference, m.ClientId, m.InvalidatedAtUtc != null)).ToListAsync(cancellationToken),
                revisions,
                measurers));
        }

        return rows;
    }

    /// <summary>Révision affichée : dernière confirmée, sinon brouillon de première saisie ; statut dérivé.</summary>
    private static IEnumerable<Row> Combine(OpticalKind kind, List<HeaderRow> headers, List<RevisionRow> revisions, IReadOnlyDictionary<Guid, string> names)
    {
        var byRecord = revisions.ToLookup(r => r.RecordId);
        foreach (var header in headers)
        {
            var confirmed = byRecord[header.Id].Where(r => r.Status == OpticalRevisionStatus.Confirmed).MaxBy(r => r.RevisionNo);
            var draft = byRecord[header.Id].FirstOrDefault(r => r.Status == OpticalRevisionStatus.Draft);
            if ((confirmed ?? draft) is not { } shown)
            {
                continue;
            }

            var source = shown.SourceName ?? (shown.SourceUserId is { } user ? names.GetValueOrDefault(user) : null);
            yield return new Row(
                kind, header.Id, header.Reference, header.ClientId, header.Invalidated, shown.Id,
                OpticalPresentation.Status(header.Invalidated, confirmed is not null, draft is not null),
                confirmed?.RevisionNo, shown.Date, source, shown.StartedAtUtc);
        }
    }

    private static IEnumerable<Row> Order(List<Row> rows, Dictionary<Guid, OpticalClientView> clients, OpticalSortField sort, bool descending) =>
        sort switch
        {
            OpticalSortField.Client => descending
                ? rows.OrderByDescending(r => clients[r.ClientId].DisplayName, NameOrder).ThenByDescending(r => r.RecordedAtUtc).ThenByDescending(r => r.Id)
                : rows.OrderBy(r => clients[r.ClientId].DisplayName, NameOrder).ThenByDescending(r => r.RecordedAtUtc).ThenByDescending(r => r.Id),
            OpticalSortField.Recorded => descending
                ? rows.OrderByDescending(r => r.RecordedAtUtc).ThenByDescending(r => r.Id)
                : rows.OrderBy(r => r.RecordedAtUtc).ThenBy(r => r.Id),
            // Dates absentes (brouillon) toujours en fin de liste, quel que soit le sens.
            _ => descending
                ? rows.OrderBy(r => r.Date is null).ThenByDescending(r => r.Date).ThenByDescending(r => r.RecordedAtUtc).ThenByDescending(r => r.Id)
                : rows.OrderBy(r => r.Date is null).ThenBy(r => r.Date).ThenByDescending(r => r.RecordedAtUtc).ThenByDescending(r => r.Id),
        };

    private async Task<Dictionary<Guid, OpticalClientView>> ClientsAsync(IEnumerable<Guid> clientIds, CancellationToken cancellationToken)
    {
        var ids = clientIds.Distinct().ToList();
        return ids.Count == 0
            ? []
            : await context.Clients.AsNoTracking().Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Reference, c.FirstName, c.LastName, Archived = c.ArchivedAtUtc != null })
                .ToDictionaryAsync(
                    c => c.Id, c => new OpticalClientView(c.Id, c.Reference, OpticalPresentation.ClientDisplayName(c.FirstName, c.LastName), c.Archived), cancellationToken);
    }

    /// <summary>Valeurs OD/OG des révisions affichées de la page seulement.</summary>
    private async Task<List<OpticalSummaryView>> SummariesAsync(List<Row> rows, Dictionary<Guid, OpticalClientView> clients, CancellationToken cancellationToken)
    {
        var prescriptionIds = rows.Where(r => r.Kind == OpticalKind.Prescription).Select(r => r.RevisionId).ToList();
        var measurementIds = rows.Where(r => r.Kind == OpticalKind.Measurement).Select(r => r.RevisionId).ToList();
        var values = new Dictionary<Guid, OpticalValues>();
        if (prescriptionIds.Count > 0)
        {
            foreach (var revision in await context.Set<PrescriptionRevision>().AsNoTracking().Where(r => prescriptionIds.Contains(r.Id)).ToListAsync(cancellationToken))
            {
                values[revision.Id] = revision.Values;
            }
        }

        if (measurementIds.Count > 0)
        {
            foreach (var revision in await context.Set<MeasurementRevision>().AsNoTracking().Where(r => measurementIds.Contains(r.Id)).ToListAsync(cancellationToken))
            {
                values[revision.Id] = revision.Values;
            }
        }

        return
        [
            .. rows.Select(r => new OpticalSummaryView(
                r.Kind, r.Id, r.Reference, clients[r.ClientId], r.Status, r.RevisionNo, r.Date, r.Source,
                values.GetValueOrDefault(r.RevisionId, OpticalValues.Empty), r.RecordedAtUtc)),
        ];
    }
}
