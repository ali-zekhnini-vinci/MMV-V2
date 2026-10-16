using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Identity;
using MMV.Domain.Identity;
using MMV.Infrastructure;

namespace MMV.Api.Tests.Identity;

/// <summary>
/// Double NON PERSISTANT de <see cref="IIdentityStore"/> (listes en mémoire du processus de test).
/// Ce n'est pas un fournisseur EF (aucun EF InMemory) : la persistance réelle est prouvée sur
/// PostgreSQL par tests/MMV.PostgreSql.Tests.
/// </summary>
internal sealed class InMemoryIdentityStore : IIdentityStore
{
    private readonly Lock _gate = new();

    public List<UserAccount> Users { get; } = [];

    public List<UserSession> Sessions { get; } = [];

    public BootstrapRecord? Bootstrap { get; private set; }

    public int SaveCount { get; private set; }

    public Task<UserAccount?> FindUserByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(Users.SingleOrDefault(u => u.NormalizedUserName == normalizedUserName));
        }
    }

    public Task<(UserSession Session, UserAccount User)?> FindSessionByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var session = Sessions.SingleOrDefault(s => s.TokenHash.AsSpan().SequenceEqual(tokenHash));
            return Task.FromResult<(UserSession, UserAccount)?>(
                session is null ? null : (session, Users.Single(u => u.Id == session.UserId)));
        }
    }

    public Task<UserSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(Sessions.SingleOrDefault(s => s.Id == sessionId));
        }
    }

    public void AddSession(UserSession session)
    {
        lock (_gate)
        {
            Sessions.Add(session);
        }
    }

    public Task<IReadOnlyList<UserAccount>> ListUsersAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<UserAccount>>([.. Users.OrderBy(u => u.NormalizedUserName, StringComparer.Ordinal)]);
        }
    }

    public Task<bool> IsBootstrapCompletedAsync(CancellationToken cancellationToken) => Task.FromResult(Bootstrap is not null);

    public Task<bool> TryCreateInitialAdministratorAsync(UserAccount administrator, BootstrapRecord record, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (Bootstrap is not null)
            {
                return Task.FromResult(false);
            }

            Bootstrap = record;
            Users.Add(administrator);
            return Task.FromResult(true);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    /// <summary>Ajoute un compte avec le VRAI hachage Identity.</summary>
    public UserAccount AddUser(IPasswordHashing hashing, string userName, string password, params string[] permissions)
    {
        var user = new UserAccount(Guid.CreateVersion7(), userName, $"{userName} (test)", DateTimeOffset.UtcNow);
        user.SetPasswordHash(hashing.Hash(user, password));
        foreach (var permission in permissions)
        {
            user.Grant(permission);
        }

        lock (_gate)
        {
            Users.Add(user);
        }

        return user;
    }
}

/// <summary>Horloge manuelle (aucun paquet supplémentaire).</summary>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan delta) => Now = Now.Add(delta);
}

internal static class RealHashing
{
    /// <summary>Hachage réel (PasswordHasher d'ASP.NET Core Identity) résolu via MMV.Infrastructure.</summary>
    public static IPasswordHashing Create()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider()
            .GetRequiredService<IPasswordHashing>();
    }
}
