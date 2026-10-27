using Microsoft.EntityFrameworkCore;
using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure.Audit;

/// <summary>
/// Ajoute l'événement au contexte EF PARTAGÉ de la requête (portée DI) : il est inséré par le même
/// <c>SaveChanges</c>, donc dans la même transaction, que la modification métier. Un échec (conflit de version →
/// <c>ChangeTracker.Clear()</c>, contrainte, panne) l'emporte avec lui : il n'est jamais écrit seul.
/// </summary>
internal sealed class AuditRecorder(MmvDbContext context, IAuditContext request) : IAuditRecorder
{
    private Guid _last;

    public void Record(AuditRecord record) =>
        context.AuditEvents.Add(AuditEvent.Create(
            NextId(record.OccurredAtUtc),
            record.OccurredAtUtc,
            record.ActorUserId,
            record.ClientId,
            record.Module,
            record.Action,
            record.EntityType,
            record.EntityId,
            AuditChanges.Serialize(record.Changes),
            request.SessionId,
            request.TraceId));

    /// <summary>
    /// UUIDv7 strictement croissant dans l'unité de travail : les événements d'une même opération partagent leur instant
    /// et la partie aléatoire d'un UUIDv7 ne les ordonnerait pas ; l'ordre d'enregistrement (déplacement, responsable,
    /// modification) est ainsi conservé par le tri « instant puis identifiant ».
    /// </summary>
    internal Guid NextId(DateTimeOffset at)
    {
        var id = Guid.CreateVersion7(at);
        if (id.CompareTo(_last) <= 0)
        {
            Span<byte> bytes = stackalloc byte[16];
            _last.TryWriteBytes(bytes, bigEndian: true, out _);
            for (var index = 15; index > 9 && ++bytes[index] == 0; index--)
            {
            }

            id = new Guid(bytes, bigEndian: true);
        }

        _last = id;
        return id;
    }
}

internal sealed class AuditStore(MmvDbContext context) : IAuditStore
{
    public async Task<AuditPage> ListForClientAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var events = context.AuditEvents.AsNoTracking().Where(e => e.ClientId == query.ClientId);
        if (query.Module is { } module)
        {
            events = events.Where(e => e.Module == module);
        }

        if (query.ExcludedModules.Count > 0)
        {
            var excluded = query.ExcludedModules.ToList();
            events = events.Where(e => !excluded.Contains(e.Module));
        }

        if (query.FromUtc is { } from)
        {
            events = events.Where(e => e.OccurredAtUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            events = events.Where(e => e.OccurredAtUtc < to);
        }

        var total = await events.CountAsync(cancellationToken);
        // Tri AVANT la projection (traduisible en SQL), index ix_events_client_occurred.
        var items = await (from entry in events.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
                               .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                           join actor in context.Users.AsNoTracking() on entry.ActorUserId equals actor.Id
                           orderby entry.OccurredAtUtc descending, entry.Id descending
                           select new AuditEntryView(entry, actor.DisplayName))
            .ToListAsync(cancellationToken);
        return new AuditPage(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
}
