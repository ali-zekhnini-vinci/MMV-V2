using System.Globalization;
using System.Reflection;
using MMV.Application.Clients;
using MMV.Domain.Clients;
using MMV.Infrastructure.Clients;

namespace MMV.Api.Tests.Clients;

/// <summary>
/// Double NON PERSISTANT de <see cref="IClientStore"/> (listes en mémoire du processus de test). Il imite ce que
/// PostgreSQL fait réellement — référence attribuée par séquence à l'insertion, nouvelle version (xmin) à chaque
/// écriture — sans être un fournisseur EF. La persistance réelle, les contraintes et le conflit de concurrence
/// au niveau SQL sont prouvés par tests/MMV.PostgreSql.Tests (étape ClientsWrite).
/// </summary>
/// <param name="clock">Horloge des clients semés ; celle du service testé, pour que création et modification soient
/// datées sur la même échelle (sinon le résultat dépend de l'heure réelle d'exécution).</param>
internal sealed class InMemoryClientStore(TimeProvider? clock = null) : IClientStore
{
    private static readonly PropertyInfo ReferenceProperty = typeof(Client).GetProperty(nameof(Client.Reference))!;
    private static readonly PropertyInfo VersionProperty = typeof(Client).GetProperty(nameof(Client.Version))!;

    private readonly Lock _gate = new();
    private readonly HashSet<Client> _pending = [];
    private long _sequence;
    private uint _transaction = 1000;

    public List<Client> Clients { get; } = [];

    /// <summary>Simule une écriture concurrente survenue entre la lecture et l'enregistrement.</summary>
    public bool FailNextSave { get; set; }

    public int SaveCount { get; private set; }

    public Task<ClientPage> ListAsync(ClientListQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IEnumerable<Client> clients = query.Status switch
            {
                ClientStatusFilter.Active => Clients.Where(c => !c.IsArchived),
                ClientStatusFilter.Archived => Clients.Where(c => c.IsArchived),
                _ => Clients,
            };

            // Même construction des motifs que ClientStore ; LIKE '%x%' imité par Contains.
            foreach (var pattern in ClientStore.SearchPatterns(query.Search))
            {
                var needle = pattern[1..^1].Replace("\\%", "%", StringComparison.Ordinal)
                    .Replace("\\_", "_", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
                clients = clients.Where(c => c.SearchText.Contains(needle, StringComparison.Ordinal));
            }

            var ordered = (query.Sort, query.Descending) switch
            {
                (ClientSortField.CreatedAt, false) => clients.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id),
                (ClientSortField.CreatedAt, true) => clients.OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id),
                // PostgreSQL : NULL en dernier en ordre croissant, en premier en ordre décroissant.
                (ClientSortField.City, false) => clients.OrderBy(c => c.City is null).ThenBy(c => c.City, StringComparer.Ordinal).ThenBy(c => c.Id),
                (ClientSortField.City, true) => clients.OrderBy(c => c.City is not null).ThenByDescending(c => c.City, StringComparer.Ordinal).ThenByDescending(c => c.Id),
                (_, false) => clients.OrderBy(c => c.LastName, StringComparer.Ordinal).ThenBy(c => c.FirstName, StringComparer.Ordinal).ThenBy(c => c.Id),
                (_, true) => clients.OrderByDescending(c => c.LastName, StringComparer.Ordinal).ThenByDescending(c => c.FirstName, StringComparer.Ordinal).ThenByDescending(c => c.Id),
            };

            var all = ordered.ToList();
            var items = all.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
            return Task.FromResult(new ClientPage(items, query.Page, query.PageSize, all.Count));
        }
    }

    public Task<Client?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(Clients.SingleOrDefault(c => c.Id == id));
        }
    }

    public Task<Client?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var client = Clients.SingleOrDefault(c => c.Id == id);
            if (client is not null)
            {
                _pending.Add(client);
            }

            return Task.FromResult(client);
        }
    }

    public void Add(Client client)
    {
        lock (_gate)
        {
            _pending.Add(client);
        }
    }

    public Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            SaveCount++;
            if (FailNextSave)
            {
                FailNextSave = false;
                _pending.Clear();
                return Task.FromResult(false);
            }

            foreach (var client in _pending)
            {
                if (client.Reference.Length == 0)
                {
                    ReferenceProperty.SetValue(client, string.Create(CultureInfo.InvariantCulture, $"CLI-{++_sequence:000000}"));
                    Clients.Add(client);
                }

                VersionProperty.SetValue(client, ++_transaction);
            }

            _pending.Clear();
            return Task.FromResult(true);
        }
    }

    /// <summary>Client fictif enregistré (données de test uniquement, jamais en base de développement).</summary>
    public Client Seed(string firstName, string lastName, string? city = null, string? email = null, string? phone = null, bool archived = false)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var client = Client.Create(
            Guid.CreateVersion7(), new ClientDetails(firstName, lastName, null, phone, email, null, city, null, null), Guid.CreateVersion7(), now);
        if (archived)
        {
            client.Archive(client.CreatedByUserId, now);
        }

        Add(client);
        SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
        return client;
    }
}
