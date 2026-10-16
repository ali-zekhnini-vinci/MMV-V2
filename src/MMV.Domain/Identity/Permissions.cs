namespace MMV.Domain.Identity;

/// <summary>
/// Matrice minimale de permissions. Être authentifié donne accès à l'espace authentifié ;
/// <see cref="SystemAdmin"/> donne l'administration technique seulement (phase 02) et n'implique
/// AUCUN droit métier. Phase 03 (G03-METIER D-03-08, accepté le 17/10/2026) : droits Clients
/// distincts, attribués explicitement par <c>MMV.Maintenance permissions grant</c>. Aucune autre
/// permission métier (caisse, ordonnances, stock) n'est définie ici.
/// </summary>
public static class Permissions
{
    public const string SystemAdmin = "system.admin";

    /// <summary>Liste et fiche des clients.</summary>
    public const string ClientsRead = "clients.read";

    /// <summary>Création et modification des clients.</summary>
    public const string ClientsWrite = "clients.write";

    /// <summary>Archivage et réactivation des clients (aucune suppression physique).</summary>
    public const string ClientsArchive = "clients.archive";

    public const int MaxLength = 64;

    public static IReadOnlyList<string> All { get; } = [SystemAdmin, ClientsRead, ClientsWrite, ClientsArchive];

    /// <summary>Permissions attribuables par la commande de maintenance (jamais <see cref="SystemAdmin"/>).</summary>
    public static IReadOnlyList<string> Grantable { get; } = [ClientsRead, ClientsWrite, ClientsArchive];

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
