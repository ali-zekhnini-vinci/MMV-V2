using System.Globalization;
using System.Reflection;
using MMV.Api.Tests.Audit;
using MMV.Api.Tests.Clients;
using MMV.Application.Optical;
using MMV.Domain.Audit;
using MMV.Domain.Optical;

namespace MMV.Api.Tests.Optical;

/// <summary>
/// Double NON PERSISTANT de <see cref="IOpticalRecordStore{TRecord}"/> : imite ce que PostgreSQL fait réellement —
/// référence attribuée par séquence, nouvelle version (xmin) à chaque écriture, unicité de la requête de création — et
/// valide ou abandonne les événements d'audit avec l'enregistrement. Les contraintes, déclencheurs et transactions réels
/// sont prouvés par tests/MMV.PostgreSql.Tests (après application de 0006, G04-SQL).
/// </summary>
internal sealed class InMemoryOpticalStore<TRecord, TRevision, TContent>(string referencePrefix, InMemoryAuditRecorder audit) : IOpticalRecordStore<TRecord>
    where TRecord : OpticalRecord<TRevision, TContent>
    where TRevision : OpticalRevision<TContent>
    where TContent : OpticalContent
{
    private static readonly PropertyInfo ReferenceProperty = typeof(OpticalRecord<TRevision, TContent>).GetProperty(nameof(OpticalRecord<TRevision, TContent>.Reference))!;
    private static readonly PropertyInfo VersionProperty = typeof(OpticalRecord<TRevision, TContent>).GetProperty(nameof(OpticalRecord<TRevision, TContent>.Version))!;

    private readonly Lock _gate = new();
    private readonly HashSet<TRecord> _pending = [];
    private long _sequence;
    private uint _transaction = 5000;

    public List<TRecord> Records { get; } = [];

    /// <summary>Simule une écriture concurrente survenue entre la lecture et l'enregistrement.</summary>
    public bool FailNextSave { get; set; }

    public int SaveCount { get; private set; }

    public Task<TRecord?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var record = Records.SingleOrDefault(r => r.Id == id);
            if (record is not null)
            {
                _pending.Add(record);
            }

            return Task.FromResult(record);
        }
    }

    public Task<TRecord?> FindByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(Records.SingleOrDefault(r => r.CreationRequestId == requestId));
        }
    }

    public void Add(TRecord record)
    {
        lock (_gate)
        {
            _pending.Add(record);
        }
    }

    public Task<OpticalCommitResult> SaveChangesAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            SaveCount++;
            if (FailNextSave)
            {
                FailNextSave = false;
                _pending.Clear();
                audit.Discard();
                return Task.FromResult(OpticalCommitResult.VersionConflict);
            }

            foreach (var record in _pending)
            {
                if (record.Reference.Length == 0)
                {
                    if (Records.Any(r => r.CreationRequestId == record.CreationRequestId))
                    {
                        _pending.Clear();
                        audit.Discard();
                        return Task.FromResult(OpticalCommitResult.DuplicateCreationRequest);
                    }

                    ReferenceProperty.SetValue(record, string.Create(CultureInfo.InvariantCulture, $"{referencePrefix}-{++_sequence:000000}"));
                    Records.Add(record);
                }

                VersionProperty.SetValue(record, ++_transaction);
            }

            _pending.Clear();
            audit.Commit();
            return Task.FromResult(OpticalCommitResult.Saved);
        }
    }
}

internal sealed class InMemoryOpticalDirectory : IOpticalDirectory
{
    public List<OpticalActorView> Measurers { get; } = [];

    public Task<bool> IsEligibleMeasurerAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Measurers.Any(m => m.Id == userId));

    public Task<IReadOnlyList<OpticalActorView>> ListMeasurersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OpticalActorView>>([.. Measurers]);
}

/// <summary>Lecture doublée : mêmes règles de présentation que le store réel, calculées sur les agrégats en mémoire.</summary>
internal sealed class InMemoryOpticalReadStore(
    InMemoryOpticalStore<Prescription, PrescriptionRevision, PrescriptionContent> prescriptions,
    InMemoryOpticalStore<Measurement, MeasurementRevision, MeasurementContent> measurements,
    InMemoryClientStore clients,
    InMemoryAuditRecorder audit) : IOpticalReadStore
{
    public Dictionary<Guid, string> Names { get; } = [];

    public Task<OpticalSummaryPage> ListAsync(OpticalListQuery query, CancellationToken cancellationToken)
    {
        var rows = All().Where(r => (query.Kind is null || r.Kind == query.Kind) && OpticalPresentation.Matches(r.Status, query.Status, query.IncludeInvalidated)).ToList();
        var items = rows.OrderByDescending(r => r.RecordedAtUtc).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult(new OpticalSummaryPage(items, query.Page, query.PageSize, rows.Count));
    }

    public Task<IReadOnlyList<OpticalSummaryView>> ListForClientAsync(Guid clientId, bool includeInvalidated, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OpticalSummaryView>>(
            [.. All().Where(r => r.Client.Id == clientId && (includeInvalidated || r.Status != OpticalDisplayStatus.Invalidated)).OrderByDescending(r => r.RecordedAtUtc)]);

    public Task<Prescription?> GetPrescriptionAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(prescriptions.Records.SingleOrDefault(r => r.Id == id));

    public Task<Measurement?> GetMeasurementAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(measurements.Records.SingleOrDefault(r => r.Id == id));

    public async Task<OpticalClientView?> GetClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await clients.GetAsync(clientId, cancellationToken) is { } client
            ? new OpticalClientView(client.Id, client.Reference, OpticalPresentation.ClientDisplayName(client.FirstName, client.LastName), client.IsArchived)
            : null;

    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Names.Where(n => userIds.Contains(n.Key)).ToDictionary());

    public Task<IReadOnlyList<AuditEvent>> EventsAsync(string entityType, Guid entityId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(
        [
            .. audit.Committed.Where(r => r.EntityType == entityType && r.EntityId == entityId).Select(r => AuditEvent.Create(
                Guid.CreateVersion7(r.OccurredAtUtc), r.OccurredAtUtc, r.ActorUserId, r.ClientId, r.Module, r.Action, r.EntityType, r.EntityId,
                Application.Audit.AuditChanges.Serialize(r.Changes), null, null)),
        ]);

    private IEnumerable<OpticalSummaryView> All() =>
        prescriptions.Records.Select(r => Summary(OpticalKind.Prescription, r, r.LastConfirmed, r.OpenDraft, c => (c.Provenance.PrescribedOn, c.Provenance.PrescriberName)))
            .Concat(measurements.Records.Select(r => Summary(OpticalKind.Measurement, r, r.LastConfirmed, r.OpenDraft, c => (c.Provenance.MeasuredOn, (string?)null))));

    private OpticalSummaryView Summary<TRevision, TContent>(
        OpticalKind kind, OpticalRecord<TRevision, TContent> record, TRevision? confirmed, TRevision? draft, Func<TContent, (DateOnly?, string?)> source)
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
    {
        var shown = (confirmed ?? draft)!;
        var client = clients.Clients.Single(c => c.Id == record.ClientId);
        var (date, name) = source(shown.Content);
        return new OpticalSummaryView(
            kind, record.Id, record.Reference,
            new OpticalClientView(client.Id, client.Reference, OpticalPresentation.ClientDisplayName(client.FirstName, client.LastName), client.IsArchived),
            OpticalPresentation.Status(record.IsInvalidated, confirmed is not null, draft is not null), confirmed?.RevisionNo, date, name, shown.Values, shown.StartedAtUtc);
    }
}
