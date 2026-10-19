using MMV.Domain.Appointments;

namespace MMV.Application.Appointments;

/// <summary>
/// Saisie brute d'un rendez-vous (corps HTTP), avant validation. <see cref="StartsAtLocal"/> : heure murale du
/// magasin (<c>AAAA-MM-JJTHH:mm</c>) que le POSTE a convertie en <see cref="StartsAtUtc"/> ; le serveur la recalcule
/// avec ses propres données de fuseaux et refuse tout écart (B2-REV-03, 03-B3).
/// </summary>
public sealed record AppointmentInput(
    Guid? ClientId,
    Guid? ResponsibleUserId,
    string? StartsAtUtc,
    string? StartsAtLocal,
    int? DurationMinutes,
    string? ReasonCode,
    string? Notes);

/// <summary>Saisie validée, indépendamment de la base et de l'horloge.</summary>
public sealed record ValidatedAppointment(
    Guid ClientId,
    Guid? ResponsibleUserId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    AppointmentReason Reason,
    string? Notes)
{
    public AppointmentDetails ToDetails() => new(ClientId, ResponsibleUserId, StartsAtUtc, EndsAtUtc, Reason.Code, Notes);
}

public sealed record AppointmentValidation(ValidatedAppointment? Value, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Value is not null;
}

/// <summary>
/// Contrôles de saisie (422 <c>validation_failed</c>, codes par champ) qui ne dépendent ni de la base ni de
/// l'horloge : formats, liste des durées et des motifs (D-03-13, D-03-18), créneau dans le fuseau du magasin
/// (<see cref="AppointmentSchedule"/>), mode d'attribution (D-03-14 : responsable obligatoire pour un rendez-vous
/// personnalisé ; sans responsable, une capacité d'accueil doit être configurée), longueur des notes.
/// L'existence du client, l'éligibilité du responsable, le passé et les conflits sont contrôlés par
/// <see cref="AppointmentService"/>.
/// </summary>
public static class AppointmentInputValidator
{
    public const string Required = "required";
    public const string InvalidFormat = "invalid_format";
    public const string NotAllowed = "not_allowed";
    public const string TooLong = "too_long";
    public const string ReceptionNotConfigured = "reception_not_configured";

    public static AppointmentValidation Validate(AppointmentInput input, AppointmentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.TimeZone);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (input.ClientId is null || input.ClientId == Guid.Empty)
        {
            errors["clientId"] = [Required];
        }

        var reason = AppointmentReasons.Find(input.ReasonCode);
        if (string.IsNullOrEmpty(input.ReasonCode))
        {
            errors["reasonCode"] = [Required];
        }
        else if (reason is null)
        {
            errors["reasonCode"] = [NotAllowed];
        }

        if (input.DurationMinutes is null)
        {
            errors["durationMinutes"] = [Required];
        }
        else if (!AppointmentRules.DurationsMinutes.Contains(input.DurationMinutes.Value))
        {
            errors["durationMinutes"] = [NotAllowed];
        }

        DateTimeOffset start = default;
        var startParsed = false;
        if (string.IsNullOrEmpty(input.StartsAtUtc))
        {
            errors["startsAtUtc"] = [Required];
        }
        else if (!(startParsed = AppointmentSchedule.TryParseUtcInstant(input.StartsAtUtc, out start)))
        {
            errors["startsAtUtc"] = [InvalidFormat];
        }
        else if (!errors.ContainsKey("durationMinutes")
                 && AppointmentSchedule.CheckSlot(settings.TimeZone, start, start.AddMinutes(input.DurationMinutes!.Value)) is { } slotError)
        {
            errors["startsAtUtc"] = [slotError];
        }

        // Heure murale annoncée par le poste : présente, stricte, et IDENTIQUE à celle du serveur pour cet instant.
        if (string.IsNullOrEmpty(input.StartsAtLocal))
        {
            errors["startsAtLocal"] = [Required];
        }
        else if (!AppointmentSchedule.IsLocalWallTime(input.StartsAtLocal))
        {
            errors["startsAtLocal"] = [InvalidFormat];
        }
        else if (startParsed && AppointmentSchedule.LocalWallTime(settings.TimeZone, start) != input.StartsAtLocal)
        {
            errors["startsAtLocal"] = [AppointmentSchedule.TimeZoneMismatch];
        }

        var responsible = input.ResponsibleUserId == Guid.Empty ? null : input.ResponsibleUserId;
        if (responsible is null && reason is not null)
        {
            if (reason.ResponsibleRequired)
            {
                errors["responsibleUserId"] = [Required];
            }
            else if (settings.ReceptionCapacity is null)
            {
                errors["responsibleUserId"] = [ReceptionNotConfigured];
            }
        }

        var notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        if (notes is { Length: > Appointment.NotesMaxLength })
        {
            errors["notes"] = [TooLong];
        }

        if (errors.Count > 0)
        {
            return new AppointmentValidation(null, errors);
        }

        var value = new ValidatedAppointment(
            input.ClientId!.Value, responsible, start, start.AddMinutes(input.DurationMinutes!.Value), reason!, notes);
        return new AppointmentValidation(value, errors);
    }

    /// <summary>Motif d'annulation facultatif (D-03-16) : rogné, vide → <c>null</c>, 200 caractères au plus.</summary>
    public static bool TryNormalizeCancellationReason(string? reason, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return normalized is null or { Length: <= Appointment.CancellationReasonMaxLength };
    }
}
