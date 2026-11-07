using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;
using MMV.Domain.Suppliers;

namespace MMV.Application.Catalog;

public enum StatusFilter
{
    Active,
    Archived,
    All,
}

public enum VariantSort
{
    Sku,
    Name,
    Family,
    Updated,
    Price,
    Stock,
}

/// <summary>Liste des variantes (pagination serveur) ; prix, stock et disponibilité DU magasin <see cref="StoreId"/>.</summary>
public sealed record VariantListQuery(
    Guid StoreId,
    string? Search = null,
    string? FamilyCode = null,
    StockPolicy? Policy = null,
    StatusFilter Status = StatusFilter.Active,
    Availability? Availability = null,
    bool? Incomplete = null,
    Guid? SupplierId = null,
    Guid? ModelId = null,
    VariantSort Sort = VariantSort.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = VariantListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const int MaxPage = 100_000;
    public const int SearchMaxLength = 100;
}

/// <summary>
/// Ligne de variante pour un magasin. <see cref="Price"/> est l'entité complète : MMV.Api la PROJETTE selon
/// <c>costs.read</c> (prix d'achat, coefficient, marge jamais sérialisés sans le droit).
/// </summary>
public sealed record VariantRowView(
    Guid VariantId,
    Guid ModelId,
    string Sku,
    string Label,
    string ModelName,
    string? Brand,
    string FamilyCode,
    StockPolicy Policy,
    string? Barcode,
    string? ManufacturerReference,
    bool IsArchived,
    bool IsModelArchived,
    bool IsIncomplete,
    Guid? PrimarySupplierId,
    string? PrimarySupplierName,
    int? PrimaryLeadTimeDays,
    VariantStorePrice? Price,
    string Currency,
    int OnHand,
    int Reserved,
    int? LowStockThreshold,
    Availability Availability,
    uint Version,
    DateTimeOffset UpdatedAtUtc,
    string? TaxCategoryCode = null)
{
    public int Available => OnHand - Reserved;
}

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public sealed record SupplierLinkView(VariantSupplier Link, string SupplierCode, string SupplierName, bool SupplierArchived);

public sealed record BalanceView(Guid LocationId, string LocationCode, string LocationName, bool IsDefault, int OnHand, int Reserved, uint Version, DateTimeOffset UpdatedAtUtc);

public sealed record VariantDetailView(
    ProductModel Model,
    ProductVariant Variant,
    VariantRowView Row,
    IReadOnlyList<SupplierLinkView> Suppliers,
    IReadOnlyList<BalanceView> Balances);

public sealed record SupplierListQuery(
    string? Search = null, string? CountryCode = null, StatusFilter Status = StatusFilter.Active, bool Descending = false, int Page = 1, int PageSize = 50);

public sealed record SupplierRowView(Supplier Supplier, int VariantCount, int PrimaryCount);

public sealed record SupplierVariantView(VariantSupplier Link, Guid ModelId, string Sku, string Label, string ModelName, StockPolicy Policy, bool IsArchived);

public sealed record BalanceQuery(
    Guid StoreId,
    Guid? LocationId = null,
    string? Search = null,
    string? FamilyCode = null,
    Availability? Availability = null,
    VariantSort Sort = VariantSort.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = 50);

/// <summary>Solde d'une variante <c>stocked</c> active dans un emplacement du magasin (ligne à 0 si jamais mouvementée).</summary>
public sealed record BalanceRowView(
    Guid VariantId,
    Guid ModelId,
    string Sku,
    string Label,
    string ModelName,
    string? Brand,
    string FamilyCode,
    Guid LocationId,
    string LocationCode,
    int OnHand,
    int Reserved,
    int? LowStockThreshold,
    Availability Availability,
    uint? Version,
    uint? ThresholdVersion)
{
    public int Available => OnHand - Reserved;
}

public sealed record MovementQuery(
    Guid StoreId,
    Guid? LocationId = null,
    Guid? VariantId = null,
    string? MovementType = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Page = 1,
    int PageSize = 50);

public sealed record MovementView(StockMovement Movement, string Sku, string Label, string LocationCode, string PerformedByName, string? SupplierName, string? SourceNumber);

public sealed record ReceiptSummaryView(Receipt Receipt, string SupplierName, string LocationCode, string ReceivedByName, int LineCount, int ReceivedQuantity, int? OutstandingQuantity);

public sealed record ReceiptLineView(ReceiptLine Line, string Sku, string Label);

public sealed record ReceiptDetailView(ReceiptSummaryView Summary, IReadOnlyList<ReceiptLineView> Lines, IReadOnlyList<(Guid VariantId, int Announced, int Received)> Outstanding);

public sealed record InventorySummaryView(InventorySession Session, string OpenedByName, int LineCount, int CountedCount, int DifferenceCount);

/// <summary>Ligne d'inventaire avec le stock COURANT (pour signaler « modifié depuis le comptage »).</summary>
public sealed record InventoryLineView(InventoryLine Line, string Sku, string Label, string ModelName, string LocationCode, int CurrentOnHand, string? CountedByName);

public sealed record InventoryDetailView(InventorySummaryView Summary, IReadOnlyList<InventoryLineView> Lines);

public sealed record ImportBatchView(ImportBatch Batch, string UploadedByName);

/// <summary>Lectures de la Phase 05 (aucune écriture ; aucun verrou).</summary>
public interface ICatalogReadStore
{
    Task<Page<VariantRowView>> ListVariantsAsync(VariantListQuery query, CancellationToken cancellationToken);

    Task<ProductModel?> GetModelAsync(Guid modelId, CancellationToken cancellationToken);

    Task<VariantDetailView?> GetVariantAsync(Guid variantId, Guid storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LensReference>> LensReferencesAsync(CancellationToken cancellationToken);

    Task<Page<SupplierRowView>> ListSuppliersAsync(SupplierListQuery query, CancellationToken cancellationToken);

    Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierVariantView>> SupplierVariantsAsync(Guid supplierId, CancellationToken cancellationToken);

    Task<Page<BalanceRowView>> ListBalancesAsync(BalanceQuery query, CancellationToken cancellationToken);

    /// <summary>Variantes du magasin en stock faible ou en rupture (agrégées sur ses emplacements).</summary>
    Task<IReadOnlyList<VariantRowView>> AlertsAsync(Guid storeId, CancellationToken cancellationToken);

    Task<Page<MovementView>> ListMovementsAsync(MovementQuery query, CancellationToken cancellationToken);

    Task<MovementView?> GetMovementAsync(Guid movementId, CancellationToken cancellationToken);

    Task<Page<ReceiptSummaryView>> ListReceiptsAsync(Guid storeId, Guid? supplierId, int page, int pageSize, CancellationToken cancellationToken);

    Task<ReceiptDetailView?> GetReceiptAsync(Guid receiptId, CancellationToken cancellationToken);

    Task<Page<InventorySummaryView>> ListInventoriesAsync(Guid? storeId, string? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<InventoryDetailView?> GetInventoryAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportBatchView>> ListImportsAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEvent>> EventsAsync(string moduleCode, string entityType, Guid entityId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
