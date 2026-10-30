using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MMV.Application.Catalog;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure.Catalog;

/// <summary>
/// Lectures de la Phase 05 (aucun verrou). Listes : filtre SQL (recherche, famille, politique, statut), puis prix, soldes,
/// seuils et fournisseurs principaux du magasin lus pour l'ensemble filtré, disponibilité calculée par le domaine
/// (<see cref="Availabilities.Compute"/>), tri et pagination en mémoire. Volume à mesurer en pilote (réserve documentée).
/// </summary>
internal sealed class CatalogReadStore(MmvDbContext context) : ICatalogReadStore
{
    private static readonly StringComparer NameOrder = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), CompareOptions.IgnoreCase);

    private sealed record VariantBase(
        Guid VariantId, Guid ModelId, string Sku, string Label, string ModelName, string? Brand, string FamilyCode, string Policy, string? Barcode,
        string? ManufacturerReference, bool Archived, bool ModelArchived, uint Version, DateTimeOffset UpdatedAtUtc, string? TaxCategoryCode = null);

    public async Task<Page<VariantRowView>> ListVariantsAsync(VariantListQuery query, CancellationToken cancellationToken)
    {
        var shop = await context.Stores.AsNoTracking().SingleAsync(s => s.Id == query.StoreId, cancellationToken);
        var variants = from v in context.ProductVariants.AsNoTracking()
                       join m in context.ProductModels.AsNoTracking() on v.ModelId equals m.Id
                       select new { v, m };
        if (query.FamilyCode is { } family)
        {
            variants = variants.Where(x => x.m.FamilyCode == family);
        }

        if (query.Policy is { } policy)
        {
            var code = StockPolicies.ToCode(policy);
            variants = variants.Where(x => x.v.StockPolicyCode == code);
        }

        if (query.ModelId is { } modelId)
        {
            variants = variants.Where(x => x.m.Id == modelId);
        }

        variants = query.Status switch
        {
            StatusFilter.Active => variants.Where(x => x.v.ArchivedAtUtc == null && x.m.ArchivedAtUtc == null),
            StatusFilter.Archived => variants.Where(x => x.v.ArchivedAtUtc != null || x.m.ArchivedAtUtc != null),
            _ => variants,
        };

        if (query.SupplierId is { } supplierId)
        {
            variants = variants.Where(x => context.VariantSuppliers.Any(l => l.VariantId == x.v.Id && l.SupplierId == supplierId && l.RemovedAtUtc == null));
        }

        foreach (var word in (query.Search ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Take(5))
        {
            var pattern = Like(word);
            variants = variants.Where(x => x.v.SkuKey.Contains(word) || EF.Functions.ILike(x.v.Label, pattern) || EF.Functions.ILike(x.m.Name, pattern)
                || (x.m.Brand != null && EF.Functions.ILike(x.m.Brand, pattern)) || (x.v.Barcode != null && x.v.Barcode.Contains(word))
                || (x.v.ManufacturerReference != null && EF.Functions.ILike(x.v.ManufacturerReference, pattern)));
        }

        var rows = await variants.Select(x => new VariantBase(
            x.v.Id, x.m.Id, x.v.Sku, x.v.Label, x.m.Name, x.m.Brand, x.m.FamilyCode, x.v.StockPolicyCode, x.v.Barcode, x.v.ManufacturerReference,
            x.v.ArchivedAtUtc != null, x.m.ArchivedAtUtc != null, x.v.Version, x.v.UpdatedAtUtc, x.v.TaxCategoryCode)).ToListAsync(cancellationToken);
        var views = await EnrichAsync(rows, shop, cancellationToken);
        IEnumerable<VariantRowView> filtered = views;
        if (query.Availability is { } availability)
        {
            filtered = filtered.Where(v => v.Availability == availability);
        }

        if (query.Incomplete is { } incomplete)
        {
            filtered = filtered.Where(v => v.IsIncomplete == incomplete);
        }

        var list = filtered.ToList();
        var ordered = Order(list, query.Sort, query.Descending).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new Page<VariantRowView>(ordered, query.Page, query.PageSize, list.Count);
    }

    public Task<ProductModel?> GetModelAsync(Guid modelId, CancellationToken cancellationToken) =>
        context.ProductModels.AsNoTracking().Include(m => m.Variants).ThenInclude(v => v.Treatments).AsSplitQuery().SingleOrDefaultAsync(m => m.Id == modelId, cancellationToken);

    public async Task<VariantDetailView?> GetVariantAsync(Guid variantId, Guid storeId, CancellationToken cancellationToken)
    {
        var variant = await context.ProductVariants.AsNoTracking().Include(v => v.Treatments).SingleOrDefaultAsync(v => v.Id == variantId, cancellationToken);
        var shop = await context.Stores.AsNoTracking().SingleOrDefaultAsync(s => s.Id == storeId, cancellationToken);
        if (variant is null || shop is null)
        {
            return null;
        }

        var model = await context.ProductModels.AsNoTracking().SingleAsync(m => m.Id == variant.ModelId, cancellationToken);
        var row = (await EnrichAsync(
            [new VariantBase(variant.Id, model.Id, variant.Sku, variant.Label, model.Name, model.Brand, model.FamilyCode, variant.StockPolicyCode, variant.Barcode,
                variant.ManufacturerReference, variant.IsArchived, model.IsArchived, variant.Version, variant.UpdatedAtUtc, variant.TaxCategoryCode)],
            shop, cancellationToken))[0];
        var links = await (from l in context.VariantSuppliers.AsNoTracking()
                           join s in context.Suppliers.AsNoTracking() on l.SupplierId equals s.Id
                           where l.VariantId == variantId
                           orderby l.IsPrimary descending, s.Name
                           select new { l, s.Code, s.Name, Archived = s.ArchivedAtUtc != null }).ToListAsync(cancellationToken);
        var balances = await (from l in context.Locations.AsNoTracking()
                              where l.StoreId == storeId
                              join b in context.StockBalances.AsNoTracking().Where(b => b.VariantId == variantId) on l.Id equals b.LocationId into joined
                              from b in joined.DefaultIfEmpty()
                              orderby l.IsDefault descending, l.Code
                              select new BalanceView(l.Id, l.Code, l.Name, l.IsDefault, b == null ? 0 : b.OnHand, b == null ? 0 : b.Reserved, b == null ? 0u : b.Version,
                                  b == null ? l.CreatedAtUtc : b.UpdatedAtUtc)).ToListAsync(cancellationToken);
        return new VariantDetailView(model, variant, row, [.. links.Select(x => new SupplierLinkView(x.l, x.Code, x.Name, x.Archived))], balances);
    }

    public async Task<IReadOnlyList<LensReference>> LensReferencesAsync(CancellationToken cancellationToken) =>
        await context.LensReferences.AsNoTracking().OrderBy(r => r.KindCode).ThenBy(r => r.IndexValue).ThenBy(r => r.Name).ToListAsync(cancellationToken);

    public async Task<Page<SupplierRowView>> ListSuppliersAsync(SupplierListQuery query, CancellationToken cancellationToken)
    {
        var suppliers = context.Suppliers.AsNoTracking();
        suppliers = query.Status switch
        {
            StatusFilter.Active => suppliers.Where(s => s.ArchivedAtUtc == null),
            StatusFilter.Archived => suppliers.Where(s => s.ArchivedAtUtc != null),
            _ => suppliers,
        };
        if (query.CountryCode is { } country)
        {
            suppliers = suppliers.Where(s => s.CountryCode == country);
        }

        foreach (var word in (query.Search ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Take(5))
        {
            var pattern = Like(word);
            suppliers = suppliers.Where(s => s.CodeKey.Contains(word) || EF.Functions.ILike(s.Name, pattern) || (s.City != null && EF.Functions.ILike(s.City, pattern)));
        }

        var total = await suppliers.CountAsync(cancellationToken);
        var ordered = query.Descending ? suppliers.OrderByDescending(s => s.Name).ThenByDescending(s => s.Id) : suppliers.OrderBy(s => s.Name).ThenBy(s => s.Id);
        var page = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(s => new
            {
                Supplier = s,
                Variants = context.VariantSuppliers.Count(l => l.SupplierId == s.Id && l.RemovedAtUtc == null),
                Primary = context.VariantSuppliers.Count(l => l.SupplierId == s.Id && l.IsPrimary),
            })
            .ToListAsync(cancellationToken);
        return new Page<SupplierRowView>([.. page.Select(p => new SupplierRowView(p.Supplier, p.Variants, p.Primary))], query.Page, query.PageSize, total);
    }

    public Task<Domain.Suppliers.Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

    public async Task<IReadOnlyList<SupplierVariantView>> SupplierVariantsAsync(Guid supplierId, CancellationToken cancellationToken) =>
        [.. (await (from l in context.VariantSuppliers.AsNoTracking()
                    join v in context.ProductVariants.AsNoTracking() on l.VariantId equals v.Id
                    join m in context.ProductModels.AsNoTracking() on v.ModelId equals m.Id
                    where l.SupplierId == supplierId && l.RemovedAtUtc == null
                    orderby v.Sku
                    select new { l, m.Id, v.Sku, v.Label, m.Name, v.StockPolicyCode, Archived = v.ArchivedAtUtc != null || m.ArchivedAtUtc != null })
                .Take(1000).ToListAsync(cancellationToken))
            .Select(x => new SupplierVariantView(x.l, x.Id, x.Sku, x.Label, x.Name, StockPolicies.FromCode(x.StockPolicyCode), x.Archived))];

    public async Task<Page<BalanceRowView>> ListBalancesAsync(BalanceQuery query, CancellationToken cancellationToken)
    {
        var locations = await context.Locations.AsNoTracking()
            .Where(l => l.StoreId == query.StoreId && l.ArchivedAtUtc == null && (query.LocationId == null || l.Id == query.LocationId)).ToListAsync(cancellationToken);
        var variants = from v in context.ProductVariants.AsNoTracking()
                       join m in context.ProductModels.AsNoTracking() on v.ModelId equals m.Id
                       where v.StockPolicyCode == StockPolicies.Stocked && v.ArchivedAtUtc == null && m.ArchivedAtUtc == null
                       select new { v, m };
        if (query.FamilyCode is { } family)
        {
            variants = variants.Where(x => x.m.FamilyCode == family);
        }

        foreach (var word in (query.Search ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Take(5))
        {
            var pattern = Like(word);
            variants = variants.Where(x => x.v.SkuKey.Contains(word) || EF.Functions.ILike(x.v.Label, pattern) || EF.Functions.ILike(x.m.Name, pattern)
                || (x.m.Brand != null && EF.Functions.ILike(x.m.Brand, pattern)) || (x.v.Barcode != null && x.v.Barcode.Contains(word)));
        }

        var list = await variants.Select(x => new { x.v.Id, ModelId = x.m.Id, x.v.Sku, x.v.Label, x.m.Name, x.m.Brand, x.m.FamilyCode }).ToListAsync(cancellationToken);
        var ids = list.Select(v => v.Id).ToList();
        var locationIds = locations.Select(l => l.Id).ToList();
        var balances = (await context.StockBalances.AsNoTracking().Where(b => ids.Contains(b.VariantId) && locationIds.Contains(b.LocationId)).ToListAsync(cancellationToken))
            .ToDictionary(b => (b.VariantId, b.LocationId));
        var settings = (await context.VariantStoreSettings.AsNoTracking().Where(s => s.StoreId == query.StoreId && ids.Contains(s.VariantId)).ToListAsync(cancellationToken))
            .ToDictionary(s => s.VariantId);
        var rows = new List<BalanceRowView>();
        foreach (var variant in list)
        {
            foreach (var location in locations)
            {
                var balance = balances.GetValueOrDefault((variant.Id, location.Id));
                var setting = settings.GetValueOrDefault(variant.Id);
                var onHand = balance?.OnHand ?? 0;
                var reserved = balance?.Reserved ?? 0;
                rows.Add(new BalanceRowView(
                    variant.Id, variant.ModelId, variant.Sku, variant.Label, variant.Name, variant.Brand, variant.FamilyCode, location.Id, location.Code, onHand, reserved,
                    setting?.LowStockThreshold, Availabilities.Compute(StockPolicy.Stocked, false, false, onHand - reserved, setting?.LowStockThreshold),
                    balance?.Version, setting?.Version));
            }
        }

        IEnumerable<BalanceRowView> filtered = rows;
        if (query.Availability is { } availability)
        {
            filtered = filtered.Where(r => r.Availability == availability);
        }

        var all = filtered.ToList();
        IEnumerable<BalanceRowView> ordered = query.Sort switch
        {
            VariantSort.Sku => all.OrderBy(r => r.Sku, StringComparer.OrdinalIgnoreCase),
            VariantSort.Stock => all.OrderBy(r => r.Available).ThenBy(r => r.Sku, StringComparer.OrdinalIgnoreCase),
            VariantSort.Family => all.OrderBy(r => r.FamilyCode, StringComparer.Ordinal).ThenBy(r => r.ModelName, NameOrder),
            _ => all.OrderBy(r => r.ModelName, NameOrder).ThenBy(r => r.Label, NameOrder).ThenBy(r => r.LocationCode, StringComparer.Ordinal),
        };
        if (query.Descending)
        {
            ordered = ordered.Reverse();
        }

        return new Page<BalanceRowView>([.. ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)], query.Page, query.PageSize, all.Count);
    }

    public async Task<IReadOnlyList<VariantRowView>> AlertsAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var page = await ListVariantsAsync(
            new VariantListQuery(storeId, Policy: StockPolicy.Stocked, PageSize: int.MaxValue / 2, Sort: VariantSort.Stock), cancellationToken);
        return [.. page.Items.Where(v => v.Availability is Availability.LowStock or Availability.OutOfStock)];
    }

    public async Task<Page<MovementView>> ListMovementsAsync(MovementQuery query, CancellationToken cancellationToken)
    {
        var locationIds = await context.Locations.AsNoTracking().Where(l => l.StoreId == query.StoreId).Select(l => l.Id).ToListAsync(cancellationToken);
        var movements = context.StockMovements.AsNoTracking().Where(m => locationIds.Contains(m.LocationId));
        if (query.LocationId is { } location)
        {
            movements = movements.Where(m => m.LocationId == location);
        }

        if (query.VariantId is { } variant)
        {
            movements = movements.Where(m => m.VariantId == variant);
        }

        if (query.MovementType is { } type)
        {
            movements = movements.Where(m => m.MovementType == type);
        }

        if (query.FromUtc is { } from)
        {
            movements = movements.Where(m => m.PerformedAtUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            movements = movements.Where(m => m.PerformedAtUtc < to);
        }

        var total = await movements.CountAsync(cancellationToken);
        var page = await movements.OrderByDescending(m => m.Seq).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new Page<MovementView>(await DescribeAsync(page, cancellationToken), query.Page, query.PageSize, total);
    }

    public async Task<MovementView?> GetMovementAsync(Guid movementId, CancellationToken cancellationToken)
    {
        var movement = await context.StockMovements.AsNoTracking().SingleOrDefaultAsync(m => m.Id == movementId, cancellationToken);
        return movement is null ? null : (await DescribeAsync([movement], cancellationToken))[0];
    }

    public async Task<Page<ReceiptSummaryView>> ListReceiptsAsync(Guid storeId, Guid? supplierId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var receipts = context.Receipts.AsNoTracking().Include(r => r.Lines).Where(r => r.StoreId == storeId && (supplierId == null || r.SupplierId == supplierId));
        var total = await receipts.CountAsync(cancellationToken);
        var items = await receipts.OrderByDescending(r => r.ReceivedAtUtc).ThenByDescending(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return new Page<ReceiptSummaryView>(await SummariesAsync(items, cancellationToken), page, pageSize, total);
    }

    public async Task<ReceiptDetailView?> GetReceiptAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await context.Receipts.AsNoTracking().Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var summary = (await SummariesAsync([receipt], cancellationToken))[0];
        var variantIds = receipt.Lines.Select(l => l.VariantId).ToList();
        var names = await context.ProductVariants.AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => (v.Sku, v.Label), cancellationToken);
        var outstanding = await OutstandingAsync(receipt.CompletesReceiptId ?? receipt.Id, cancellationToken);
        return new ReceiptDetailView(
            summary,
            [.. receipt.Lines.OrderBy(l => l.LineNo).Select(l => new ReceiptLineView(l, names[l.VariantId].Sku, names[l.VariantId].Label))],
            outstanding);
    }

    public async Task<Page<InventorySummaryView>> ListInventoriesAsync(Guid? storeId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var sessions = context.InventorySessions.AsNoTracking().Where(s => (storeId == null || s.StoreId == storeId) && (status == null || s.Status == status));
        var total = await sessions.CountAsync(cancellationToken);
        var items = await sessions.OrderByDescending(s => s.OpenedAtUtc).ThenByDescending(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                Session = s,
                Lines = context.InventoryLines.Count(l => l.SessionId == s.Id),
                Counted = context.InventoryLines.Count(l => l.SessionId == s.Id && l.CountedQuantity != null),
                Differences = context.InventoryLines.Count(l => l.SessionId == s.Id && l.CountedQuantity != null && l.CountedQuantity != l.ExpectedAtCount),
            })
            .ToListAsync(cancellationToken);
        var names = await DisplayNamesAsync([.. items.Select(i => i.Session.OpenedByUserId)], cancellationToken);
        return new Page<InventorySummaryView>(
            [.. items.Select(i => new InventorySummaryView(i.Session, names.GetValueOrDefault(i.Session.OpenedByUserId, "Compte inconnu"), i.Lines, i.Counted, i.Differences))],
            page, pageSize, total);
    }

    public async Task<InventoryDetailView?> GetInventoryAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await context.InventorySessions.AsNoTracking().Include(s => s.Lines).AsSplitQuery().SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var variantIds = session.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await (from v in context.ProductVariants.AsNoTracking()
                              join m in context.ProductModels.AsNoTracking() on v.ModelId equals m.Id
                              where variantIds.Contains(v.Id)
                              select new { v.Id, v.Sku, v.Label, m.Name }).ToDictionaryAsync(v => v.Id, cancellationToken);
        var locations = await context.Locations.AsNoTracking().Where(l => session.LocationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        var balances = (await context.StockBalances.AsNoTracking().Where(b => variantIds.Contains(b.VariantId)).ToListAsync(cancellationToken))
            .ToDictionary(b => (b.VariantId, b.LocationId), b => b.OnHand);
        var names = await DisplayNamesAsync([session.OpenedByUserId, .. session.Lines.Where(l => l.CountedByUserId != null).Select(l => l.CountedByUserId!.Value)], cancellationToken);
        var lines = session.Lines.OrderBy(l => variants[l.VariantId].Name, NameOrder).ThenBy(l => variants[l.VariantId].Label, NameOrder).ThenBy(l => locations.GetValueOrDefault(l.LocationId))
            .Select(l => new InventoryLineView(
                l, variants[l.VariantId].Sku, variants[l.VariantId].Label, variants[l.VariantId].Name, locations.GetValueOrDefault(l.LocationId, "?"),
                balances.GetValueOrDefault((l.VariantId, l.LocationId)), l.CountedByUserId is { } c ? names.GetValueOrDefault(c) : null))
            .ToList();
        var summary = new InventorySummaryView(
            session, names.GetValueOrDefault(session.OpenedByUserId, "Compte inconnu"), lines.Count, lines.Count(l => l.Line.CountedQuantity is not null),
            lines.Count(l => l.Line.Difference is not (null or 0)));
        return new InventoryDetailView(summary, lines);
    }

    public async Task<IReadOnlyList<ImportBatchView>> ListImportsAsync(int limit, CancellationToken cancellationToken)
    {
        var batches = await context.ImportBatches.AsNoTracking().OrderByDescending(b => b.UploadedAtUtc).Take(limit).ToListAsync(cancellationToken);
        var names = await DisplayNamesAsync([.. batches.Select(b => b.UploadedByUserId)], cancellationToken);
        return [.. batches.Select(b => new ImportBatchView(b, names.GetValueOrDefault(b.UploadedByUserId, "Compte inconnu")))];
    }

    public async Task<IReadOnlyList<AuditEvent>> EventsAsync(string moduleCode, string entityType, Guid entityId, CancellationToken cancellationToken) =>
        await context.AuditEvents.AsNoTracking()
            .Where(e => e.Module == moduleCode && e.EntityType == entityType && e.EntityId == entityId)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Take(200).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
    }

    // ---------- outils ----------

    /// <summary>Motif ILIKE « contient » (caractères spéciaux échappés).</summary>
    private static string Like(string word) => "%" + word.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";

    /// <summary>Prix, stock agrégé sur les emplacements ACTIFS du magasin, seuil, fournisseur principal ACTIF, disponibilité.</summary>
    private async Task<List<VariantRowView>> EnrichAsync(List<VariantBase> rows, Store shop, CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.VariantId).ToList();
        var prices = (await context.VariantStorePrices.AsNoTracking().Where(p => p.StoreId == shop.Id && ids.Contains(p.VariantId)).ToListAsync(cancellationToken))
            .ToDictionary(p => p.VariantId);
        var locationIds = await context.Locations.AsNoTracking().Where(l => l.StoreId == shop.Id && l.ArchivedAtUtc == null).Select(l => l.Id).ToListAsync(cancellationToken);
        var stock = (await context.StockBalances.AsNoTracking().Where(b => ids.Contains(b.VariantId) && locationIds.Contains(b.LocationId))
                .GroupBy(b => b.VariantId).Select(g => new { VariantId = g.Key, OnHand = g.Sum(b => b.OnHand), Reserved = g.Sum(b => b.Reserved) })
                .ToListAsync(cancellationToken))
            .ToDictionary(s => s.VariantId);
        var thresholds = (await context.VariantStoreSettings.AsNoTracking().Where(s => s.StoreId == shop.Id && ids.Contains(s.VariantId)).ToListAsync(cancellationToken))
            .ToDictionary(s => s.VariantId, s => s.LowStockThreshold);
        var primaries = (await (from l in context.VariantSuppliers.AsNoTracking()
                                join s in context.Suppliers.AsNoTracking() on l.SupplierId equals s.Id
                                where ids.Contains(l.VariantId) && l.IsPrimary && s.ArchivedAtUtc == null
                                select new { l.VariantId, s.Id, s.Name, l.LeadTimeDays }).ToListAsync(cancellationToken))
            .ToDictionary(p => p.VariantId);
        return
        [
            .. rows.Select(r =>
            {
                var policy = StockPolicies.FromCode(r.Policy);
                var primary = primaries.GetValueOrDefault(r.VariantId);
                var incomplete = policy == StockPolicy.OrderOnly && !r.Archived && primary is null;
                var totals = stock.GetValueOrDefault(r.VariantId);
                var onHand = policy == StockPolicy.Stocked ? totals?.OnHand ?? 0 : 0;
                var reserved = policy == StockPolicy.Stocked ? totals?.Reserved ?? 0 : 0;
                var threshold = thresholds.GetValueOrDefault(r.VariantId);
                return new VariantRowView(
                    r.VariantId, r.ModelId, r.Sku, r.Label, r.ModelName, r.Brand, r.FamilyCode, policy, r.Barcode, r.ManufacturerReference, r.Archived, r.ModelArchived,
                    incomplete, primary?.Id, primary?.Name, primary?.LeadTimeDays, prices.GetValueOrDefault(r.VariantId), shop.CurrencyCode, onHand, reserved, threshold,
                    Availabilities.Compute(policy, r.Archived || r.ModelArchived, incomplete, onHand - reserved, threshold), r.Version, r.UpdatedAtUtc, r.TaxCategoryCode);
            }),
        ];
    }

    private static List<VariantRowView> Order(List<VariantRowView> rows, VariantSort sort, bool descending)
    {
        IOrderedEnumerable<VariantRowView> ordered = sort switch
        {
            VariantSort.Sku => rows.OrderBy(r => r.Sku, StringComparer.OrdinalIgnoreCase),
            VariantSort.Family => rows.OrderBy(r => r.FamilyCode, StringComparer.Ordinal).ThenBy(r => r.ModelName, NameOrder),
            VariantSort.Updated => rows.OrderBy(r => r.UpdatedAtUtc),
            VariantSort.Price => rows.OrderBy(r => r.Price?.SalePriceHt is null).ThenBy(r => r.Price?.SalePriceHt),
            VariantSort.Stock => rows.OrderBy(r => r.Available),
            _ => rows.OrderBy(r => r.ModelName, NameOrder).ThenBy(r => r.Label, NameOrder),
        };
        var result = ordered.ThenBy(r => r.Sku, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.VariantId).ToList();
        if (descending)
        {
            result.Reverse();
        }

        return result;
    }

    private async Task<List<MovementView>> DescribeAsync(List<StockMovement> movements, CancellationToken cancellationToken)
    {
        var variantIds = movements.Select(m => m.VariantId).Distinct().ToList();
        var locationIds = movements.Select(m => m.LocationId).Distinct().ToList();
        var supplierIds = movements.Where(m => m.SupplierId != null).Select(m => m.SupplierId!.Value).Distinct().ToList();
        var sourceIds = movements.Where(m => m.SourceId != null).Select(m => m.SourceId!.Value).Distinct().ToList();
        var variants = await context.ProductVariants.AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => (v.Sku, v.Label), cancellationToken);
        var locations = await context.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        var suppliers = await context.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var receipts = await context.Receipts.AsNoTracking().Where(r => sourceIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Number, cancellationToken);
        var sessions = await context.InventorySessions.AsNoTracking().Where(s => sourceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Number, cancellationToken);
        var names = await DisplayNamesAsync([.. movements.Select(m => m.PerformedByUserId)], cancellationToken);
        return
        [
            .. movements.Select(m => new MovementView(
                m, variants[m.VariantId].Sku, variants[m.VariantId].Label, locations.GetValueOrDefault(m.LocationId, "?"),
                names.GetValueOrDefault(m.PerformedByUserId, "Compte inconnu"), m.SupplierId is { } s ? suppliers.GetValueOrDefault(s) : null,
                m.SourceId is { } source ? receipts.GetValueOrDefault(source) ?? sessions.GetValueOrDefault(source) : null)),
        ];
    }

    private async Task<List<ReceiptSummaryView>> SummariesAsync(List<Receipt> receipts, CancellationToken cancellationToken)
    {
        var supplierIds = receipts.Select(r => r.SupplierId).Distinct().ToList();
        var locationIds = receipts.Select(r => r.LocationId).Distinct().ToList();
        var suppliers = await context.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var locations = await context.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        var names = await DisplayNamesAsync([.. receipts.Select(r => r.ReceivedByUserId)], cancellationToken);
        var result = new List<ReceiptSummaryView>();
        foreach (var receipt in receipts)
        {
            int? outstanding = null;
            if (receipt.CompletesReceiptId is null && receipt.Lines.Any(l => l.AnnouncedQuantity is not null))
            {
                outstanding = (await OutstandingAsync(receipt.Id, cancellationToken)).Sum(o => Math.Max(0, o.Announced - o.Received));
            }

            result.Add(new ReceiptSummaryView(
                receipt, suppliers.GetValueOrDefault(receipt.SupplierId, "?"), locations.GetValueOrDefault(receipt.LocationId, "?"),
                names.GetValueOrDefault(receipt.ReceivedByUserId, "Compte inconnu"), receipt.Lines.Count, receipt.Lines.Sum(l => l.ReceivedQuantity), outstanding));
        }

        return result;
    }

    /// <summary>Reliquat d'une réception partielle : annoncé sur la réception d'origine − reçu sur toute la chaîne, par variante.</summary>
    private async Task<IReadOnlyList<(Guid VariantId, int Announced, int Received)>> OutstandingAsync(Guid rootId, CancellationToken cancellationToken)
    {
        var lines = await context.Set<ReceiptLine>().AsNoTracking()
            .Where(l => l.ReceiptId == rootId || context.Receipts.Any(r => r.Id == l.ReceiptId && r.CompletesReceiptId == rootId))
            .ToListAsync(cancellationToken);
        return
        [
            .. lines.Where(l => l.ReceiptId == rootId && l.AnnouncedQuantity is not null)
                .Select(root => (root.VariantId, root.AnnouncedQuantity!.Value, lines.Where(l => l.VariantId == root.VariantId).Sum(l => l.ReceivedQuantity))),
        ];
    }
}
