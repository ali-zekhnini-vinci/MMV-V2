using System.Collections.Concurrent;
using System.Reflection;
using MMV.Application.Appointments;
using MMV.Domain.Appointments;
using MMV.Infrastructure.Appointments;

namespace MMV.Api.Tests.Appointments;

/// <summary>
/// Double NON PERSISTANT de <see cref="IAppointmentStore"/> (mémoire du processus de test). Il imite ce que la
/// transaction PostgreSQL fait réellement pour les garanties D-03-15 — verrous exclusifs par clé pris dans l'ordre
/// de <see cref="AppointmentStore.LockSequence"/> et tenus jusqu'à la fin de la session, ajouts invisibles des autres
/// sessions avant validation, nouvelle version (xmin) à chaque écriture — sans être un fournisseur EF. Les verrous
/// consultatifs, le <c>FOR SHARE</c> et les contraintes réels sont prouvés par tests/MMV.PostgreSql.Tests (étape
/// AppointmentsWrite, qui exige la migration 0004). Limite : un rendez-vous suivi est l'objet partagé (pas de copie
/// par session) ; les tests concurrents portent donc sur des créations, des rétablissements et des déplacements de
/// rendez-vous distincts.
/// </summary>
internal sealed class InMemoryAppointmentStore : IAppointmentStore
{
    /// <summary>Nom affiché d'un auteur absent des comptes doublés (les auteurs des tests ne sont pas des collaborateurs).</summary>
    public const string AuthorDisplayName = "Auteur de test";

    private static readonly PropertyInfo VersionProperty = typeof(Appointment).GetProperty(nameof(Appointment.Version))!;

    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();
    private uint _transaction = 5000;

    public List<Appointment> Appointments { get; } = [];

    public Dictionary<Guid, FakeClient> Clients { get; } = [];

    public Dictionary<Guid, FakeUser> Users { get; } = [];

    /// <summary>Clés effectivement verrouillées, dans l'ordre (toutes sessions confondues).</summary>
    public ConcurrentQueue<int> LockLog { get; } = new();

    /// <summary>Attente simulée après la prise des verrous (fenêtre de concurrence élargie).</summary>
    public TimeSpan DelayAfterLock { get; set; }

    /// <summary>Témoin négatif : sans verrou, deux postes simultanés passent les contrôles (la garantie vient du verrou).</summary>
    public bool IgnoreLocks { get; set; }

    /// <summary>
    /// Témoin négatif (03-B3) : chaque session attend, contrôles faits et juste avant d'enregistrer, que ce nombre de
    /// sessions y soit arrivé — fenêtre de concurrence garantie sans dépendre du minutage (avec <see cref="IgnoreLocks"/>
    /// seulement : sous verrou, la seconde session n'atteindrait jamais ce point).
    /// </summary>
    public int RendezvousBeforeCommit { get; set; }

    private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;

    /// <summary>Simule une écriture concurrente survenue entre la lecture et l'enregistrement.</summary>
    public bool FailNextCommit { get; set; }

    public int CommitCount { get; private set; }

    public sealed record FakeClient(string Reference, string FirstName, string LastName, bool Archived);

    public sealed record FakeUser(string DisplayName, bool Active, bool Eligible);

    public Guid AddClient(string firstName, string lastName, bool archived = false)
    {
        var id = Guid.CreateVersion7();
        lock (_gate)
        {
            Clients[id] = new FakeClient($"CLI-{Clients.Count + 1:000000}", firstName, lastName, archived);
        }

        return id;
    }

    public Guid AddUser(string displayName, bool active = true, bool eligible = true, Guid? id = null)
    {
        var userId = id ?? Guid.CreateVersion7();
        lock (_gate)
        {
            Users[userId] = new FakeUser(displayName, active, eligible);
        }

        return userId;
    }

    public void ArchiveClient(Guid id)
    {
        lock (_gate)
        {
            Clients[id] = Clients[id] with { Archived = true };
        }
    }

    public void SetUser(Guid id, bool active, bool eligible)
    {
        lock (_gate)
        {
            Users[id] = Users[id] with { Active = active, Eligible = eligible };
        }
    }

    /// <summary>Rendez-vous enregistré directement (données fictives), hors règles du service.</summary>
    public Appointment Seed(Guid clientId, Guid? responsible, DateTimeOffset start, int minutes, string reason = "conseil_devis", bool cancelled = false)
    {
        var appointment = Appointment.Create(
            Guid.CreateVersion7(), new AppointmentDetails(clientId, responsible, start, start.AddMinutes(minutes), reason, null), Guid.CreateVersion7(), start.AddDays(-10));
        if (cancelled)
        {
            appointment.Cancel(null, appointment.CreatedByUserId, start.AddDays(-9));
        }

        lock (_gate)
        {
            VersionProperty.SetValue(appointment, ++_transaction);
            Appointments.Add(appointment);
        }

        return appointment;
    }

    public Task<IReadOnlyList<AppointmentView>> ListAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, bool includeCancelled, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<AppointmentView> items =
            [
                .. Appointments
                    .Where(a => a.StartsAtUtc < toUtc && a.EndsAtUtc > fromUtc && (includeCancelled || !a.IsCancelled))
                    .OrderBy(a => a.StartsAtUtc).ThenBy(a => a.Id)
                    .Select(View),
            ];
            return Task.FromResult(items);
        }
    }

    public Task<AppointmentView?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var appointment = Appointments.SingleOrDefault(a => a.Id == id);
            return Task.FromResult(appointment is null ? null : View(appointment));
        }
    }

    public Task<ClientAppointmentPage> ListForClientAsync(ClientAppointmentQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            int Count(ClientAppointmentScope scope) => Appointments.Count(ClientAppointmentFilters.For(query.ClientId, scope, now).Compile());
            var counts = new ClientAppointmentCounts(Count(ClientAppointmentScope.Upcoming), Count(ClientAppointmentScope.Past), Count(ClientAppointmentScope.Cancelled));
            var filtered = Appointments.Where(ClientAppointmentFilters.For(query.ClientId, query.Scope, now).Compile()).ToList();
            var ordered = query.Descending
                ? filtered.OrderByDescending(a => a.StartsAtUtc).ThenByDescending(a => a.Id)
                : filtered.OrderBy(a => a.StartsAtUtc).ThenBy(a => a.Id);
            IReadOnlyList<AppointmentView> items = [.. ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(View)];
            return Task.FromResult(new ClientAppointmentPage(items, query.Page, query.PageSize, filtered.Count, counts));
        }
    }

    public Task<IReadOnlyList<ResponsibleOption>> ListEligibleResponsiblesAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<ResponsibleOption> items =
            [
                .. Users.Where(u => u.Value.Active && u.Value.Eligible)
                    .OrderBy(u => u.Value.DisplayName, StringComparer.Ordinal).ThenBy(u => u.Key)
                    .Select(u => new ResponsibleOption(u.Key, u.Value.DisplayName)),
            ];
            return Task.FromResult(items);
        }
    }

    public Task<IAppointmentWriteSession> BeginWriteAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IAppointmentWriteSession>(new Session(this));

    private AppointmentView View(Appointment a)
    {
        var client = Clients[a.ClientId];
        return new AppointmentView(
            a, client.Reference, client.FirstName, client.LastName, client.Archived,
            a.ResponsibleUserId is { } r ? Users[r].DisplayName : null,
            a.CancelledByUserId is { } c && Users.TryGetValue(c, out var canceller) ? canceller.DisplayName : null,
            Users.TryGetValue(a.UpdatedByUserId, out var updater) ? updater.DisplayName : AuthorDisplayName);
    }

    private sealed class Session(InMemoryAppointmentStore store) : IAppointmentWriteSession
    {
        private readonly List<SemaphoreSlim> _held = [];
        private readonly List<Appointment> _added = [];
        private readonly Dictionary<Appointment, uint> _tracked = [];

        public Task<Appointment?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            lock (store._gate)
            {
                var appointment = store.Appointments.SingleOrDefault(a => a.Id == id);
                if (appointment is not null)
                {
                    _tracked[appointment] = appointment.Version;
                }

                return Task.FromResult(appointment);
            }
        }

        public async Task LockAsync(IReadOnlyCollection<AppointmentLock> locks, CancellationToken cancellationToken)
        {
            if (!store.IgnoreLocks)
            {
                foreach (var key in AppointmentStore.LockSequence(locks))
                {
                    var semaphore = store._locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                    await semaphore.WaitAsync(cancellationToken);
                    _held.Add(semaphore);
                    store.LockLog.Enqueue(key);
                }
            }

            if (store.DelayAfterLock > TimeSpan.Zero)
            {
                await Task.Delay(store.DelayAfterLock, cancellationToken);
            }
        }

        public Task<ClientLockState> LockClientAsync(Guid clientId, CancellationToken cancellationToken)
        {
            lock (store._gate)
            {
                return Task.FromResult(!store.Clients.TryGetValue(clientId, out var client)
                    ? ClientLockState.NotFound
                    : client.Archived ? ClientLockState.Archived : ClientLockState.Active);
            }
        }

        public Task<bool> IsEligibleResponsibleAsync(Guid userId, CancellationToken cancellationToken)
        {
            lock (store._gate)
            {
                return Task.FromResult(store.Users.TryGetValue(userId, out var user) && user.Active && user.Eligible);
            }
        }

        public Task<bool> HasResponsibleOverlapAsync(
            Guid responsibleUserId, DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken)
        {
            lock (store._gate)
            {
                return Task.FromResult(store.Appointments.Any(a =>
                    a.ResponsibleUserId == responsibleUserId && !a.IsCancelled && a.Overlaps(startUtc, endUtc) && a.Id != excludeId));
            }
        }

        public Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> ListUnassignedOverlapsAsync(
            DateTimeOffset startUtc, DateTimeOffset endUtc, Guid? excludeId, CancellationToken cancellationToken)
        {
            lock (store._gate)
            {
                IReadOnlyList<(DateTimeOffset, DateTimeOffset)> items =
                [
                    .. store.Appointments
                        .Where(a => a.ResponsibleUserId is null && !a.IsCancelled && a.Overlaps(startUtc, endUtc) && a.Id != excludeId)
                        .Select(a => (a.StartsAtUtc, a.EndsAtUtc)),
                ];
                return Task.FromResult(items);
            }
        }

        public void Add(Appointment appointment) => _added.Add(appointment);

        public async Task<bool> CommitAsync(CancellationToken cancellationToken)
        {
            if (store.RendezvousBeforeCommit > 0)
            {
                if (Interlocked.Increment(ref store._arrivals) >= store.RendezvousBeforeCommit)
                {
                    store._allArrived.TrySetResult();
                }

                await store._allArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            lock (store._gate)
            {
                store.CommitCount++;
                // xmin : une autre session a validé une écriture de la même ligne depuis la lecture → conflit.
                if (store.FailNextCommit || _tracked.Any(t => t.Key.Version != t.Value))
                {
                    store.FailNextCommit = false;
                    return false;
                }

                foreach (var (appointment, _) in _tracked)
                {
                    VersionProperty.SetValue(appointment, ++store._transaction);
                }

                foreach (var appointment in _added)
                {
                    VersionProperty.SetValue(appointment, ++store._transaction);
                    store.Appointments.Add(appointment);
                }

                _added.Clear();
                _tracked.Clear();
                return true;
            }
        }

        public ValueTask DisposeAsync()
        {
            foreach (var semaphore in _held)
            {
                semaphore.Release();
            }

            _held.Clear();
            return ValueTask.CompletedTask;
        }
    }
}

internal static class AppointmentTestSettings
{
    public static AppointmentSettings Configured(string timeZone = "Europe/Brussels", int? capacity = 1) =>
        AppointmentSettings.From(new AppointmentsOptions
        {
            TimeZone = timeZone,
            DisplayStartTime = "08:00",
            DisplayEndTime = "20:00",
            HiddenWeekdays = [],
            ReceptionCapacity = capacity,
        });
}
