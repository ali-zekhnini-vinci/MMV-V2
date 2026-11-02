using MMV.Domain.Catalog;

namespace MMV.Domain.Stock;

/// <summary>Types de mouvement (D-05-09). Les types réservés aux phases suivantes sont refusés par la contrainte SQL de 0007.</summary>
public static class MovementTypes
{
    public const string Receipt = "receipt";
    public const string ManualIn = "manual_in";
    public const string ManualOut = "manual_out";
    public const string Adjustment = "adjustment";
    public const string InventoryAdjustment = "inventory_adjustment";

    public static IReadOnlyList<string> Phase05 { get; } = [Receipt, ManualIn, ManualOut, Adjustment, InventoryAdjustment];

    /// <summary>Saisissables par <c>POST /api/stock/movements</c> (les réceptions passent par <c>/receipts</c>).</summary>
    public static IReadOnlyList<string> Manual { get; } = [ManualIn, ManualOut];
}

/// <summary>
/// Motifs codés par type de mouvement (D-05-09 / QR-03 : liste proposée appliquée comme règle technique conservatrice,
/// non ratifiée). <c>other</c> exige un commentaire.
/// </summary>
public static class StockReasons
{
    public const string Other = "other";
    public const string SupplierReceipt = "supplier_receipt";
    public const string InitialStock = "initial_stock";
    public const int CommentMaxLength = 500;

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ByType { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [MovementTypes.Receipt] = [SupplierReceipt],
        // « initial_stock » : stock initial saisi à la création d'un produit (assistant COR-05-06), code borné du même format.
        [MovementTypes.ManualIn] = ["found", "internal_use_return", "customer_return_unsold", "entry_error", InitialStock, Other],
        [MovementTypes.ManualOut] = ["breakage", "loss", "theft", "internal_use", "supplier_return", "entry_error", Other],
        [MovementTypes.Adjustment] = ["count_correction", "breakage", "loss", "theft", "entry_error", Other],
        [MovementTypes.InventoryAdjustment] = ["count_correction", "breakage", "loss", "theft", "entry_error", Other],
    };

    public static bool IsAllowed(string movementType, string? reasonCode) =>
        reasonCode is not null && ByType.TryGetValue(movementType, out var reasons) && reasons.Contains(reasonCode, StringComparer.Ordinal);
}

/// <summary>Disponibilité calculée par le serveur (CADRAGE § 8.4) ; jamais saisie, jamais recalculée par le poste.</summary>
public enum Availability
{
    Archived,
    Available,
    OnOrder,
    Incomplete,
    InStock,
    LowStock,
    OutOfStock,
}

public static class Availabilities
{
    public static string ToCode(Availability value) => value switch
    {
        Availability.Archived => "archived",
        Availability.Available => "available",
        Availability.OnOrder => "on_order",
        Availability.Incomplete => "incomplete",
        Availability.InStock => "in_stock",
        Availability.LowStock => "low_stock",
        _ => "out_of_stock",
    };

    public static Availability? FromCode(string? code) => code switch
    {
        "archived" => Availability.Archived,
        "available" => Availability.Available,
        "on_order" => Availability.OnOrder,
        "incomplete" => Availability.Incomplete,
        "in_stock" => Availability.InStock,
        "low_stock" => Availability.LowStock,
        "out_of_stock" => Availability.OutOfStock,
        _ => null,
    };

    /// <summary>
    /// Calcul unique. <paramref name="available"/> = en main − réservé (agrégé sur les emplacements du magasin) ;
    /// stock faible si 0 &lt; disponible ≤ seuil ; seuil vide = jamais « faible ».
    /// </summary>
    public static Availability Compute(StockPolicy policy, bool archived, bool incomplete, int available, int? threshold) =>
        archived ? Availability.Archived
        : policy switch
        {
            StockPolicy.Service => Availability.Available,
            StockPolicy.OrderOnly => incomplete ? Availability.Incomplete : Availability.OnOrder,
            _ => available <= 0 ? Availability.OutOfStock
                : threshold is { } t && available <= t ? Availability.LowStock
                : Availability.InStock,
        };
}

/// <summary>Contrôles d'une sortie (D-05-03 stock négatif interdit, D-05-17 réservé intouchable), appliqués SOUS VERROU.</summary>
public enum StockCheck
{
    Ok,
    Insufficient,
    Reserved,
}

public static class StockRules
{
    public static StockCheck CheckDelta(int onHand, int reserved, int delta)
    {
        var after = (long)onHand + delta;
        if (after < 0)
        {
            return StockCheck.Insufficient;
        }

        return delta < 0 && after < reserved ? StockCheck.Reserved : StockCheck.Ok;
    }
}

/// <summary>
/// Solde d'une variante <c>stocked</c> dans un emplacement : en main ≥ 0 (contrainte SQL), réservé (toujours 0 en Phase 05,
/// écrit par la Phase 06), version (xmin). Modifié UNIQUEMENT avec un mouvement, dans la même transaction, solde verrouillé.
/// </summary>
public sealed class StockBalance
{
    private StockBalance()
    {
    }

    public Guid VariantId { get; private set; }

    public Guid LocationId { get; private set; }

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public long? LastMovementSeq { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public int Available => OnHand - Reserved;

    public static StockBalance Empty(Guid variantId, Guid locationId, DateTimeOffset now) =>
        new() { VariantId = variantId, LocationId = locationId, UpdatedAtUtc = now };

    public void Apply(int delta, DateTimeOffset now)
    {
        if (StockRules.CheckDelta(OnHand, Reserved, delta) != StockCheck.Ok)
        {
            throw new InvalidOperationException("Mouvement refusé : stock insuffisant ou réservé.");
        }

        OnHand += delta;
        UpdatedAtUtc = now;
    }
}

/// <summary>Seuil d'alerte de stock faible par (variante, magasin), facultatif (D-05-19 : aucun seuil par défaut).</summary>
public sealed class VariantStoreSetting
{
    public const int ThresholdMax = 100_000;

    private VariantStoreSetting()
    {
    }

    public Guid VariantId { get; private set; }

    public Guid StoreId { get; private set; }

    public int? LowStockThreshold { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public Guid? LastRequestId { get; private set; }

    public uint Version { get; private set; }

    public static VariantStoreSetting Create(Guid variantId, Guid storeId) => new() { VariantId = variantId, StoreId = storeId };

    public void SetThreshold(int? threshold, Guid userId, DateTimeOffset now, Guid requestId)
    {
        LowStockThreshold = threshold;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }
}

/// <summary>
/// Mouvement de stock, en AJOUT SEUL (déclencheur SQL) : quantité avant / après, écart signé, motif codé, auteur,
/// source (réception, session d'inventaire ; vente plus tard). Correction = nouveau mouvement motivé.
/// </summary>
public sealed class StockMovement
{
    private StockMovement()
    {
        MovementType = string.Empty;
        ReasonCode = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Ordre total attribué par PostgreSQL (identité).</summary>
    public long Seq { get; private set; }

    public Guid VariantId { get; private set; }

    public Guid LocationId { get; private set; }

    public string MovementType { get; private set; }

    public int QuantityDelta { get; private set; }

    public int QuantityBefore { get; private set; }

    public int QuantityAfter { get; private set; }

    public string ReasonCode { get; private set; }

    public string? Comment { get; private set; }

    public Guid? SupplierId { get; private set; }

    /// <summary>Coût unitaire HT d'une réception (PROTÉGÉ), dans la devise du magasin de l'emplacement.</summary>
    public decimal? UnitCostHt { get; private set; }

    public string? CurrencyCode { get; private set; }

    public string? SourceType { get; private set; }

    public Guid? SourceId { get; private set; }

    public Guid PerformedByUserId { get; private set; }

    public DateTimeOffset PerformedAtUtc { get; private set; }

    public Guid RequestId { get; private set; }

    public static StockMovement Record(
        Guid id, StockBalance balance, string movementType, int delta, string reasonCode, string? comment, Guid? supplierId, decimal? unitCostHt,
        string? currencyCode, string? sourceType, Guid? sourceId, Guid userId, DateTimeOffset now, Guid requestId)
    {
        if (delta == 0)
        {
            throw new ArgumentException("Écart nul : aucun mouvement.", nameof(delta));
        }

        var before = balance.OnHand;
        balance.Apply(delta, now);
        return new StockMovement
        {
            Id = id,
            VariantId = balance.VariantId,
            LocationId = balance.LocationId,
            MovementType = movementType,
            QuantityDelta = delta,
            QuantityBefore = before,
            QuantityAfter = balance.OnHand,
            ReasonCode = reasonCode,
            Comment = comment,
            SupplierId = supplierId,
            UnitCostHt = unitCostHt,
            CurrencyCode = unitCostHt is null ? null : currencyCode,
            SourceType = sourceType,
            SourceId = sourceId,
            PerformedByUserId = userId,
            PerformedAtUtc = now,
            RequestId = requestId,
        };
    }
}

public static class StockSources
{
    public const string Receipt = "receipt";
    public const string InventorySession = "inventory_session";
}

/// <summary>
/// Réception simple d'un fournisseur (Q12) : une pièce numérotée (bon de livraison facultatif), lignes reçues en une
/// transaction. RÉCEPTION PARTIELLE : une ligne peut porter la quantité annoncée par le fournisseur ; le reliquat est
/// reçu plus tard par une réception de complément liée (<see cref="CompletesReceiptId"/>). Aucune commande fournisseur
/// (BL-05-01). Immuable après enregistrement.
/// </summary>
public sealed class Receipt
{
    public const int DocumentReferenceMaxLength = 64;
    public const int NotesMaxLength = 500;
    public const int MaxLines = 200;

    private readonly List<ReceiptLine> _lines = [];

    private Receipt()
    {
        Number = string.Empty;
        CurrencyCode = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Numéro lisible attribué par PostgreSQL (<c>REC-000001</c>).</summary>
    public string Number { get; private set; }

    public Guid StoreId { get; private set; }

    public Guid LocationId { get; private set; }

    public Guid SupplierId { get; private set; }

    public string CurrencyCode { get; private set; }

    public string? SupplierDocumentReference { get; private set; }

    public string? Notes { get; private set; }

    public Guid? CompletesReceiptId { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public Guid ReceivedByUserId { get; private set; }

    public Guid RequestId { get; private set; }

    public IReadOnlyList<ReceiptLine> Lines => _lines;

    public static Receipt Create(
        Guid id, Guid storeId, Guid locationId, Guid supplierId, string currencyCode, string? documentReference, string? notes, Guid? completesReceiptId,
        Guid userId, DateTimeOffset now, Guid requestId) =>
        new()
        {
            Id = id,
            StoreId = storeId,
            LocationId = locationId,
            SupplierId = supplierId,
            CurrencyCode = currencyCode,
            SupplierDocumentReference = documentReference,
            Notes = notes,
            CompletesReceiptId = completesReceiptId,
            ReceivedAtUtc = now,
            ReceivedByUserId = userId,
            RequestId = requestId,
        };

    public void AddLine(ReceiptLine line) => _lines.Add(line);
}

public sealed class ReceiptLine
{
    private ReceiptLine()
    {
    }

    public Guid ReceiptId { get; private set; }

    public int LineNo { get; private set; }

    public Guid VariantId { get; private set; }

    /// <summary>Quantité annoncée (bon de livraison) ; null = non renseignée.</summary>
    public int? AnnouncedQuantity { get; private set; }

    public int ReceivedQuantity { get; private set; }

    public decimal? UnitCostHt { get; private set; }

    public Guid? MovementId { get; private set; }

    public static ReceiptLine Create(Guid receiptId, int lineNo, Guid variantId, int? announced, int received, decimal? unitCostHt, Guid? movementId) =>
        new()
        {
            ReceiptId = receiptId,
            LineNo = lineNo,
            VariantId = variantId,
            AnnouncedQuantity = announced,
            ReceivedQuantity = received,
            UnitCostHt = unitCostHt,
            MovementId = movementId,
        };
}
