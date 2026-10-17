using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MMV.Application.Appointments;
using MMV.Domain.Appointments;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure.Appointments;

internal sealed class AppointmentStore(MmvDbContext context) : IAppointmentStore
{
    public async Task<IReadOnlyList<AppointmentView>> ListAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, bool includeCancelled, CancellationToken cancellationToken) =>
        await Views(context.Appointments.AsNoTracking()
                .Where(a => a.StartsAtUtc < toUtc && a.EndsAtUtc > fromUtc && (includeCancelled || a.Status == Appointment.StatusPlanned)))
            .OrderBy(v => v.Appointment.StartsAtUtc).ThenBy(v => v.Appointment.Id)
            .ToListAsync(cancellationToken);

    public Task<AppointmentView?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Views(context.Appointments.AsNoTracking().Where(a => a.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ResponsibleOption>> ListEligibleResponsiblesAsync(CancellationToken cancellationToken) =>
        await (from user in context.Users.AsNoTracking()
               join responsible in context.AppointmentResponsibles.AsNoTracking() on user.Id equals responsible.UserId
               where user.IsActive && responsible.IsEligible
               orderby user.DisplayName, user.Id
               select new ResponsibleOption(user.Id, user.DisplayName))
            .ToListAsync(cancellationToken);

    public async Task<IAppointmentWriteSession> BeginWriteAsync(CancellationToken cancellationToken)
    {
        // Transaction englobante (tests PostgreSQL annulés au ROLLBACK) : réutilisée, jamais validée ici.
        var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        return new WriteSession(context, transaction);
    }

    /// <summary>
    /// Ordre de prise des verrous : clés croissantes, sans doublon. Ordre stable pour toutes les écritures : deux
    /// transactions qui visent les mêmes ressources les demandent dans le même ordre et ne s'interbloquent pas.
    /// </summary>
    internal static IReadOnlyList<int> LockSequence(IReadOnlyCollection<AppointmentLock> locks) =>
        [.. locks.Select(l => l.Key).Distinct().Order()];

    /// <summary>Client (référence, nom, archivage) et noms affichés des collaborateurs : jointures externes.</summary>
    private IQueryable<AppointmentView> Views(IQueryable<Appointment> appointments) =>
        from appointment in appointments
        join client in context.Clients on appointment.ClientId equals client.Id
        join responsible in context.Users on appointment.ResponsibleUserId equals (Guid?)responsible.Id into responsibles
        from responsible in responsibles.DefaultIfEmpty()
        join canceller in context.Users on appointment.CancelledByUserId equals (Guid?)canceller.Id into cancellers
        from canceller in cancellers.DefaultIfEmpty()
        select new AppointmentView(
            appointment,
            client.Reference,
            client.FirstName,
            client.LastName,
            client.ArchivedAtUtc != null,
            responsible == null ? null : responsible.DisplayName,
            canceller == null ? null : canceller.DisplayName);

    private sealed class WriteSession(MmvDbContext context, IDbContextTransaction? transaction) : IAppointmentWriteSession
    {
        /// <summary>Espace de clés des verrous consultatifs MMV Rendez-vous (« MMVR ») : forme à deux entiers.</summary>
        private const int LockNamespace = 0x4D4D5652;

        public Task<Appointment?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            context.Appointments.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        public async Task LockAsync(IReadOnlyCollection<AppointmentLock> locks, CancellationToken cancellationToken)
        {
            foreach (var key in LockSequence(locks))
            {
                await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LockNamespace}, {key})", cancellationToken);
            }
        }

        public async Task<ClientLockState> LockClientAsync(Guid clientId, CancellationToken cancellationToken)
        {
            // FOR SHARE : un archivage concurrent (UPDATE de la ligne) attend la fin de cette transaction.
            var archived = await context.Database
                .SqlQuery<bool>($"SELECT (archived_at_utc IS NOT NULL) AS \"Value\" FROM clients.clients WHERE id = {clientId} FOR SHARE")
                .ToListAsync(cancellationToken);
            return archived switch
            {
                [] => ClientLockState.NotFound,
                [true] => ClientLockState.Archived,
                _ => ClientLockState.Active,
            };
        }

        public Task<bool> IsEligibleResponsibleAsync(Guid userId, CancellationToken cancellationToken) =>
            context.Users.AnyAsync(
                u => u.Id == userId && u.IsActive && context.AppointmentResponsibles.Any(r => r.UserId == userId && r.IsEligible),
                cancellationToken);

        public Task<bool> HasResponsibleOverlapAsync(
            Guid responsibleUserId, DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken) =>
            context.Appointments.AnyAsync(
                a => a.ResponsibleUserId == responsibleUserId && a.Status == Appointment.StatusPlanned
                     && a.StartsAtUtc < endUtc && startUtc < a.EndsAtUtc && (excludeId == null || a.Id != excludeId),
                cancellationToken);

        public async Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> ListUnassignedOverlapsAsync(
            DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken)
        {
            var rows = await context.Appointments.AsNoTracking()
                .Where(a => a.ResponsibleUserId == null && a.Status == Appointment.StatusPlanned
                            && a.StartsAtUtc < endUtc && startUtc < a.EndsAtUtc && (excludeId == null || a.Id != excludeId))
                .Select(a => new { a.StartsAtUtc, a.EndsAtUtc })
                .ToListAsync(cancellationToken);
            return [.. rows.Select(r => (r.StartsAtUtc, r.EndsAtUtc))];
        }

        public void Add(Appointment appointment) => context.Appointments.Add(appointment);

        public async Task<bool> CommitAsync(CancellationToken cancellationToken)
        {
            try
            {
                // UPDATE … WHERE id = @id AND xmin = @version_lue : aucune ligne touchée → conflit, rien n'est écrit.
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                context.ChangeTracker.Clear();
                return false;
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }

        // Transaction propre non validée (refus, erreur) : annulée à la libération, verrous relâchés.
        public async ValueTask DisposeAsync()
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}

internal sealed class AppointmentResponsibleStore(MmvDbContext context) : IAppointmentResponsibleStore
{
    public async Task<IReadOnlyList<AppointmentResponsible>> ListAsync(CancellationToken cancellationToken) =>
        await context.AppointmentResponsibles.AsNoTracking().ToListAsync(cancellationToken);

    public Task<AppointmentResponsible?> FindForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AppointmentResponsibles.SingleOrDefaultAsync(r => r.UserId == userId, cancellationToken);

    public void Add(AppointmentResponsible responsible) => context.AppointmentResponsibles.Add(responsible);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
