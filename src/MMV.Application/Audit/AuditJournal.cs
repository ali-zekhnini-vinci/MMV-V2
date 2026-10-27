using System.Globalization;
using System.Text.Json;
using MMV.Domain.Appointments;
using MMV.Domain.Audit;
using MMV.Domain.Clients;

namespace MMV.Application.Audit;

/// <summary>Contexte technique de la requête en cours (session et corrélation), jamais de jeton ni de secret.</summary>
public interface IAuditContext
{
    Guid? SessionId { get; }

    string? TraceId { get; }
}

/// <summary>Hors requête HTTP (outils, tests) : aucun identifiant technique supplémentaire.</summary>
public sealed class NoAuditContext : IAuditContext
{
    public Guid? SessionId => null;

    public string? TraceId => null;
}

/// <summary>Action réussie à journaliser ; l'auteur est le compte authentifié transmis par MMV.Api.</summary>
public sealed record AuditRecord(
    Guid ActorUserId,
    DateTimeOffset OccurredAtUtc,
    Guid? ClientId,
    string Module,
    string Action,
    string EntityType,
    Guid EntityId,
    IReadOnlyList<AuditChange> Changes);

/// <summary>
/// Enregistrement d'un événement d'audit dans l'unité de travail EN COURS (03-D2) : l'événement est écrit par le même
/// <c>SaveChanges</c> / la même transaction PostgreSQL que la modification métier. Si l'enregistrement métier échoue
/// (conflit de version, contrainte, panne), l'événement n'est jamais écrit : aucune action ratée n'apparaît réussie.
/// À appeler APRÈS les contrôles métier, juste avant l'enregistrement.
/// </summary>
public interface IAuditRecorder
{
    void Record(AuditRecord record);
}

public sealed record AuditQuery(Guid ClientId, string? Module, DateTimeOffset? FromUtc, DateTimeOffset? ToUtc, int Page, int PageSize)
{
    /// <summary>Modules exclus par le serveur selon les droits de la session (04-B : <c>optical</c> sans <c>optical.read</c>).</summary>
    public IReadOnlyList<string> ExcludedModules { get; init; } = [];

    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 100_000;
}

/// <summary>Événement lu, avec le nom affiché ACTUEL de son auteur.</summary>
public sealed record AuditEntryView(AuditEvent Event, string ActorDisplayName);

public sealed record AuditPage(IReadOnlyList<AuditEntryView> Items, int Page, int PageSize, int TotalCount);

public interface IAuditStore
{
    /// <summary>Événements d'un dossier client, du plus récent au plus ancien (puis identifiant décroissant).</summary>
    Task<AuditPage> ListForClientAsync(AuditQuery query, CancellationToken cancellationToken);

    /// <summary>Noms affichés actuels des comptes (responsables cités dans les champs modifiés).</summary>
    Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

/// <summary>Lecture du journal (droit <c>audit.read</c>, contrôlé par MMV.Api).</summary>
public sealed class AuditService(IAuditStore store)
{
    public Task<AuditPage> ListForClientAsync(AuditQuery query, CancellationToken cancellationToken) => store.ListForClientAsync(query, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        store.DisplayNamesAsync(userIds, cancellationToken);
}

/// <summary>Sérialisation stable des champs modifiés (JSON, propriétés camelCase).</summary>
public static class AuditChanges
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string? Serialize(IReadOnlyList<AuditChange> changes) => changes.Count == 0 ? null : JsonSerializer.Serialize(changes, Options);

    public static IReadOnlyList<AuditChange> Deserialize(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<AuditChange>>(json, Options) ?? [];
}

/// <summary>Champs modifiés d'un client : NOMS seulement (toutes les valeurs client sont personnelles, donc masquées).</summary>
public static class ClientAuditChanges
{
    public static IReadOnlyList<AuditChange> Between(ClientDetails? before, ClientDetails after)
    {
        var changes = new List<AuditChange>();
        void Compare<T>(string field, T? previous, T? current)
        {
            if (before is null ? current is not null : !EqualityComparer<T?>.Default.Equals(previous, current))
            {
                changes.Add(AuditFieldPolicy.Change(field, null, null));
            }
        }

        Compare("firstName", before?.FirstName, after.FirstName);
        Compare("lastName", before?.LastName, after.LastName);
        Compare("birthDate", before?.BirthDate, after.BirthDate);
        Compare("phone", before?.Phone, after.Phone);
        Compare("email", before?.Email, after.Email);
        Compare("addressLine", before?.AddressLine, after.AddressLine);
        Compare("city", before?.City, after.City);
        Compare("postalCode", before?.PostalCode, after.PostalCode);
        Compare("notes", before?.Notes, after.Notes);
        return changes;
    }

    public static ClientDetails Snapshot(Client client) =>
        new(client.FirstName, client.LastName, client.BirthDate, client.Phone, client.Email, client.AddressLine, client.City, client.PostalCode, client.Notes);
}

/// <summary>
/// Événements d'un rendez-vous (03-D2). Une modification est décomposée en catégories distinctes, dans cet ordre :
/// déplacement (créneau ou durée), changement de responsable, modification (motif, notes, client). Le responsable
/// (collaborateur) figure dans les champs ; l'auteur de l'action est toujours le compte connecté.
/// </summary>
public static class AppointmentAuditChanges
{
    public static string Instant(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string? Id(Guid? value) => value?.ToString();

    private static string Minutes(TimeSpan duration) => ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture);

    public static IReadOnlyList<AuditChange> Created(Appointment appointment) =>
    [
        AuditFieldPolicy.Change("startsAtUtc", null, Instant(appointment.StartsAtUtc)),
        AuditFieldPolicy.Change("durationMinutes", null, Minutes(appointment.Duration)),
        AuditFieldPolicy.Change("reasonCode", null, appointment.ReasonCode),
        AuditFieldPolicy.Change("responsibleUserId", null, Id(appointment.ResponsibleUserId)),
        .. appointment.Notes is null ? Array.Empty<AuditChange>() : [AuditFieldPolicy.Change("notes", null, null)],
    ];

    /// <summary>Catégories (action, champs) d'une modification ; vide si rien n'a changé.</summary>
    public static IReadOnlyList<(string Action, IReadOnlyList<AuditChange> Changes)> Updated(AppointmentDetails before, AppointmentDetails after)
    {
        var result = new List<(string, IReadOnlyList<AuditChange>)>();
        var moved = new List<AuditChange>();
        if (before.StartsAtUtc != after.StartsAtUtc)
        {
            moved.Add(AuditFieldPolicy.Change("startsAtUtc", Instant(before.StartsAtUtc), Instant(after.StartsAtUtc)));
        }

        if (before.EndsAtUtc - before.StartsAtUtc != after.EndsAtUtc - after.StartsAtUtc)
        {
            moved.Add(AuditFieldPolicy.Change("durationMinutes", Minutes(before.EndsAtUtc - before.StartsAtUtc), Minutes(after.EndsAtUtc - after.StartsAtUtc)));
        }

        if (moved.Count > 0)
        {
            result.Add((AuditActions.AppointmentMoved, moved));
        }

        if (before.ResponsibleUserId != after.ResponsibleUserId)
        {
            result.Add((AuditActions.AppointmentResponsibleChanged, [AuditFieldPolicy.Change("responsibleUserId", Id(before.ResponsibleUserId), Id(after.ResponsibleUserId))]));
        }

        var updated = new List<AuditChange>();
        if (before.ReasonCode != after.ReasonCode)
        {
            updated.Add(AuditFieldPolicy.Change("reasonCode", before.ReasonCode, after.ReasonCode));
        }

        if (before.Notes != after.Notes)
        {
            updated.Add(AuditFieldPolicy.Change("notes", null, null));
        }

        if (before.ClientId != after.ClientId)
        {
            updated.Add(AuditFieldPolicy.Change("clientId", null, null));
        }

        if (updated.Count > 0)
        {
            result.Add((AuditActions.AppointmentUpdated, updated));
        }

        return result;
    }

    public static AppointmentDetails Snapshot(Appointment appointment) =>
        new(appointment.ClientId, appointment.ResponsibleUserId, appointment.StartsAtUtc, appointment.EndsAtUtc, appointment.ReasonCode, appointment.Notes);
}
