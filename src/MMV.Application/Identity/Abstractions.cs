using MMV.Domain.Identity;

namespace MMV.Application.Identity;

/// <summary>Persistance de l'identité (implémentée par MMV.Infrastructure sur PostgreSQL).</summary>
public interface IIdentityStore
{
    Task<UserAccount?> FindUserByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken);

    /// <summary>Session et compte associé, suivis pour mise à jour, ou <c>null</c>.</summary>
    Task<(UserSession Session, UserAccount User)?> FindSessionByTokenHashAsync(
        byte[] tokenHash, CancellationToken cancellationToken);

    Task<UserSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    void AddSession(UserSession session);

    Task<IReadOnlyList<UserAccount>> ListUsersAsync(CancellationToken cancellationToken);

    Task<bool> IsBootstrapCompletedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Crée atomiquement le compte, ses permissions et la trace de bootstrap. Renvoie <c>false</c>
    /// si la trace existe déjà (contrainte de clé primaire), sans rien créer.
    /// </summary>
    Task<bool> TryCreateInitialAdministratorAsync(
        UserAccount administrator, BootstrapRecord record, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

/// <summary>Hachage des mots de passe (ASP.NET Core Identity <c>PasswordHasher</c>), jamais de crypto maison.</summary>
public interface IPasswordHashing
{
    string Hash(UserAccount user, string password);

    PasswordCheck Verify(UserAccount user, string passwordHash, string password);

    /// <summary>Vérification sur un hachage factice : même coût lorsqu'aucun compte ne correspond.</summary>
    void VerifyAgainstDummy(string password);
}
