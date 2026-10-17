namespace MMV.Domain.Appointments;

/// <summary>
/// Rendez-vous du magasin (phase 03-B2, décisions D-03-13 à D-03-18 approuvées le 17/10/2026). Un client
/// (<c>clients.clients</c>), un responsable (compte MMV <c>auth.users</c>) obligatoire ou facultatif selon le motif
/// (D-03-14), un intervalle UTC semi-ouvert [début, fin[, un motif de la liste fermée (D-03-18), des notes
/// opérationnelles non médicales. Cycle de vie : planifié ou annulé, jamais supprimé (D-03-16). Concurrence :
/// colonne système <c>xmin</c> exposée comme <see cref="Version"/> (même principe que D-03-06). Les règles qui
/// dépendent de l'horloge, du fuseau du magasin ou des autres rendez-vous (passé, conflit, capacité d'accueil,
/// client archivé) sont vérifiées par le service applicatif dans la transaction, pas par l'entité.
/// </summary>
public sealed class Appointment
{
    public const int ReasonCodeMaxLength = 32;
    public const int NotesMaxLength = 1000;
    public const int CancellationReasonMaxLength = 200;
    public const int StatusMaxLength = 16;
    public const string StatusPlanned = "planned";
    public const string StatusCancelled = "cancelled";

    /// <summary>Durée maximale admise par la base (contrainte <c>ck_appointments_duration_range</c>, garde-fou).</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);

    private Appointment()
    {
        ReasonCode = string.Empty;
        Status = StatusPlanned;
    }

    public Guid Id { get; private set; }

    public Guid ClientId { get; private set; }

    /// <summary><c>null</c> : rendez-vous d'accueil non attribué, imputé sur la capacité d'accueil du magasin (D-03-15).</summary>
    public Guid? ResponsibleUserId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    /// <summary>Fin exclue : deux rendez-vous bout à bout (10:00–10:30, 10:30–11:00) ne se chevauchent pas.</summary>
    public DateTimeOffset EndsAtUtc { get; private set; }

    public string ReasonCode { get; private set; }

    /// <summary>Notes opérationnelles (consigne : aucune donnée de santé), jamais journalisées.</summary>
    public string? Notes { get; private set; }

    public string Status { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Jeton de concurrence optimiste : colonne système <c>xmin</c> (aucune colonne créée).</summary>
    public uint Version { get; private set; }

    public bool IsCancelled => Status == StatusCancelled;

    public TimeSpan Duration => EndsAtUtc - StartsAtUtc;

    /// <summary>Nouveau rendez-vous planifié. <paramref name="details"/> doit avoir été validé (MMV.Application).</summary>
    public static Appointment Create(Guid id, AppointmentDetails details, Guid userId, DateTimeOffset now)
    {
        var appointment = new Appointment
        {
            Id = id,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
        };
        appointment.Apply(details, userId, now);
        return appointment;
    }

    /// <summary>Modification ou déplacement. Un rendez-vous annulé est en lecture seule.</summary>
    public void Update(AppointmentDetails details, Guid userId, DateTimeOffset now)
    {
        if (IsCancelled)
        {
            throw new InvalidOperationException("Un rendez-vous annulé n'est pas modifiable.");
        }

        Apply(details, userId, now);
    }

    /// <summary>Annulation logique : auteur, date et motif facultatif conservés (D-03-16).</summary>
    public void Cancel(string? reason, Guid userId, DateTimeOffset now)
    {
        if (IsCancelled)
        {
            throw new InvalidOperationException("Rendez-vous déjà annulé.");
        }

        Status = StatusCancelled;
        CancelledAtUtc = now;
        CancelledByUserId = userId;
        CancellationReason = reason;
        Touch(userId, now);
    }

    /// <summary>Rétablissement ; le service a vérifié avant que le créneau ou la capacité est disponible (D-03-15).</summary>
    public void Restore(Guid userId, DateTimeOffset now)
    {
        if (!IsCancelled)
        {
            throw new InvalidOperationException("Rendez-vous déjà planifié.");
        }

        Status = StatusPlanned;
        CancelledAtUtc = null;
        CancelledByUserId = null;
        CancellationReason = null;
        Touch(userId, now);
    }

    /// <summary>Chevauchement semi-ouvert : <c>a.début &lt; b.fin ET b.début &lt; a.fin</c> (D-03-15).</summary>
    public bool Overlaps(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc) => StartsAtUtc < endsAtUtc && startsAtUtc < EndsAtUtc;

    private void Apply(AppointmentDetails details, Guid userId, DateTimeOffset now)
    {
        if (details.EndsAtUtc <= details.StartsAtUtc || details.EndsAtUtc - details.StartsAtUtc > MaxDuration)
        {
            throw new ArgumentException("Intervalle de rendez-vous invalide.", nameof(details));
        }

        ClientId = details.ClientId;
        ResponsibleUserId = details.ResponsibleUserId;
        StartsAtUtc = details.StartsAtUtc;
        EndsAtUtc = details.EndsAtUtc;
        ReasonCode = details.ReasonCode;
        Notes = details.Notes;
        Touch(userId, now);
    }

    private void Touch(Guid userId, DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
    }
}

/// <summary>Données saisissables d'un rendez-vous, déjà validées (instants UTC, fin exclue).</summary>
public sealed record AppointmentDetails(
    Guid ClientId,
    Guid? ResponsibleUserId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ReasonCode,
    string? Notes);
