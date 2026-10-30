namespace MMV.Domain.Identity;

/// <summary>
/// Matrice minimale de permissions. Être authentifié donne accès à l'espace authentifié ;
/// <see cref="SystemAdmin"/> donne l'administration technique seulement (phase 02) et n'implique
/// AUCUN droit métier. Phase 03 (G03-METIER D-03-08, accepté le 17/10/2026) : droits Clients
/// distincts, attribués explicitement par <c>MMV.Maintenance permissions grant</c>. Phase 03-B2 (D-03-17,
/// approuvée le 17/10/2026) : droits Rendez-vous distincts, même attribution. Phase 03-D2 : <see cref="AuditRead"/>,
/// droit DÉDIÉ à la lecture du journal d'audit, distinct des droits métier (lire un client ou un rendez-vous ne donne
/// pas son historique) et de <see cref="SystemAdmin"/>. Phase 04-B (D-04-05, mandat GO du 27/10/2026) : quatre droits
/// du dossier optique, explicites, combinés par le serveur avec <see cref="ClientsRead"/> ; aucun n'en implique un autre
/// et <see cref="SystemAdmin"/> n'en donne aucun. Phase 05 : seize droits Catalogue, Tarifs, Coûts, Fournisseurs, Stock
/// et Inventaires (<see cref="Phase05"/>). Aucune permission de caisse ni de vente n'est définie ici.
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

    /// <summary>Agenda, détail et paramètres des rendez-vous.</summary>
    public const string AppointmentsRead = "appointments.read";

    /// <summary>Création, modification et déplacement des rendez-vous.</summary>
    public const string AppointmentsWrite = "appointments.write";

    /// <summary>Annulation et rétablissement des rendez-vous (aucune suppression physique).</summary>
    public const string AppointmentsCancel = "appointments.cancel";

    /// <summary>Historique des actions sur les dossiers (journal d'audit, lecture seule ; 03-D2).</summary>
    public const string AuditRead = "audit.read";

    /// <summary>Dossier optique : listes, détail, révisions des deux types (ordonnances et mesures ; 04-B).</summary>
    public const string OpticalRead = "optical.read";

    /// <summary>Ordonnances médicales : brouillon, confirmation, rectification (04-B).</summary>
    public const string OpticalPrescriptionsWrite = "optical.prescriptions.write";

    /// <summary>Mesures magasin : brouillon, confirmation, rectification ; habilitation à mesurer (04-B).</summary>
    public const string OpticalMeasurementsWrite = "optical.measurements.write";

    /// <summary>Invalidation motivée d'une ordonnance ou d'une mesure (aucune suppression physique ; 04-B).</summary>
    public const string OpticalInvalidate = "optical.invalidate";

    // ---------- Phase 05 (Q16, D-05-11 ; liste QR-05 appliquée par le mandat du 30/10/2026) ----------
    // Seize droits distincts ; aucun n'en implique un autre et system.admin n'en donne aucun. Le coefficient est un coût
    // (prix HT ÷ coefficient = prix d'achat) : il suit costs.read / costs.write.

    /// <summary>Catalogue : magasins, familles, référentiel verres, modèles, variantes, prix de vente HT, disponibilité.</summary>
    public const string CatalogRead = "catalog.read";

    /// <summary>Création et modification des modèles, variantes, politiques et rattachements fournisseurs.</summary>
    public const string CatalogWrite = "catalog.write";

    /// <summary>Archivage et réactivation des modèles et variantes (aucune suppression).</summary>
    public const string CatalogArchive = "catalog.archive";

    /// <summary>Référentiel configurable des verres (fabricants, gammes, indices, traitements).</summary>
    public const string CatalogConfigure = "catalog.configure";

    /// <summary>Import CSV contrôlé (dépôt, contrôle à blanc, application).</summary>
    public const string CatalogImport = "catalog.import";

    /// <summary>Prix de vente par coefficient : aperçu, enregistrement, recalcul explicite.</summary>
    public const string PricingWrite = "pricing.write";

    /// <summary>Prix de vente HT manuel.</summary>
    public const string PricingManual = "pricing.manual";

    /// <summary>Lecture des coûts : prix d'achat, coefficients, marges, prix fournisseurs, coûts de réception.</summary>
    public const string CostsRead = "costs.read";

    /// <summary>Écriture des coûts (prix d'achat HT, prix fournisseurs, coûts unitaires de réception).</summary>
    public const string CostsWrite = "costs.write";

    /// <summary>Fiches fournisseurs.</summary>
    public const string SuppliersRead = "suppliers.read";

    /// <summary>Création, modification, archivage des fournisseurs.</summary>
    public const string SuppliersWrite = "suppliers.write";

    /// <summary>Soldes, alertes, journal des mouvements, réceptions, sessions d'inventaire (lecture).</summary>
    public const string StockRead = "stock.read";

    /// <summary>Réceptions simples, entrées et sorties manuelles motivées.</summary>
    public const string StockMovementsWrite = "stock.movements.write";

    /// <summary>Ajustements motivés (quantité constatée + attendue) et seuils d'alerte.</summary>
    public const string StockAdjust = "stock.adjust";

    /// <summary>Ouverture de session et saisie des comptages d'inventaire.</summary>
    public const string InventoryCount = "inventory.count";

    /// <summary>Justification des écarts, validation atomique et abandon d'une session d'inventaire (distinct du comptage).</summary>
    public const string InventoryValidate = "inventory.validate";

    public const int MaxLength = 64;

    public static IReadOnlyList<string> Phase05 { get; } =
        [
            CatalogRead, CatalogWrite, CatalogArchive, CatalogConfigure, CatalogImport, PricingWrite, PricingManual, CostsRead, CostsWrite,
            SuppliersRead, SuppliersWrite, StockRead, StockMovementsWrite, StockAdjust, InventoryCount, InventoryValidate,
        ];

    public static IReadOnlyList<string> All { get; } =
        [
            SystemAdmin, ClientsRead, ClientsWrite, ClientsArchive, AppointmentsRead, AppointmentsWrite, AppointmentsCancel, AuditRead,
            OpticalRead, OpticalPrescriptionsWrite, OpticalMeasurementsWrite, OpticalInvalidate,
            .. Phase05,
        ];

    /// <summary>Permissions attribuables par la commande de maintenance (jamais <see cref="SystemAdmin"/>).</summary>
    public static IReadOnlyList<string> Grantable { get; } =
        [
            ClientsRead, ClientsWrite, ClientsArchive, AppointmentsRead, AppointmentsWrite, AppointmentsCancel, AuditRead,
            OpticalRead, OpticalPrescriptionsWrite, OpticalMeasurementsWrite, OpticalInvalidate,
            .. Phase05,
        ];

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
