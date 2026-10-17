using MMV.Application.Identity;
using MMV.Domain.Appointments;
using MMV.Domain.Identity;

namespace MMV.Application.Appointments;

/// <summary>Persistance de l'éligibilité des collaborateurs (table <c>appointments.responsibles</c>).</summary>
public interface IAppointmentResponsibleStore
{
    Task<IReadOnlyList<AppointmentResponsible>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Ligne suivie pour modification, ou <c>null</c>.</summary>
    Task<AppointmentResponsible?> FindForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    void Add(AppointmentResponsible responsible);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public enum ResponsibleChangeStatus
{
    Enabled,
    Disabled,
    Unchanged,
    UnknownAccount,
}

public sealed record ResponsibleChangeResult(ResponsibleChangeStatus Status, Guid? UserId = null, bool AccountActive = false);

public sealed record ResponsibleListing(string UserName, string DisplayName, bool IsActive, bool IsEligible);

/// <summary>
/// Gestion EXPLICITE de l'éligibilité à être responsable de rendez-vous (D-03-14, D-03-17), indépendante des
/// permissions. Appelée exclusivement par la commande locale <c>MMV.Maintenance responsibles</c> (aucun endpoint
/// HTTP). Retrait = éligibilité désactivée, jamais de suppression ; les rendez-vous existants du collaborateur
/// restent affichés avec son nom.
/// </summary>
public sealed class ResponsibleAdministrationService(
    IIdentityStore identity, IAppointmentResponsibleStore responsibles, TimeProvider clock)
{
    public async Task<IReadOnlyList<ResponsibleListing>> ListAsync(CancellationToken cancellationToken)
    {
        var eligible = (await responsibles.ListAsync(cancellationToken)).ToDictionary(r => r.UserId, r => r.IsEligible);
        var users = await identity.ListUsersAsync(cancellationToken);
        return [.. users.Select(u => new ResponsibleListing(u.UserName, u.DisplayName, u.IsActive, eligible.GetValueOrDefault(u.Id)))];
    }

    public async Task<ResponsibleChangeResult> SetEligibleAsync(string userName, bool eligible, CancellationToken cancellationToken)
    {
        var user = await identity.FindUserByNormalizedNameAsync(UserNames.Normalize(userName), cancellationToken);
        if (user is null)
        {
            return new ResponsibleChangeResult(ResponsibleChangeStatus.UnknownAccount);
        }

        var now = clock.GetUtcNow();
        var current = await responsibles.FindForUpdateAsync(user.Id, cancellationToken);
        bool changed;
        if (current is null)
        {
            changed = eligible;
            if (eligible)
            {
                responsibles.Add(AppointmentResponsible.Enable(user.Id, now));
            }
        }
        else
        {
            changed = current.SetEligible(eligible, now);
        }

        if (!changed)
        {
            return new ResponsibleChangeResult(ResponsibleChangeStatus.Unchanged, user.Id, user.IsActive);
        }

        await responsibles.SaveChangesAsync(cancellationToken);
        return new ResponsibleChangeResult(
            eligible ? ResponsibleChangeStatus.Enabled : ResponsibleChangeStatus.Disabled, user.Id, user.IsActive);
    }
}
