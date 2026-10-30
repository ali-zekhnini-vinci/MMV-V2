using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MMV.Application.Catalog;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;
using MMV.Domain.Suppliers;
using MMV.Infrastructure.Persistence;
using Npgsql;

namespace MMV.Infrastructure.Catalog;

/// <summary>
/// Unité de travail de la Phase 05 sur le contexte EF PARTAGÉ de la requête (même transaction que l'audit). Un échec
/// d'enregistrement annule immédiatement la transaction ouverte (PostgreSQL la laisse sinon « abandonnée ») et vide le suivi.
/// </summary>
internal sealed class CatalogUnitOfWork(MmvDbContext context) : ICatalogUnitOfWork
{
    public async Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is not null
            ? NeutralScope.Instance
            : new Scope(await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken));

    public async Task<CommitResult> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new CommitResult(CommitStatus.Saved);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await FailAsync(new CommitResult(CommitStatus.VersionConflict), cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            return await FailAsync(Map(postgres), cancellationToken);
        }
        catch (PostgresException postgres)
        {
            return await FailAsync(Map(postgres), cancellationToken);
        }
    }

    public async Task AdvisoryLockAsync(long key, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);

    internal static CommitResult Map(PostgresException exception) => exception.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => new CommitResult(CommitStatus.UniqueViolation, exception.ConstraintName),
        PostgresErrorCodes.InsufficientPrivilege => new CommitResult(CommitStatus.Frozen),
        PostgresErrorCodes.CheckViolation => new CommitResult(CommitStatus.CheckViolation, exception.ConstraintName),
        PostgresErrorCodes.DeadlockDetected => new CommitResult(CommitStatus.Deadlock),
        PostgresErrorCodes.LockNotAvailable => new CommitResult(CommitStatus.LockTimeout),
        _ => throw exception,
    };

    private async Task<CommitResult> FailAsync(CommitResult result, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        if (context.Database.CurrentTransaction is not null)
        {
            await context.Database.RollbackTransactionAsync(cancellationToken);
        }

        return result;
    }

    private sealed class Scope(IDbContextTransaction transaction) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    /// <summary>Transaction déjà ouverte par l'appelant (tests, futures ventes) : il en garde la maîtrise.</summary>
    private sealed class NeutralScope : ITransactionScope
    {
        public static readonly NeutralScope Instance = new();

        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Accès aux magasins, au référentiel verres, aux modèles, variantes, fournisseurs et rattachements.</summary>
internal sealed class CatalogStore(MmvDbContext context) : ICatalogStore
{
    public Task<Store?> GetStoreAsync(Guid storeId, CancellationToken cancellationToken) =>
        context.Stores.AsNoTracking().SingleOrDefaultAsync(s => s.Id == storeId, cancellationToken);

    public async Task<IReadOnlyList<Store>> ListStoresAsync(bool includeArchived, CancellationToken cancellationToken) =>
        await context.Stores.AsNoTracking().Where(s => includeArchived || s.ArchivedAtUtc == null).OrderBy(s => s.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Location>> ListLocationsAsync(Guid storeId, CancellationToken cancellationToken) =>
        await context.Locations.AsNoTracking().Where(l => l.StoreId == storeId).OrderByDescending(l => l.IsDefault).ThenBy(l => l.Code).ToListAsync(cancellationToken);

    public Task<Location?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken) =>
        context.Locations.AsNoTracking().SingleOrDefaultAsync(l => l.Id == locationId, cancellationToken);

    public async Task<bool> LockStoreAsync(Guid storeId, CancellationToken cancellationToken) =>
        await context.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM org.stores WHERE id = {storeId} FOR SHARE").ToListAsync(cancellationToken) is [1];

    public Task<LensReference?> FindLensReferenceAsync(Guid id, CancellationToken cancellationToken) =>
        context.LensReferences.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<LensReference?> FindLensReferenceByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.LensReferences.AsNoTracking().FirstOrDefaultAsync(r => r.LastRequestId == requestId && r.CreatedAtUtc == r.UpdatedAtUtc, cancellationToken);

    public async Task<IReadOnlyList<LensReference>> ListLensReferencesAsync(CancellationToken cancellationToken) =>
        await context.LensReferences.AsNoTracking().OrderBy(r => r.KindCode).ThenBy(r => r.Name).ToListAsync(cancellationToken);

    public Task<bool> LensReferenceTakenAsync(string kindCode, Guid? manufacturerId, string nameKey, Guid? excludeId, CancellationToken cancellationToken) =>
        context.LensReferences.AnyAsync(r => r.KindCode == kindCode && r.ManufacturerId == manufacturerId && r.NameKey == nameKey && r.Id != excludeId, cancellationToken);

    public void Add(LensReference reference) => context.LensReferences.Add(reference);

    public Task<ProductModel?> FindModelAsync(Guid id, CancellationToken cancellationToken) =>
        context.ProductModels.Include(m => m.Variants).ThenInclude(v => v.Treatments).SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<ProductModel?> FindModelByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.ProductModels.AsNoTracking().SingleOrDefaultAsync(m => m.CreationRequestId == requestId, cancellationToken);

    public Task<ProductVariant?> FindVariantAsync(Guid id, CancellationToken cancellationToken) =>
        context.ProductVariants.Include(v => v.Treatments).SingleOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<ProductVariant?> FindVariantByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.ProductVariants.AsNoTracking().SingleOrDefaultAsync(v => v.CreationRequestId == requestId, cancellationToken);

    public async Task<IReadOnlyList<ProductVariant>> FindVariantsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await context.ProductVariants.Where(v => ids.Contains(v.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>> TakenSkusAsync(IReadOnlyCollection<string> skuKeys, Guid? excludeVariantId, CancellationToken cancellationToken) =>
        (await context.ProductVariants.AsNoTracking().Where(v => skuKeys.Contains(v.SkuKey) && v.Id != excludeVariantId).Select(v => v.SkuKey).ToListAsync(cancellationToken))
        .ToHashSet(StringComparer.Ordinal);

    public async Task<IReadOnlySet<string>> TakenBarcodesAsync(IReadOnlyCollection<string> barcodes, Guid? excludeVariantId, CancellationToken cancellationToken) =>
        (await context.ProductVariants.AsNoTracking().Where(v => v.Barcode != null && barcodes.Contains(v.Barcode) && v.Id != excludeVariantId).Select(v => v.Barcode!).ToListAsync(cancellationToken))
        .ToHashSet(StringComparer.Ordinal);

    public void Add(ProductModel model) => context.ProductModels.Add(model);

    public void Add(ProductVariant variant) => context.ProductVariants.Add(variant);

    public async Task<bool> LockVariantAsync(Guid variantId, RowLock strength, CancellationToken cancellationToken) =>
        await context.Database.SqlQuery<int>(strength switch
        {
            RowLock.Share => $"SELECT 1 AS \"Value\" FROM catalog.product_variants WHERE id = {variantId} FOR SHARE",
            RowLock.NoKeyUpdate => $"SELECT 1 AS \"Value\" FROM catalog.product_variants WHERE id = {variantId} FOR NO KEY UPDATE",
            _ => $"SELECT 1 AS \"Value\" FROM catalog.product_variants WHERE id = {variantId} FOR UPDATE",
        }).ToListAsync(cancellationToken) is [1];

    public async Task LockVariantsAsync(IReadOnlyCollection<Guid> variantIds, RowLock strength, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return;
        }

        // ORDER BY avant le verrou : les lignes sont verrouillées dans l'ordre croissant des identifiants (ordre PostgreSQL).
        var ids = variantIds.Distinct().ToArray();
        await context.Database.SqlQuery<Guid>(strength switch
        {
            RowLock.Share => $"SELECT id AS \"Value\" FROM catalog.product_variants WHERE id = ANY({ids}) ORDER BY id FOR SHARE",
            RowLock.NoKeyUpdate => $"SELECT id AS \"Value\" FROM catalog.product_variants WHERE id = ANY({ids}) ORDER BY id FOR NO KEY UPDATE",
            _ => $"SELECT id AS \"Value\" FROM catalog.product_variants WHERE id = ANY({ids}) ORDER BY id FOR UPDATE",
        }).ToListAsync(cancellationToken);
    }

    public async Task<(int OnHand, int Reserved)> StockTotalsAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var totals = await context.StockBalances.AsNoTracking().Where(b => b.VariantId == variantId)
            .GroupBy(b => b.VariantId).Select(g => new { OnHand = g.Sum(b => b.OnHand), Reserved = g.Sum(b => b.Reserved) }).SingleOrDefaultAsync(cancellationToken);
        return (totals?.OnHand ?? 0, totals?.Reserved ?? 0);
    }

    public Task<bool> InOpenInventoryAsync(Guid variantId, CancellationToken cancellationToken) =>
        context.InventoryLines.AnyAsync(
            l => l.VariantId == variantId && context.InventorySessions.Any(s => s.Id == l.SessionId && s.Status == InventoryStatuses.Open), cancellationToken);

    public Task<Supplier?> FindSupplierAsync(Guid id, CancellationToken cancellationToken) =>
        context.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Supplier?> FindSupplierByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.CreationRequestId == requestId, cancellationToken);

    public Task<bool> SupplierCodeTakenAsync(string codeKey, Guid? excludeId, CancellationToken cancellationToken) =>
        context.Suppliers.AnyAsync(s => s.CodeKey == codeKey && s.Id != excludeId, cancellationToken);

    public void Add(Supplier supplier) => context.Suppliers.Add(supplier);

    public async Task<bool> LockSupplierAsync(Guid supplierId, RowLock strength, CancellationToken cancellationToken) =>
        await context.Database.SqlQuery<int>(strength == RowLock.Share
            ? (FormattableString)$"SELECT 1 AS \"Value\" FROM catalog.suppliers WHERE id = {supplierId} FOR SHARE"
            : $"SELECT 1 AS \"Value\" FROM catalog.suppliers WHERE id = {supplierId} FOR NO KEY UPDATE").ToListAsync(cancellationToken) is [1];

    public Task<VariantSupplier?> FindLinkAsync(Guid variantId, Guid supplierId, CancellationToken cancellationToken) =>
        context.VariantSuppliers.SingleOrDefaultAsync(l => l.VariantId == variantId && l.SupplierId == supplierId, cancellationToken);

    public async Task<IReadOnlyList<VariantSupplier>> LinksForVariantAsync(Guid variantId, CancellationToken cancellationToken) =>
        await context.VariantSuppliers.Where(l => l.VariantId == variantId).ToListAsync(cancellationToken);

    public void Add(VariantSupplier link) => context.VariantSuppliers.Add(link);

    public async Task<IReadOnlyList<ProductVariant>> OrderOnlyVariantsWithPrimaryAsync(Guid supplierId, CancellationToken cancellationToken) =>
        await context.ProductVariants.AsNoTracking()
            .Where(v => v.StockPolicyCode == StockPolicies.OrderOnly && v.ArchivedAtUtc == null
                && context.VariantSuppliers.Any(l => l.VariantId == v.Id && l.SupplierId == supplierId && l.IsPrimary))
            .OrderBy(v => v.Sku).ToListAsync(cancellationToken);

}

internal sealed class StoreAdministrationStore(MmvDbContext context) : IStoreAdministrationStore
{
    public Task<Store?> FindStoreByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Stores.AsNoTracking().SingleOrDefaultAsync(s => s.Code == code, cancellationToken);

    public Task<bool> LocationCodeTakenAsync(Guid storeId, string code, CancellationToken cancellationToken) =>
        context.Locations.AnyAsync(l => l.StoreId == storeId && l.Code == code, cancellationToken);

    public void Add(Store store) => context.Stores.Add(store);

    public void Add(Location location) => context.Locations.Add(location);
}

internal sealed class PricingStore(MmvDbContext context) : IPricingStore
{
    public async Task<(VariantStorePrice Price, bool Created)> LockPriceAsync(Guid variantId, Guid storeId, string currencyCode, Guid userId, CancellationToken cancellationToken)
    {
        // Ligne créée vide dans la transaction (annulée avec elle en cas de refus) ; la création concurrente attend le verrou.
        var created = await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO pricing.variant_store_prices (variant_id, store_id, currency_code, pricing_mode, updated_at_utc, updated_by_user_id)
            VALUES ({variantId}, {storeId}, {currencyCode}, 'coefficient', now(), {userId})
            ON CONFLICT (variant_id, store_id) DO NOTHING
            """,
            cancellationToken) == 1;
        var price = await context.VariantStorePrices
            .FromSql($"SELECT *, xmin FROM pricing.variant_store_prices WHERE variant_id = {variantId} AND store_id = {storeId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return (price.Single(), created);
    }

    public async Task<IReadOnlyList<VariantStorePrice>> LockPricesAsync(Guid storeId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken) =>
        await context.VariantStorePrices
            .FromSql($"SELECT *, xmin FROM pricing.variant_store_prices WHERE store_id = {storeId} AND variant_id = ANY({variantIds.ToArray()}) ORDER BY variant_id FOR UPDATE")
            .ToListAsync(cancellationToken);

    public Task<VariantStorePrice?> GetPriceAsync(Guid variantId, Guid storeId, CancellationToken cancellationToken) =>
        context.VariantStorePrices.AsNoTracking().SingleOrDefaultAsync(p => p.VariantId == variantId && p.StoreId == storeId, cancellationToken);

    public async Task<IReadOnlyList<VariantStorePrice>> PricesAsync(Guid storeId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken) =>
        await context.VariantStorePrices.AsNoTracking().Where(p => p.StoreId == storeId && variantIds.Contains(p.VariantId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VariantStorePrice>> PendingRecalculationsAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var candidates = await context.VariantStorePrices.AsNoTracking()
            .Where(p => p.StoreId == storeId && p.PricingModeCode == PricingModes.Coefficient && p.PurchasePriceHt != null && p.Coefficient != null
                && context.ProductVariants.Any(v => v.Id == p.VariantId && v.ArchivedAtUtc == null))
            .ToListAsync(cancellationToken);
        return [.. candidates.Where(p => p.Status == PriceStatus.RecalculationPending).OrderBy(p => p.VariantId)];
    }

    public async Task<IReadOnlyList<PriceHistoryEntry>> HistoryAsync(Guid variantId, Guid? storeId, CancellationToken cancellationToken) =>
        await context.PriceHistory.AsNoTracking()
            .Where(h => h.VariantId == variantId && (storeId == null || h.StoreId == storeId || h.StoreId == null))
            .OrderByDescending(h => h.ChangedAtUtc).ThenByDescending(h => h.Id).Take(500).ToListAsync(cancellationToken);

    public Task<bool> RequestAppliedAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.PriceHistory.AnyAsync(h => h.RequestId == requestId, cancellationToken);

    public void Add(PriceHistoryEntry entry) => context.PriceHistory.Add(entry);

    public Task<TaxCategory?> TaxCategoryAsync(Guid storeId, string code, CancellationToken cancellationToken) =>
        context.TaxCategories.AsNoTracking().SingleOrDefaultAsync(c => c.StoreId == storeId && c.Code == code, cancellationToken);
}

internal sealed class StockStore(MmvDbContext context) : IStockStore
{
    public async Task<StockBalance> LockBalanceAsync(Guid variantId, Guid locationId, CancellationToken cancellationToken) =>
        (await LockBalancesAsync([(variantId, locationId)], cancellationToken)).Single();

    public async Task<IReadOnlyList<StockBalance>> LockBalancesAsync(IReadOnlyCollection<(Guid VariantId, Guid LocationId)> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        var variants = keys.Select(k => k.VariantId).ToArray();
        var locations = keys.Select(k => k.LocationId).ToArray();
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO stock.balances (variant_id, location_id, on_hand, reserved, updated_at_utc)
            SELECT k.variant_id, k.location_id, 0, 0, now() FROM unnest({variants}, {locations}) AS k(variant_id, location_id)
            ON CONFLICT (variant_id, location_id) DO NOTHING
            """,
            cancellationToken);
        // Verrouillées dans l'ordre (variante, emplacement) : ordre unique de toutes les écritures de stock.
        return await context.StockBalances
            .FromSql(
                $"""
                SELECT b.*, b.xmin FROM stock.balances b
                JOIN unnest({variants}, {locations}) AS k(variant_id, location_id) ON k.variant_id = b.variant_id AND k.location_id = b.location_id
                ORDER BY b.variant_id, b.location_id FOR UPDATE OF b
                """)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockBalance>> BalancesAsync(IReadOnlyCollection<Guid> variantIds, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken) =>
        await context.StockBalances.AsNoTracking().Where(b => variantIds.Contains(b.VariantId) && locationIds.Contains(b.LocationId)).ToListAsync(cancellationToken);

    public void Add(StockMovement movement) => context.StockMovements.Add(movement);

    public void Add(Receipt receipt) => context.Receipts.Add(receipt);

    public async Task<IReadOnlyList<StockMovement>> MovementsByRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        await context.StockMovements.AsNoTracking().Where(m => m.RequestId == requestId).ToListAsync(cancellationToken);

    public Task<Receipt?> FindReceiptAsync(Guid id, CancellationToken cancellationToken) =>
        context.Receipts.AsNoTracking().Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Receipt?> FindReceiptByRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.Receipts.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

    public async Task<IReadOnlyList<ReceiptLine>> ReceiptChainLinesAsync(Guid rootReceiptId, CancellationToken cancellationToken) =>
        await context.Set<ReceiptLine>().AsNoTracking()
            .Where(l => l.ReceiptId == rootReceiptId || context.Receipts.Any(r => r.Id == l.ReceiptId && r.CompletesReceiptId == rootReceiptId))
            .ToListAsync(cancellationToken);

    public async Task<(VariantStoreSetting Setting, bool Created)> LockSettingAsync(Guid variantId, Guid storeId, Guid userId, CancellationToken cancellationToken)
    {
        var created = await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO stock.variant_store_settings (variant_id, store_id, updated_at_utc, updated_by_user_id)
            VALUES ({variantId}, {storeId}, now(), {userId})
            ON CONFLICT (variant_id, store_id) DO NOTHING
            """,
            cancellationToken) == 1;
        var setting = await context.VariantStoreSettings
            .FromSql($"SELECT *, xmin FROM stock.variant_store_settings WHERE variant_id = {variantId} AND store_id = {storeId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return (setting.Single(), created);
    }
}

internal sealed class InventoryStore(MmvDbContext context) : IInventoryStore
{
    public void Add(InventorySession session) => context.InventorySessions.Add(session);

    public async Task<InventorySession?> FindSessionAsync(Guid id, bool lockForUpdate, CancellationToken cancellationToken)
    {
        if (lockForUpdate && await context.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM stock.inventory_sessions WHERE id = {id} FOR UPDATE").ToListAsync(cancellationToken) is not [1])
        {
            return null;
        }

        return await context.InventorySessions.Include(s => s.Lines).AsSplitQuery().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<string?> ShareSessionAsync(Guid id, CancellationToken cancellationToken) =>
        (await context.Database.SqlQuery<string>($"SELECT status AS \"Value\" FROM stock.inventory_sessions WHERE id = {id} FOR SHARE").ToListAsync(cancellationToken))
        .SingleOrDefault();

    public Task<InventorySession?> FindSessionByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.InventorySessions.AsNoTracking().SingleOrDefaultAsync(s => s.CreationRequestId == requestId, cancellationToken);

    public async Task<IReadOnlyList<(Guid VariantId, Guid LocationId)>> OpenLinesAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken) =>
        [.. (await context.InventoryLines.AsNoTracking()
            .Where(l => locationIds.Contains(l.LocationId) && context.InventorySessions.Any(s => s.Id == l.SessionId && s.Status == InventoryStatuses.Open))
            .Select(l => new { l.VariantId, l.LocationId }).ToListAsync(cancellationToken))
            .Select(l => (l.VariantId, l.LocationId))];

    public async Task<IReadOnlyList<Guid>> CandidateVariantsAsync(string scopeType, string? familyCode, IReadOnlyCollection<Guid>? selection, CancellationToken cancellationToken) =>
        await context.ProductVariants.AsNoTracking()
            .Where(v => v.StockPolicyCode == StockPolicies.Stocked && v.ArchivedAtUtc == null
                && context.ProductModels.Any(m => m.Id == v.ModelId && m.ArchivedAtUtc == null && (familyCode == null || m.FamilyCode == familyCode))
                && (selection == null || selection.Contains(v.Id)))
            .Select(v => v.Id).ToListAsync(cancellationToken);

    public async Task<InventoryLine?> LockLineAsync(Guid sessionId, Guid variantId, Guid locationId, CancellationToken cancellationToken) =>
        (await context.InventoryLines
            .FromSql($"SELECT *, xmin FROM stock.inventory_lines WHERE session_id = {sessionId} AND variant_id = {variantId} AND location_id = {locationId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
}

internal sealed class ImportStore(MmvDbContext context) : IImportStore
{
    public void Add(ImportBatch batch) => context.ImportBatches.Add(batch);

    public Task<ImportBatch?> FindBatchAsync(Guid id, CancellationToken cancellationToken) =>
        context.ImportBatches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<ImportBatch?> FindBatchByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.ImportBatches.AsNoTracking().SingleOrDefaultAsync(b => b.CreationRequestId == requestId, cancellationToken);

    public Task<bool> AppliedFileExistsAsync(string sha256, string kind, Guid? storeId, CancellationToken cancellationToken) =>
        context.ImportBatches.AnyAsync(b => b.Status == ImportStatuses.Applied && b.FileSha256 == sha256 && b.Kind == kind && b.StoreId == storeId, cancellationToken);

    public async Task ReplaceRowsAsync(Guid batchId, IReadOnlyList<ImportRow> rows, CancellationToken cancellationToken)
    {
        // Rapport d'un lot NON CLOS seulement (déclencheur trg_rows_frozen) : un nouveau contrôle remplace le précédent.
        await context.ImportRows.Where(r => r.BatchId == batchId).ExecuteDeleteAsync(cancellationToken);
        context.ImportRows.AddRange(rows);
    }

    public async Task<IReadOnlyList<ImportRow>> RowsAsync(Guid batchId, CancellationToken cancellationToken) =>
        await context.ImportRows.AsNoTracking().Where(r => r.BatchId == batchId).OrderBy(r => r.LineNo).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(int limit, CancellationToken cancellationToken) =>
        await context.ImportBatches.AsNoTracking().OrderByDescending(b => b.UploadedAtUtc).Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, ProductVariant>> VariantsBySkuAsync(IReadOnlyCollection<string> skuKeys, CancellationToken cancellationToken) =>
        await context.ProductVariants.AsNoTracking().Where(v => skuKeys.Contains(v.SkuKey)).ToDictionaryAsync(v => v.SkuKey, StringComparer.Ordinal, cancellationToken);

    public async Task<IReadOnlyDictionary<string, Supplier>> SuppliersByCodeAsync(IReadOnlyCollection<string> codeKeys, CancellationToken cancellationToken) =>
        await context.Suppliers.AsNoTracking().Where(s => codeKeys.Contains(s.CodeKey)).ToDictionaryAsync(s => s.CodeKey, StringComparer.Ordinal, cancellationToken);

    public async Task<IReadOnlyDictionary<string, ProductModel>> ModelsByKeyAsync(IReadOnlyCollection<string> familyCodes, CancellationToken cancellationToken)
    {
        var models = await context.ProductModels.AsNoTracking().Where(m => familyCodes.Contains(m.FamilyCode) && m.ArchivedAtUtc == null).ToListAsync(cancellationToken);
        var result = new Dictionary<string, ProductModel>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            result.TryAdd(ImportService.ModelKey(model.Snapshot()), model);
        }

        return result;
    }
}
