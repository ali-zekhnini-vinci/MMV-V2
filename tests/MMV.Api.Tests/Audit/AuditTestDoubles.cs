using MMV.Application.Audit;
using MMV.Domain.Audit;

namespace MMV.Api.Tests.Audit;

/// <summary>
/// Double de <see cref="IAuditRecorder"/> qui imite l'unité de travail EF : les événements restent EN ATTENTE jusqu'à
/// l'enregistrement réussi du store doublé (<see cref="Commit"/>), et sont abandonnés si l'enregistrement échoue
/// (<see cref="Discard"/>). L'atomicité réelle (même transaction PostgreSQL) est prouvée par MMV.PostgreSql.Tests.
/// </summary>
internal sealed class InMemoryAuditRecorder : IAuditRecorder
{
    private readonly Lock _gate = new();
    private readonly List<AuditRecord> _pending = [];

    public List<AuditRecord> Committed { get; } = [];

    public void Record(AuditRecord record)
    {
        lock (_gate)
        {
            _pending.Add(record);
        }
    }

    public void Commit()
    {
        lock (_gate)
        {
            Committed.AddRange(_pending);
            _pending.Clear();
        }
    }

    public void Discard()
    {
        lock (_gate)
        {
            _pending.Clear();
        }
    }

    public IReadOnlyList<AuditRecord> For(Guid clientId) => [.. Committed.Where(r => r.ClientId == clientId)];
}

/// <summary>Double de lecture du journal (pipeline HTTP) : événements et noms affichés fournis par le test.</summary>
internal sealed class InMemoryAuditStore : IAuditStore
{
    public List<AuditEvent> Events { get; } = [];

    public Dictionary<Guid, string> Names { get; } = [];

    public Task<AuditPage> ListForClientAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var matching = Events
            .Where(e => e.ClientId == query.ClientId && (query.Module is null || e.Module == query.Module)
                        && (query.FromUtc is null || e.OccurredAtUtc >= query.FromUtc) && (query.ToUtc is null || e.OccurredAtUtc < query.ToUtc))
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .ToList();
        IReadOnlyList<AuditEntryView> items =
        [
            .. matching.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                .Select(e => new AuditEntryView(e, Names.TryGetValue(e.ActorUserId, out var name) ? name : "Auteur inconnu")),
        ];
        return Task.FromResult(new AuditPage(items, query.Page, query.PageSize, matching.Count));
    }

    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Names.Where(n => userIds.Contains(n.Key)).ToDictionary());
}
