using System.Text.RegularExpressions;

namespace MMV.Domain.Audit;

/// <summary>
/// Événement du journal d'audit métier (phase 03-D2) : une action RÉUSSIE sur un dossier, enregistrée par le serveur
/// dans la même transaction que la modification métier (jamais par l'interface). Journal en ajout seul : aucune
/// modification ni suppression (déclencheur PostgreSQL <c>trg_events_append_only</c>). Réutilisable par les modules
/// futurs (ordonnances, ventes, commandes, stock) : <see cref="Module"/>, <see cref="Action"/> et
/// <see cref="EntityType"/> sont des codes ouverts au format contrôlé, le catalogue des actions vivant dans le code
/// (<see cref="AuditActions"/>) — aucun modèle métier futur n'est présumé ici.
/// </summary>
public sealed partial class AuditEvent
{
    public const int CodeMaxLength = 64;
    public const int ModuleMaxLength = 32;
    public const int EntityTypeMaxLength = 32;
    public const int TraceIdMaxLength = 64;

    private AuditEvent()
    {
        Module = string.Empty;
        Action = string.Empty;
        EntityType = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Instant UTC de l'action (horloge du serveur).</summary>
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>Compte MMV qui a RÉELLEMENT exécuté l'action (jamais le collaborateur responsable d'un rendez-vous).</summary>
    public Guid ActorUserId { get; private set; }

    /// <summary>Dossier client concerné ; <c>null</c> réservé aux futurs événements sans dossier client (stock…).</summary>
    public Guid? ClientId { get; private set; }

    /// <summary>Module propriétaire de l'action (<c>clients</c>, <c>appointments</c>…).</summary>
    public string Module { get; private set; }

    /// <summary>Code d'action <c>entité.verbe</c> (<see cref="AuditActions"/>).</summary>
    public string Action { get; private set; }

    /// <summary>Entité métier concernée (<c>client</c>, <c>appointment</c>…) et son identifiant.</summary>
    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    /// <summary>
    /// Champs modifiés (JSON, <see cref="AuditChange"/>) : nom du champ, et anciennes / nouvelles valeurs SEULEMENT pour
    /// les champs opérationnels non personnels (<see cref="AuditFieldPolicy"/>) ; sinon « masqué ».
    /// </summary>
    public string? Changes { get; private set; }

    /// <summary>Session MMV d'origine (identifiant technique, jamais le jeton).</summary>
    public Guid? SessionId { get; private set; }

    /// <summary>Identifiant de la requête HTTP (corrélation avec les journaux techniques).</summary>
    public string? TraceId { get; private set; }

    public static AuditEvent Create(
        Guid id,
        DateTimeOffset occurredAtUtc,
        Guid actorUserId,
        Guid? clientId,
        string module,
        string action,
        string entityType,
        Guid entityId,
        string? changes,
        Guid? sessionId,
        string? traceId)
    {
        if (!ModulePattern().IsMatch(module) || !ActionPattern().IsMatch(action) || !ModulePattern().IsMatch(entityType))
        {
            throw new ArgumentException("Code d'audit invalide.");
        }

        if (actorUserId == Guid.Empty || entityId == Guid.Empty)
        {
            throw new ArgumentException("Auteur et entité obligatoires.");
        }

        return new AuditEvent
        {
            Id = id,
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = actorUserId,
            ClientId = clientId,
            Module = module,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Changes = changes,
            SessionId = sessionId,
            TraceId = traceId is { Length: > TraceIdMaxLength } ? traceId[..TraceIdMaxLength] : traceId,
        };
    }

    /// <summary>Format des codes de module et d'entité (contrainte <c>ck_events_module_format</c>).</summary>
    public const string ModuleRegex = "^[a-z][a-z_]{0,31}$";

    /// <summary>Format des codes d'action (contrainte <c>ck_events_action_format</c>).</summary>
    public const string ActionRegex = "^[a-z][a-z_]{0,30}\\.[a-z][a-z_]{0,31}$";

    [GeneratedRegex(ModuleRegex, RegexOptions.CultureInvariant)]
    private static partial Regex ModulePattern();

    [GeneratedRegex(ActionRegex, RegexOptions.CultureInvariant)]
    private static partial Regex ActionPattern();
}

/// <summary>Champ modifié : valeurs absentes et <see cref="Masked"/> vrai pour un champ dont la valeur n'est pas conservée.</summary>
public sealed record AuditChange(string Field, string? Old, string? New, bool Masked);

/// <summary>Catalogue des actions journalisées (03-D2). Un module futur ajoute ses codes ici, sans migration.</summary>
public static class AuditActions
{
    public const string ClientsModule = "clients";
    public const string AppointmentsModule = "appointments";
    public const string ClientEntity = "client";
    public const string AppointmentEntity = "appointment";

    public const string ClientCreated = "client.created";
    public const string ClientUpdated = "client.updated";
    public const string ClientArchived = "client.archived";
    public const string ClientReactivated = "client.reactivated";

    public const string AppointmentCreated = "appointment.created";
    public const string AppointmentUpdated = "appointment.updated";
    public const string AppointmentMoved = "appointment.moved";
    public const string AppointmentResponsibleChanged = "appointment.responsible_changed";
    public const string AppointmentCancelled = "appointment.cancelled";
    public const string AppointmentRestored = "appointment.restored";

    public static IReadOnlyList<string> All { get; } =
    [
        ClientCreated, ClientUpdated, ClientArchived, ClientReactivated,
        AppointmentCreated, AppointmentUpdated, AppointmentMoved, AppointmentResponsibleChanged, AppointmentCancelled, AppointmentRestored,
    ];
}

/// <summary>
/// Politique de conservation des valeurs (03-D2, minimisation) : SEULS les champs opérationnels non personnels gardent
/// leurs anciennes et nouvelles valeurs (créneau, durée, motif codifié, responsable — identifiant de compte MMV, statut
/// du dossier). Tous les champs d'identité, de contact, d'adresse, de date de naissance et toutes les zones de texte
/// libre (notes, motif d'annulation) sont journalisés par leur NOM seulement (valeur masquée) : la valeur courante
/// reste consultable sur la fiche selon les droits Clients, l'historique ne la duplique pas. Aucune donnée de santé,
/// aucun mot de passe, jeton ni secret n'entre dans le journal.
/// </summary>
public static class AuditFieldPolicy
{
    /// <summary>Champs dont les valeurs sont conservées (liste blanche ; tout autre champ est masqué).</summary>
    public static IReadOnlySet<string> ValuesKept { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "startsAtUtc", "durationMinutes", "reasonCode", "responsibleUserId",
    };

    public static AuditChange Change(string field, string? oldValue, string? newValue) =>
        ValuesKept.Contains(field) ? new AuditChange(field, oldValue, newValue, Masked: false) : new AuditChange(field, null, null, Masked: true);
}
