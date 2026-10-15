namespace MMV.Domain.Identity;

/// <summary>
/// Session serveur révocable. Seul le hachage SHA-256 du jeton opaque est conservé : le jeton
/// lui-même n'existe que dans la réponse de connexion et la mémoire volatile du client.
/// </summary>
public sealed class UserSession
{
    public const int TokenHashLength = 32;
    public const int RevocationReasonMaxLength = 32;

    private UserSession()
    {
        TokenHash = [];
    }

    public UserSession(Guid id, Guid userId, byte[] tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        if (tokenHash.Length != TokenHashLength)
        {
            throw new ArgumentException("Hachage de jeton SHA-256 attendu (32 octets).", nameof(tokenHash));
        }

        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public byte[] TokenHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? RevocationReason { get; private set; }

    /// <summary>Valide si non révoquée, avant l'échéance absolue et sans inactivité prolongée.</summary>
    public bool IsValidAt(DateTimeOffset now, TimeSpan idleTimeout) =>
        RevokedAtUtc is null && now < ExpiresAtUtc && now - LastSeenAtUtc < idleTimeout;

    /// <summary>Glissement de l'inactivité ; renvoie vrai si une écriture est nécessaire.</summary>
    public bool Touch(DateTimeOffset now, TimeSpan threshold)
    {
        if (now - LastSeenAtUtc < threshold)
        {
            return false;
        }

        LastSeenAtUtc = now;
        return true;
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = now;
        RevocationReason = reason.Length <= RevocationReasonMaxLength ? reason : reason[..RevocationReasonMaxLength];
    }
}

/// <summary>Raisons de révocation enregistrées (technique, sans donnée personnelle).</summary>
public static class SessionRevocationReasons
{
    public const string Logout = "logout";
    public const string IdleTimeout = "idle-timeout";
    public const string AccountDisabled = "account-disabled";
}

/// <summary>
/// Trace unique du bootstrap administrateur : clé primaire constante (1) garantissant, au niveau
/// de PostgreSQL, qu'un second bootstrap échoue même en cas d'exécutions concurrentes.
/// </summary>
public sealed class BootstrapRecord
{
    public const short SingletonId = 1;

    private BootstrapRecord()
    {
    }

    public BootstrapRecord(Guid administratorId, DateTimeOffset completedAtUtc)
    {
        Id = SingletonId;
        AdministratorId = administratorId;
        CompletedAtUtc = completedAtUtc;
    }

    public short Id { get; private set; }

    public Guid AdministratorId { get; private set; }

    public DateTimeOffset CompletedAtUtc { get; private set; }
}
