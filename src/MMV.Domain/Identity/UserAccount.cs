namespace MMV.Domain.Identity;

/// <summary>
/// Compte d'accès à MMV (identité technique). Aucune donnée métier : ni magasin, ni fonction,
/// ni coordonnées. Le mot de passe n'est connu que sous forme hachée (format ASP.NET Core Identity).
/// </summary>
public sealed class UserAccount
{
    public const int UserNameMaxLength = 64;
    public const int DisplayNameMaxLength = 128;

    private readonly List<PermissionGrant> _permissions = [];

    private UserAccount()
    {
        UserName = string.Empty;
        NormalizedUserName = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
    }

    public UserAccount(Guid id, string userName, string displayName, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserName = userName;
        NormalizedUserName = UserNames.Normalize(userName);
        DisplayName = displayName;
        PasswordHash = string.Empty;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string UserName { get; private set; }

    public string NormalizedUserName { get; private set; }

    public string DisplayName { get; private set; }

    public string PasswordHash { get; private set; }

    public bool IsActive { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockoutEndUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<PermissionGrant> Permissions => _permissions;

    public bool HasPermission(string permission) => _permissions.Exists(p => p.Permission == permission);

    public void Grant(string permission)
    {
        if (!Identity.Permissions.IsKnown(permission))
        {
            throw new ArgumentException($"Permission inconnue : '{permission}'.", nameof(permission));
        }

        if (!HasPermission(permission))
        {
            _permissions.Add(new PermissionGrant(Id, permission));
        }
    }

    public void Revoke(string permission) => _permissions.RemoveAll(p => p.Permission == permission);

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndUtc is { } end && end > now;

    /// <summary>Enregistre un échec ; au seuil, verrouille temporairement le compte et remet le compteur à zéro.</summary>
    public void RegisterFailedLogin(DateTimeOffset now, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxFailedAttempts)
        {
            LockoutEndUtc = now.Add(lockoutDuration);
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
