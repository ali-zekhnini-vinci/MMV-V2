using Microsoft.EntityFrameworkCore;
using MMV.Application.Clients;
using MMV.Domain.Clients;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure.Clients;

internal sealed class ClientStore(MmvDbContext context) : IClientStore
{
    private const string LikeEscape = "\\";

    public async Task<ClientPage> ListAsync(ClientListQuery query, CancellationToken cancellationToken)
    {
        var clients = context.Clients.AsNoTracking();
        clients = query.Status switch
        {
            ClientStatusFilter.Active => clients.Where(c => c.ArchivedAtUtc == null),
            ClientStatusFilter.Archived => clients.Where(c => c.ArchivedAtUtc != null),
            _ => clients,
        };

        foreach (var pattern in SearchPatterns(query.Search))
        {
            clients = clients.Where(c => EF.Functions.Like(c.SearchText, pattern, LikeEscape));
        }

        var totalCount = await clients.CountAsync(cancellationToken);

        // Clé finale « id » : ordre total, pagination stable.
        var ordered = (query.Sort, query.Descending) switch
        {
            (ClientSortField.CreatedAt, false) => clients.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id),
            (ClientSortField.CreatedAt, true) => clients.OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id),
            (ClientSortField.City, false) => clients.OrderBy(c => c.City).ThenBy(c => c.Id),
            (ClientSortField.City, true) => clients.OrderByDescending(c => c.City).ThenByDescending(c => c.Id),
            (_, false) => clients.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id),
            (_, true) => clients.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName).ThenByDescending(c => c.Id),
        };

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new ClientPage(items, query.Page, query.PageSize, totalCount);
    }

    public Task<Client?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Client?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Clients.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Client client) => context.Clients.Add(client);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // UPDATE … WHERE id = @id AND xmin = @version_lue : aucune ligne touchée → conflit, rien n'est écrit.
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>
    /// Motifs LIKE (tous exigés) : requête composée uniquement de chiffres et séparateurs téléphoniques →
    /// ses chiffres ; sinon chaque mot replié (sans accents, minuscules). Les jokers saisis sont échappés.
    /// </summary>
    internal static IReadOnlyList<string> SearchPatterns(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        var digits = ClientSearch.Digits(search);
        if (digits.Length >= ClientListQuery.SearchMinLength && search.Trim().All(c => char.IsAsciiDigit(c) || c is ' ' or '.' or '-' or '+'))
        {
            return [Contains(digits)];
        }

        return [.. ClientSearch.Fold(search).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Select(Contains)];
    }

    private static string Contains(string value) =>
        $"%{value.Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal)}%";
}
