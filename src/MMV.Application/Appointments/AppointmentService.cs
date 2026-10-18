using MMV.Domain.Appointments;

namespace MMV.Application.Appointments;

/// <summary>
/// Rendez-vous accompagné des seules données client et collaborateur affichées (minimisation, D-03-17), dont
/// l'auteur de la dernière modification (O-03-23, 03-B3 : correction des notes et du motif tracée par auteur et date).
/// </summary>
public sealed record AppointmentView(
    Appointment Appointment,
    string ClientReference,
    string ClientFirstName,
    string ClientLastName,
    bool ClientArchived,
    string? ResponsibleDisplayName,
    string? CancelledByDisplayName,
    string UpdatedByDisplayName);

/// <summary>Collaborateur proposé comme responsable : identifiant et nom affiché seulement.</summary>
public sealed record ResponsibleOption(Guid Id, string DisplayName);

public enum ClientLockState
{
    NotFound,
    Active,
    Archived,
}

/// <summary>
/// Ressource d'agenda verrouillée pendant une écriture (D-03-15) : l'agenda d'un collaborateur, ou le pool de
/// capacité d'accueil du magasin. Les verrous sont toujours pris dans l'ordre croissant de <see cref="Key"/>
/// (ordre stable, aucun interblocage entre deux écritures qui visent les mêmes ressources).
/// </summary>
public readonly record struct AppointmentLock(int Key)
{
    /// <summary>Pool de capacité d'accueil (une installation = un magasin en V1).</summary>
    public static AppointmentLock Reception { get; } = new(0);

    /// <summary>
    /// Clé dérivée de l'identifiant du collaborateur (repliement des 16 octets). Deux collaborateurs de même clé
    /// partagent un verrou : sérialisation inutile mais sans risque, jamais une absence de verrou.
    /// </summary>
    public static AppointmentLock Responsible(Guid userId)
    {
        Span<byte> bytes = stackalloc byte[16];
        userId.TryWriteBytes(bytes);
        var key = BitConverter.ToInt32(bytes[..4]) ^ BitConverter.ToInt32(bytes[4..8])
                  ^ BitConverter.ToInt32(bytes[8..12]) ^ BitConverter.ToInt32(bytes[12..]);
        return new AppointmentLock(key);
    }

    public static AppointmentLock For(Guid? responsibleUserId) =>
        responsibleUserId is { } id ? Responsible(id) : Reception;
}

/// <summary>Lectures de l'agenda (non suivies) et ouverture d'une écriture transactionnelle.</summary>
public interface IAppointmentStore
{
    /// <summary>Rendez-vous qui recoupent [<paramref name="fromUtc"/>, <paramref name="toUtc"/>[, triés par début puis identifiant.</summary>
    Task<IReadOnlyList<AppointmentView>> ListAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, bool includeCancelled, CancellationToken cancellationToken);

    Task<AppointmentView?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Rendez-vous d'un client (03-D1) : page triée par début puis identifiant, et effectifs par catégorie.</summary>
    Task<ClientAppointmentPage> ListForClientAsync(ClientAppointmentQuery query, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Collaborateurs actifs ET explicitement éligibles, triés par nom affiché.</summary>
    Task<IReadOnlyList<ResponsibleOption>> ListEligibleResponsiblesAsync(CancellationToken cancellationToken);

    /// <summary>Transaction PostgreSQL <c>READ COMMITTED</c> (ou transaction englobante si elle existe déjà).</summary>
    Task<IAppointmentWriteSession> BeginWriteAsync(CancellationToken cancellationToken);
}

/// <summary>Écriture transactionnelle : verrous, contrôles et enregistrement dans UNE transaction.</summary>
public interface IAppointmentWriteSession : IAsyncDisposable
{
    /// <summary>Rendez-vous suivi pour modification, ou <c>null</c>.</summary>
    Task<Appointment?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Verrous transactionnels (libérés à la fin de la transaction), pris par clé croissante, sans doublon.</summary>
    Task LockAsync(IReadOnlyCollection<AppointmentLock> locks, CancellationToken cancellationToken);

    /// <summary>
    /// État du client, ligne verrouillée en partage jusqu'à la fin de la transaction : un archivage concurrent
    /// attend, il ne peut pas s'intercaler entre ce contrôle et l'enregistrement.
    /// </summary>
    Task<ClientLockState> LockClientAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>Compte existant, actif ET explicitement éligible (D-03-14).</summary>
    Task<bool> IsEligibleResponsibleAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Un autre rendez-vous PLANIFIÉ du collaborateur recoupe-t-il [début, fin[ ?</summary>
    Task<bool> HasResponsibleOverlapAsync(
        Guid responsibleUserId, DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Intervalles des autres rendez-vous PLANIFIÉS non attribués qui recoupent [début, fin[.</summary>
    Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> ListUnassignedOverlapsAsync(
        DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken);

    void Add(Appointment appointment);

    /// <summary>
    /// Enregistre puis valide la transaction ; <c>false</c> (rien n'est écrit) si la version lue n'est plus la
    /// version courante en base (écriture concurrente).
    /// </summary>
    Task<bool> CommitAsync(CancellationToken cancellationToken);
}

public enum AppointmentWriteStatus
{
    Success,
    NotConfigured,
    NotFound,
    ValidationFailed,
    VersionConflict,
    AppointmentCancelled,
    AppointmentNotCancelled,
    AppointmentPast,
    ClientArchived,
    SlotConflict,
    ReceptionCapacityFull,
    ReceptionNotConfigured,
    ResponsibleUnavailable,
}

public sealed record AppointmentWriteResult(
    AppointmentWriteStatus Status, AppointmentView? Appointment = null, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static AppointmentWriteResult Invalid(string field, string code) =>
        new(AppointmentWriteStatus.ValidationFailed, Errors: new Dictionary<string, string[]> { [field] = [code] });
}

/// <summary>
/// Cas d'usage Rendez-vous (D-03-13 à D-03-18, approuvées le 17/10/2026). Toute écriture qui réserve un créneau
/// (création, déplacement, changement de responsable, rétablissement) s'exécute dans une transaction qui verrouille
/// d'abord la ressource visée — agenda du collaborateur ou pool d'accueil, clés croissantes — puis le client en
/// partage, contrôle chevauchements ou capacité, et enregistre : deux postes simultanés ne peuvent pas réserver le
/// même créneau. Aucune suppression physique ; toute écriture porte la version lue (jamais d'écrasement silencieux).
/// </summary>
public sealed class AppointmentService(IAppointmentStore store, AppointmentSettings settings, TimeProvider clock)
{
    public const string InPast = "in_past";
    public const string NotFoundCode = "not_found";
    public const string NotEligible = "not_eligible";

    public const int MaxRangeDays = 31;

    public AppointmentSettings Settings => settings;

    public Task<IReadOnlyList<AppointmentView>> ListAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, bool includeCancelled, CancellationToken cancellationToken) =>
        store.ListAsync(fromUtc, toUtc, includeCancelled, cancellationToken);

    public Task<AppointmentView?> GetAsync(Guid id, CancellationToken cancellationToken) => store.GetAsync(id, cancellationToken);

    /// <summary>Historique des rendez-vous d'un client (03-D1) ; « à venir » et « passés » selon l'horloge du serveur.</summary>
    public Task<ClientAppointmentPage> ListForClientAsync(ClientAppointmentQuery query, CancellationToken cancellationToken) =>
        store.ListForClientAsync(query, Now(), cancellationToken);

    public Task<IReadOnlyList<ResponsibleOption>> ListResponsiblesAsync(CancellationToken cancellationToken) =>
        store.ListEligibleResponsiblesAsync(cancellationToken);

    public async Task<AppointmentWriteResult> CreateAsync(AppointmentInput input, Guid userId, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.NotConfigured);
        }

        var now = Now();
        var validation = AppointmentInputValidator.Validate(input, settings);
        if (!validation.IsValid)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.ValidationFailed, Errors: validation.Errors);
        }

        var value = validation.Value!;
        if (value.StartsAtUtc < now)
        {
            return AppointmentWriteResult.Invalid("startsAtUtc", InPast);
        }

        await using var session = await store.BeginWriteAsync(cancellationToken);
        await session.LockAsync([AppointmentLock.For(value.ResponsibleUserId)], cancellationToken);

        var refusal = await CheckClientAndResponsibleAsync(session, value, checkResponsible: true, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        var slot = await CheckAvailabilityAsync(session, value.ResponsibleUserId, value.StartsAtUtc, value.EndsAtUtc, null, cancellationToken);
        if (slot is not null)
        {
            return new AppointmentWriteResult(slot.Value);
        }

        var appointment = Appointment.Create(Guid.CreateVersion7(now), value.ToDetails(), userId, now);
        session.Add(appointment);
        await session.CommitAsync(cancellationToken);
        return await SuccessAsync(appointment.Id, cancellationToken);
    }

    /// <summary>
    /// Modification ou déplacement (corps complet). Le créneau est revérifié — passé, éligibilité du responsable,
    /// chevauchement ou capacité, sous verrou — dès que le début, la durée ou le responsable changent, y compris quand
    /// le responsable reste le même (B2-REV-01, 03-B3 : un collaborateur désactivé ou retiré ne reçoit aucun nouveau
    /// créneau) ; une modification qui ne touche que le motif ou les notes ne réserve rien de nouveau. Un rendez-vous
    /// commencé garde ses informations de réservation — client, créneau, responsable — (B2-REV-02 et O-03-23,
    /// règles retenues par le propriétaire en 03-B3) : seuls le motif et les notes restent corrigeables.
    /// </summary>
    public async Task<AppointmentWriteResult> UpdateAsync(
        Guid id, AppointmentInput input, uint version, Guid userId, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.NotConfigured);
        }

        await using var session = await store.BeginWriteAsync(cancellationToken);
        var appointment = await session.FindForUpdateAsync(id, cancellationToken);
        if (appointment is null)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.NotFound);
        }

        if (appointment.Version != version)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.VersionConflict);
        }

        if (appointment.IsCancelled)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.AppointmentCancelled);
        }

        var now = Now();
        var validation = AppointmentInputValidator.Validate(input, settings);
        if (!validation.IsValid)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.ValidationFailed, Errors: validation.Errors);
        }

        var value = validation.Value!;
        var slotChanged = value.StartsAtUtc != appointment.StartsAtUtc || value.EndsAtUtc != appointment.EndsAtUtc;
        var responsibleChanged = value.ResponsibleUserId != appointment.ResponsibleUserId;
        var rebooks = slotChanged || responsibleChanged;
        if ((rebooks || value.ClientId != appointment.ClientId) && appointment.StartsAtUtc < now)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.AppointmentPast);
        }

        if (slotChanged && value.StartsAtUtc < now)
        {
            return AppointmentWriteResult.Invalid("startsAtUtc", InPast);
        }

        if (rebooks)
        {
            await session.LockAsync([AppointmentLock.For(value.ResponsibleUserId)], cancellationToken);
        }

        // Les rendez-vous d'un client archivé restent consultables et annulables, jamais modifiables (D-03-16).
        if (value.ClientId != appointment.ClientId
            && await session.LockClientAsync(appointment.ClientId, cancellationToken) == ClientLockState.Archived)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.ClientArchived);
        }

        var refusal = await CheckClientAndResponsibleAsync(session, value, checkResponsible: rebooks, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        if (rebooks)
        {
            var slot = await CheckAvailabilityAsync(session, value.ResponsibleUserId, value.StartsAtUtc, value.EndsAtUtc, appointment.Id, cancellationToken);
            if (slot is not null)
            {
                return new AppointmentWriteResult(slot.Value);
            }
        }

        appointment.Update(value.ToDetails(), userId, now);

        return await session.CommitAsync(cancellationToken)
            ? await SuccessAsync(appointment.Id, cancellationToken)
            : new AppointmentWriteResult(AppointmentWriteStatus.VersionConflict);
    }

    /// <summary>Annulation logique (D-03-16) : libère le créneau ou la capacité ; permise pour un client archivé.</summary>
    public async Task<AppointmentWriteResult> CancelAsync(
        Guid id, uint version, string? reason, Guid userId, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.NotConfigured);
        }

        await using var session = await store.BeginWriteAsync(cancellationToken);
        var appointment = await session.FindForUpdateAsync(id, cancellationToken);
        if (appointment is null)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.NotFound);
        }

        if (appointment.Version != version)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.VersionConflict);
        }

        if (appointment.IsCancelled)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.AppointmentCancelled);
        }

        if (!AppointmentInputValidator.TryNormalizeCancellationReason(reason, out var normalized))
        {
            return AppointmentWriteResult.Invalid("reason", AppointmentInputValidator.TooLong);
        }

        appointment.Cancel(normalized, userId, Now());
        return await session.CommitAsync(cancellationToken)
            ? await SuccessAsync(appointment.Id, cancellationToken)
            : new AppointmentWriteResult(AppointmentWriteStatus.VersionConflict);
    }

    /// <summary>
    /// Rétablissement (D-03-16) : nouvelle réservation du même créneau, refusée si le client est archivé, si le
    /// créneau a commencé (création dans le passé interdite, D-03-13), si le collaborateur n'est plus actif et
    /// éligible, ou si le créneau ou la capacité d'accueil ne sont plus disponibles (D-03-15).
    /// </summary>
    public async Task<AppointmentWriteResult> RestoreAsync(Guid id, uint version, Guid userId, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.NotConfigured);
        }

        await using var session = await store.BeginWriteAsync(cancellationToken);
        var appointment = await session.FindForUpdateAsync(id, cancellationToken);
        if (appointment is null)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.NotFound);
        }

        if (appointment.Version != version)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.VersionConflict);
        }

        if (!appointment.IsCancelled)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.AppointmentNotCancelled);
        }

        var now = Now();
        if (appointment.StartsAtUtc < now)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.AppointmentPast);
        }

        if (appointment.ResponsibleUserId is null && settings.ReceptionCapacity is null)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.ReceptionNotConfigured);
        }

        await session.LockAsync([AppointmentLock.For(appointment.ResponsibleUserId)], cancellationToken);
        if (await session.LockClientAsync(appointment.ClientId, cancellationToken) != ClientLockState.Active)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.ClientArchived);
        }

        if (appointment.ResponsibleUserId is { } responsible
            && !await session.IsEligibleResponsibleAsync(responsible, cancellationToken))
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.ResponsibleUnavailable);
        }

        var slot = await CheckAvailabilityAsync(
            session, appointment.ResponsibleUserId, appointment.StartsAtUtc, appointment.EndsAtUtc, appointment.Id, cancellationToken);
        if (slot is not null)
        {
            return new AppointmentWriteResult(slot.Value);
        }

        appointment.Restore(userId, now);
        return await session.CommitAsync(cancellationToken)
            ? await SuccessAsync(appointment.Id, cancellationToken)
            : new AppointmentWriteResult(AppointmentWriteStatus.VersionConflict);
    }

    /// <summary>Client à verrouiller : introuvable → 422 ; archivé → 409. Responsable inactif ou non éligible → 422.</summary>
    private static async Task<AppointmentWriteResult?> CheckClientAndResponsibleAsync(
        IAppointmentWriteSession session, ValidatedAppointment value, bool checkResponsible, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var client = await session.LockClientAsync(value.ClientId, cancellationToken);
        if (client == ClientLockState.NotFound)
        {
            errors["clientId"] = [NotFoundCode];
        }

        if (checkResponsible && value.ResponsibleUserId is { } responsible
            && !await session.IsEligibleResponsibleAsync(responsible, cancellationToken))
        {
            errors["responsibleUserId"] = [NotEligible];
        }

        if (errors.Count > 0)
        {
            return new AppointmentWriteResult(AppointmentWriteStatus.ValidationFailed, Errors: errors);
        }

        return client == ClientLockState.Archived ? new AppointmentWriteResult(AppointmentWriteStatus.ClientArchived) : null;
    }

    /// <summary>
    /// Sous verrou de la ressource : chevauchement avec un autre rendez-vous planifié du collaborateur (bout à bout
    /// admis), ou occupation de l'accueil à CHAQUE instant du créneau (<see cref="ReceptionOccupancy"/>).
    /// </summary>
    private async Task<AppointmentWriteStatus?> CheckAvailabilityAsync(
        IAppointmentWriteSession session, Guid? responsibleUserId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (responsibleUserId is { } responsible)
        {
            return await session.HasResponsibleOverlapAsync(responsible, start, end, excludeId, cancellationToken)
                ? AppointmentWriteStatus.SlotConflict
                : null;
        }

        var capacity = settings.ReceptionCapacity ?? 0;
        var others = await session.ListUnassignedOverlapsAsync(start, end, excludeId, cancellationToken);
        return ReceptionOccupancy.MaxConcurrent(others, start, end) + 1 > capacity
            ? AppointmentWriteStatus.ReceptionCapacityFull
            : null;
    }

    private async Task<AppointmentWriteResult> SuccessAsync(Guid id, CancellationToken cancellationToken) =>
        await store.GetAsync(id, cancellationToken) is { } view
            ? new AppointmentWriteResult(AppointmentWriteStatus.Success, view)
            : new AppointmentWriteResult(AppointmentWriteStatus.NotFound);

    // Précision PostgreSQL (microseconde) : la valeur renvoyée est identique à celle relue ensuite.
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }
}
