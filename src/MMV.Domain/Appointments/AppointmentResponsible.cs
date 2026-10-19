namespace MMV.Domain.Appointments;

/// <summary>
/// Éligibilité EXPLICITE d'un collaborateur à être responsable de rendez-vous (D-03-14, D-03-17), indépendante des
/// permissions <c>appointments.*</c> (qui donnent accès à l'agenda) et de <c>system.admin</c>. Un compte n'est
/// proposé comme responsable que s'il est actif ET éligible. Gérée par la commande locale
/// <c>MMV.Maintenance responsibles enable|disable</c> ; jamais supprimée (retrait = <see cref="IsEligible"/> faux).
/// </summary>
public sealed class AppointmentResponsible
{
    private AppointmentResponsible()
    {
    }

    public Guid UserId { get; private set; }

    public bool IsEligible { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static AppointmentResponsible Enable(Guid userId, DateTimeOffset now) => new()
    {
        UserId = userId,
        IsEligible = true,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    /// <summary>Change l'éligibilité ; renvoie <c>false</c> si elle était déjà dans l'état demandé.</summary>
    public bool SetEligible(bool eligible, DateTimeOffset now)
    {
        if (IsEligible == eligible)
        {
            return false;
        }

        IsEligible = eligible;
        UpdatedAtUtc = now;
        return true;
    }
}
