using MMV.Domain.Identity;

namespace MMV.Application.Identity;

public sealed record AuthenticatedUser(Guid Id, string UserName, string DisplayName, IReadOnlyList<string> Permissions)
{
    public static AuthenticatedUser From(UserAccount user) => new(
        user.Id,
        user.UserName,
        user.DisplayName,
        [.. user.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal)]);
}

public sealed record AuthenticatedSession(Guid SessionId, DateTimeOffset ExpiresAtUtc, AuthenticatedUser User);

public sealed record LoginSuccess(string AccessToken, AuthenticatedSession Session);

/// <summary>
/// Connexion, validation de session et déconnexion. Les refus de connexion sont NEUTRES : compte
/// inconnu, mot de passe erroné, compte désactivé ou verrouillé produisent le même résultat
/// (<c>null</c>), avec un coût de hachage comparable.
/// </summary>
public sealed class AuthService(IIdentityStore store, IPasswordHashing hashing, AuthOptions options, TimeProvider clock)
{
    public AuthOptions Options => options;

    public async Task<LoginSuccess?> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var user = await store.FindUserByNormalizedNameAsync(UserNames.Normalize(userName), cancellationToken);
        if (user is null)
        {
            hashing.VerifyAgainstDummy(password);
            return null;
        }

        var now = clock.GetUtcNow();
        var check = hashing.Verify(user, user.PasswordHash, password);

        if (!user.IsActive || user.IsLockedOut(now))
        {
            return null;
        }

        if (check == PasswordCheck.Failed)
        {
            user.RegisterFailedLogin(now, options.MaxFailedAttempts, options.LockoutDuration);
            await store.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.SetPasswordHash(hashing.Hash(user, password));
        }

        user.RegisterSuccessfulLogin();
        var token = SessionTokens.NewToken();
        var session = new UserSession(
            Guid.CreateVersion7(now),
            user.Id,
            SessionTokens.TryHash(token)!,
            now,
            now.Add(options.AbsoluteLifetime));
        store.AddSession(session);
        await store.SaveChangesAsync(cancellationToken);

        return new LoginSuccess(token, new AuthenticatedSession(session.Id, session.ExpiresAtUtc, AuthenticatedUser.From(user)));
    }

    /// <summary>Session valide pour ce jeton, ou <c>null</c> (absente, révoquée, expirée, compte désactivé).</summary>
    public async Task<AuthenticatedSession?> AuthenticateAsync(string token, CancellationToken cancellationToken)
    {
        var hash = SessionTokens.TryHash(token);
        if (hash is null)
        {
            return null;
        }

        var found = await store.FindSessionByTokenHashAsync(hash, cancellationToken);
        if (found is not { Session: var session, User: var user })
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (!user.IsActive)
        {
            session.Revoke(now, SessionRevocationReasons.AccountDisabled);
            await store.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!session.IsValidAt(now, options.IdleTimeout))
        {
            if (session.RevokedAtUtc is null && now < session.ExpiresAtUtc)
            {
                session.Revoke(now, SessionRevocationReasons.IdleTimeout);
                await store.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        if (session.Touch(now, options.TouchThreshold))
        {
            await store.SaveChangesAsync(cancellationToken);
        }

        return new AuthenticatedSession(session.Id, session.ExpiresAtUtc, AuthenticatedUser.From(user));
    }

    public async Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await store.FindSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.Revoke(clock.GetUtcNow(), SessionRevocationReasons.Logout);
        await store.SaveChangesAsync(cancellationToken);
    }
}
