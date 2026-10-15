namespace MMV.Domain.Identity;

/// <summary>
/// Matrice minimale de permissions TECHNIQUES (phase 02). Être authentifié donne accès à l'espace
/// authentifié ; <see cref="SystemAdmin"/> donne l'administration technique seulement. Aucune
/// permission métier (clients, caisse, ordonnances, stock) n'est définie ici.
/// </summary>
public static class Permissions
{
    public const string SystemAdmin = "system.admin";

    public const int MaxLength = 64;

    public static IReadOnlyList<string> All { get; } = [SystemAdmin];

    public static bool IsKnown(string permission) => All.Contains(permission, StringComparer.Ordinal);
}

/// <summary>Permission technique accordée à un compte.</summary>
public sealed class PermissionGrant
{
    private PermissionGrant()
    {
        Permission = string.Empty;
    }

    internal PermissionGrant(Guid userId, string permission)
    {
        UserId = userId;
        Permission = permission;
    }

    public Guid UserId { get; private set; }

    public string Permission { get; private set; }
}

/// <summary>Normalisation des identifiants de connexion (comparaison insensible à la casse).</summary>
public static class UserNames
{
    public static string Normalize(string userName) => userName.Trim().ToUpperInvariant();
}
