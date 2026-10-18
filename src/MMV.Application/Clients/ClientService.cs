using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Clients;

namespace MMV.Application.Clients;

public enum ClientStatusFilter
{
    Active,
    Archived,
    All,
}

public enum ClientSortField
{
    Name,
    CreatedAt,
    City,
}

/// <summary>Critères de liste déjà validés (liste blanche appliquée par MMV.Api).</summary>
public sealed record ClientListQuery(
    string? Search, ClientStatusFilter Status, ClientSortField Sort, bool Descending, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int SearchMinLength = 2;
    public const int SearchMaxLength = 100;
}

public sealed record ClientPage(IReadOnlyList<Client> Items, int Page, int PageSize, int TotalCount);

public enum ClientWriteStatus
{
    Success,
    NotFound,
    ValidationFailed,
    VersionConflict,
    ClientArchived,
    AlreadyArchived,
    AlreadyActive,
}

public sealed record ClientWriteResult(
    ClientWriteStatus Status, Client? Client = null, IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>Persistance des clients (implémentée par MMV.Infrastructure sur PostgreSQL).</summary>
public interface IClientStore
{
    Task<ClientPage> ListAsync(ClientListQuery query, CancellationToken cancellationToken);

    /// <summary>Lecture seule (non suivie).</summary>
    Task<Client?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Client suivi pour modification, ou <c>null</c>.</summary>
    Task<Client?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    void Add(Client client);

    /// <summary>
    /// Enregistre ; renvoie <c>false</c> si la version lue n'est plus la version courante en base
    /// (écriture concurrente), sans rien écrire. La référence et la nouvelle version sont relues.
    /// </summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Cas d'usage Clients (D-03-01 à D-03-07). Aucune suppression physique ; un client archivé est en lecture
/// seule ; toute écriture porte la version lue et échoue en conflit si elle est périmée (jamais d'écrasement
/// silencieux). L'auteur de l'écriture est le compte authentifié transmis par MMV.Api. 03-D2 : chaque écriture
/// réussie produit son événement d'audit dans le même enregistrement (création, modification des informations,
/// archivage, réactivation ; champs modifiés par leur nom seulement).
/// </summary>
public sealed class ClientService(IClientStore store, TimeProvider clock, IAuditRecorder audit)
{
    public Task<ClientPage> ListAsync(ClientListQuery query, CancellationToken cancellationToken) =>
        store.ListAsync(query, cancellationToken);

    public Task<Client?> GetAsync(Guid id, CancellationToken cancellationToken) => store.GetAsync(id, cancellationToken);

    public async Task<ClientWriteResult> CreateAsync(ClientInput input, Guid userId, CancellationToken cancellationToken)
    {
        var now = Now();
        var validation = ClientInputValidator.Validate(input, DateOnly.FromDateTime(now.UtcDateTime));
        if (!validation.IsValid)
        {
            return new ClientWriteResult(ClientWriteStatus.ValidationFailed, Errors: validation.Errors);
        }

        var client = Client.Create(Guid.CreateVersion7(now), validation.Details!, userId, now);
        store.Add(client);
        Record(client, AuditActions.ClientCreated, ClientAuditChanges.Between(null, validation.Details!), userId, now);
        await store.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteStatus.Success, client);
    }

    public async Task<ClientWriteResult> UpdateAsync(
        Guid id, ClientInput input, uint version, Guid userId, CancellationToken cancellationToken)
    {
        var client = await store.FindForUpdateAsync(id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteStatus.NotFound);
        }

        if (client.IsArchived)
        {
            return new ClientWriteResult(ClientWriteStatus.ClientArchived, client);
        }

        if (client.Version != version)
        {
            return new ClientWriteResult(ClientWriteStatus.VersionConflict, client);
        }

        var now = Now();
        var validation = ClientInputValidator.Validate(input, DateOnly.FromDateTime(now.UtcDateTime));
        if (!validation.IsValid)
        {
            return new ClientWriteResult(ClientWriteStatus.ValidationFailed, Errors: validation.Errors);
        }

        var before = ClientAuditChanges.Snapshot(client);
        client.Update(validation.Details!, userId, now);
        var changes = ClientAuditChanges.Between(before, validation.Details!);
        if (changes.Count > 0)
        {
            Record(client, AuditActions.ClientUpdated, changes, userId, now);
        }

        return await SaveAsync(client, cancellationToken);
    }

    public Task<ClientWriteResult> ArchiveAsync(Guid id, uint version, Guid userId, CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(id, version, archive: true, userId, cancellationToken);

    public Task<ClientWriteResult> ReactivateAsync(Guid id, uint version, Guid userId, CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(id, version, archive: false, userId, cancellationToken);

    private async Task<ClientWriteResult> ChangeLifecycleAsync(
        Guid id, uint version, bool archive, Guid userId, CancellationToken cancellationToken)
    {
        var client = await store.FindForUpdateAsync(id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteStatus.NotFound);
        }

        if (client.Version != version)
        {
            return new ClientWriteResult(ClientWriteStatus.VersionConflict, client);
        }

        if (archive == client.IsArchived)
        {
            return new ClientWriteResult(archive ? ClientWriteStatus.AlreadyArchived : ClientWriteStatus.AlreadyActive, client);
        }

        var now = Now();
        if (archive)
        {
            client.Archive(userId, now);
        }
        else
        {
            client.Reactivate(userId, now);
        }

        Record(client, archive ? AuditActions.ClientArchived : AuditActions.ClientReactivated, [], userId, now);

        return await SaveAsync(client, cancellationToken);
    }

    private async Task<ClientWriteResult> SaveAsync(Client client, CancellationToken cancellationToken) =>
        await store.SaveChangesAsync(cancellationToken)
            ? new ClientWriteResult(ClientWriteStatus.Success, client)
            : new ClientWriteResult(ClientWriteStatus.VersionConflict);

    private void Record(Client client, string action, IReadOnlyList<AuditChange> changes, Guid userId, DateTimeOffset now) =>
        audit.Record(new AuditRecord(userId, now, client.Id, AuditActions.ClientsModule, action, AuditActions.ClientEntity, client.Id, changes));

    // Précision PostgreSQL (microseconde) : la valeur renvoyée est identique à celle relue ensuite.
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }
}
