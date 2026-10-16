using System.Text.RegularExpressions;
using MMV.Domain.Identity;

namespace MMV.Application.Identity;

public enum BootstrapStatus
{
    Created,
    AlreadyCompleted,
    Invalid,
}

public sealed record BootstrapResult(BootstrapStatus Status, IReadOnlyList<string> Errors, Guid? AdministratorId = null);

/// <summary>
/// Création UNIQUE du premier administrateur, appelée exclusivement par la commande locale
/// <c>MMV.Maintenance bootstrap-admin</c> (aucun endpoint HTTP). Aucun compte ni mot de passe codé
/// en dur : les valeurs sont saisies par le propriétaire au moment de l'exécution.
/// </summary>
public sealed partial class BootstrapService(IIdentityStore store, IPasswordHashing hashing, TimeProvider clock)
{
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;

    public static IReadOnlyList<string> ValidateUserName(string userName)
    {
        var trimmed = userName.Trim();
        return trimmed.Length is < 3 or > UserAccount.UserNameMaxLength || !UserNamePattern().IsMatch(trimmed)
            ? [$"Identifiant : 3 à {UserAccount.UserNameMaxLength} caractères parmi lettres non accentuées, chiffres, « . », « _ », « - », « @ »."]
            : [];
    }

    public static IReadOnlyList<string> ValidateDisplayName(string displayName)
    {
        var trimmed = displayName.Trim();
        return trimmed.Length is < 1 or > UserAccount.DisplayNameMaxLength || trimmed.Any(char.IsControl)
            ? [$"Nom affiché : 1 à {UserAccount.DisplayNameMaxLength} caractères, sans caractère de contrôle."]
            : [];
    }

    /// <summary>Longueur seule (recommandation NIST SP 800-63B) ; aucune règle de composition imposée.</summary>
    public static IReadOnlyList<string> ValidatePassword(string password) =>
        password.Length is < PasswordMinLength or > PasswordMaxLength
            ? [$"Mot de passe : {PasswordMinLength} à {PasswordMaxLength} caractères."]
            : [];

    public Task<bool> IsCompletedAsync(CancellationToken cancellationToken) => store.IsBootstrapCompletedAsync(cancellationToken);

    public async Task<BootstrapResult> CreateInitialAdministratorAsync(
        string userName, string displayName, string password, CancellationToken cancellationToken)
    {
        if (await store.IsBootstrapCompletedAsync(cancellationToken))
        {
            return new BootstrapResult(BootstrapStatus.AlreadyCompleted, []);
        }

        string[] errors = [.. ValidateUserName(userName), .. ValidateDisplayName(displayName), .. ValidatePassword(password)];
        if (errors.Length > 0)
        {
            return new BootstrapResult(BootstrapStatus.Invalid, errors);
        }

        var now = clock.GetUtcNow();
        var administrator = new UserAccount(Guid.CreateVersion7(now), userName.Trim(), displayName.Trim(), now);
        administrator.SetPasswordHash(hashing.Hash(administrator, password));
        administrator.Grant(Permissions.SystemAdmin);

        var created = await store.TryCreateInitialAdministratorAsync(
            administrator, new BootstrapRecord(administrator.Id, now), cancellationToken);

        return created
            ? new BootstrapResult(BootstrapStatus.Created, [], administrator.Id)
            : new BootstrapResult(BootstrapStatus.AlreadyCompleted, []);
    }

    [GeneratedRegex("^[A-Za-z0-9._@-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UserNamePattern();
}
