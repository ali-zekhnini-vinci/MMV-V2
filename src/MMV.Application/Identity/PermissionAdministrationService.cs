using MMV.Domain.Identity;

namespace MMV.Application.Identity;

public enum PermissionChangeStatus
{
    Granted,
    Revoked,
    Unchanged,
    UnknownAccount,
    NotGrantable,
}

public sealed record PermissionChangeResult(PermissionChangeStatus Status, Guid? UserId = null, IReadOnlyList<string>? Permissions = null);

/// <summary>
/// Attribution et retrait des droits métier (D-03-09), appelés exclusivement par la commande locale
/// <c>MMV.Maintenance permissions grant|revoke</c> (aucun endpoint HTTP). Seules les permissions de
/// <see cref="Permissions.Grantable"/> sont admises : <c>system.admin</c> n'est ni attribuable ni
/// retirable par cette voie (il reste réservé au bootstrap).
/// </summary>
public sealed class PermissionAdministrationService(IIdentityStore store)
{
    public static bool IsGrantable(string permission) => Permissions.Grantable.Contains(permission, StringComparer.Ordinal);

    public Task<PermissionChangeResult> GrantAsync(string userName, string permission, CancellationToken cancellationToken) =>
        ChangeAsync(userName, permission, grant: true, cancellationToken);

    public Task<PermissionChangeResult> RevokeAsync(string userName, string permission, CancellationToken cancellationToken) =>
        ChangeAsync(userName, permission, grant: false, cancellationToken);

    private async Task<PermissionChangeResult> ChangeAsync(
        string userName, string permission, bool grant, CancellationToken cancellationToken)
    {
        if (!IsGrantable(permission))
        {
            return new PermissionChangeResult(PermissionChangeStatus.NotGrantable);
        }

        var user = await store.FindUserByNormalizedNameAsync(UserNames.Normalize(userName), cancellationToken);
        if (user is null)
        {
            return new PermissionChangeResult(PermissionChangeStatus.UnknownAccount);
        }

        if (user.HasPermission(permission) == grant)
        {
            return new PermissionChangeResult(PermissionChangeStatus.Unchanged, user.Id, Current(user));
        }

        if (grant)
        {
            user.Grant(permission);
        }
        else
        {
            user.Revoke(permission);
        }

        await store.SaveChangesAsync(cancellationToken);
        return new PermissionChangeResult(
            grant ? PermissionChangeStatus.Granted : PermissionChangeStatus.Revoked, user.Id, Current(user));
    }

    private static string[] Current(UserAccount user) =>
        [.. user.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal)];
}
