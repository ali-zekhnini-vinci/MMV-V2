using MMV.Domain.Catalog;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;

namespace MMV.Application.Catalog;

/// <summary>
/// Variante vendable telle que la Phase 06 la COPIERA sur une ligne de vente (non-rétroactivité, D-05-16) : aucune
/// valeur de coût. <see cref="SalePriceTtc"/> n'existe que si la fiscalité du magasin et la catégorie sont validées
/// (ADR-010) ; sinon <see cref="TtcStatus"/> = <c>tax_not_validated</c> et toute vente réelle reste refusée.
/// </summary>
public sealed record SellableVariant(
    Guid VariantId,
    string Sku,
    string Label,
    string FamilyCode,
    StockPolicy Policy,
    bool IsIncomplete,
    decimal? SalePriceHt,
    PricingMode PricingMode,
    string Currency,
    string? TaxCategoryCode,
    decimal? TaxRate,
    decimal? SalePriceTtc,
    string TtcStatus,
    Availability Availability,
    int? AvailableQuantity,
    bool IsArchived,
    uint Version);

/// <summary>Lecture pour le futur panier (Phase 06) — implémentée et testée en Phase 05, appelée par aucune vente.</summary>
public interface ICatalogQuery
{
    Task<IReadOnlyList<SellableVariant>> GetSellableVariantsAsync(Guid storeId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);
}

/// <summary>Document source d'un mouvement (vente, commande, réception rapprochée…).</summary>
public sealed record StockSource(string SourceType, Guid SourceId);

/// <summary>Ligne de document : quantité positive à sortir (vente) ou à entrer (annulation).</summary>
public sealed record StockLine(Guid VariantId, Guid LocationId, int Quantity);

public sealed record StockApplyResult(bool Applied, IReadOnlyList<(Guid VariantId, Guid LocationId, string Error)> Errors);

/// <summary>
/// CONTRAT SEULEMENT (Phase 06, R-05-01) : écritures de stock d'un document DANS LA TRANSACTION DE L'APPELANT, tout ou
/// rien, verrous dans l'ordre croissant (variante, emplacement) — le même ordre que <see cref="StockService"/> et
/// <see cref="InventoryService"/>. Lignes <c>order_only</c> / <c>service</c> ignorées. Erreurs par ligne :
/// <c>stock_insufficient</c>, <c>stock_reserved</c>, <c>archived</c>, <c>not_stocked</c>. Aucune implémentation en Phase 05 :
/// les types de mouvement <c>sale</c> / <c>sale_reversal</c> exigent une extension revue de la contrainte SQL.
/// </summary>
public interface IStockLedger
{
    Task<StockApplyResult> ApplyDocumentMovementsAsync(StockSource source, IReadOnlyList<StockLine> lines, Guid requestId, CancellationToken cancellationToken);

    Task<StockApplyResult> ReverseDocumentMovementsAsync(StockSource source, string reasonCode, Guid requestId, CancellationToken cancellationToken);
}

/// <summary>
/// CONTRAT SEULEMENT (D-05-17, Q09) : réservation créée à la VALIDATION d'une vente ou d'une commande client, jamais au
/// devis ; variantes <c>stocked</c> seulement ; <c>disponible = en main − réservé</c> (colonne <c>stock.balances.reserved</c>,
/// toujours 0 en Phase 05). Implémentation et table <c>stock.reservations</c> : Phase 06.
/// </summary>
public interface IStockReservations
{
    Task ReserveAsync(StockSource validatedDocument, IReadOnlyList<StockLine> lines, Guid requestId, CancellationToken cancellationToken);

    Task ReleaseAsync(StockSource source, Guid requestId, CancellationToken cancellationToken);

    Task ConsumeAsync(StockSource source, Guid requestId, CancellationToken cancellationToken);
}

/// <summary>Implémentation de <see cref="ICatalogQuery"/> sur les lectures du catalogue (prix, fiscalité validée, disponibilité).</summary>
public sealed class CatalogQuery(ICatalogReadStore reads, ICatalogStore store, IPricingStore pricing) : ICatalogQuery
{
    public async Task<IReadOnlyList<SellableVariant>> GetSellableVariantsAsync(Guid storeId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        var shop = await store.GetStoreAsync(storeId, cancellationToken);
        if (shop is null || variantIds.Count == 0)
        {
            return [];
        }

        var result = new List<SellableVariant>();
        foreach (var id in variantIds.Distinct())
        {
            if (await reads.GetVariantAsync(id, storeId, cancellationToken) is not { } detail)
            {
                continue;
            }

            var row = detail.Row;
            var category = detail.Variant.TaxCategoryCode is { } code ? await pricing.TaxCategoryAsync(storeId, code, cancellationToken) : null;
            var (ttc, status) = TaxCalculator.Compute(row.Price?.SalePriceHt, shop.IsFiscalProfileValidated, category);
            result.Add(new SellableVariant(
                row.VariantId, row.Sku, row.Label, row.FamilyCode, row.Policy, row.IsIncomplete, row.Price?.SalePriceHt, row.Price?.Mode ?? PricingMode.Coefficient,
                shop.CurrencyCode, detail.Variant.TaxCategoryCode, category is { IsValidated: true } ? category.Rate : null, ttc, status, row.Availability,
                row.Policy == StockPolicy.Stocked ? row.Available : null, row.IsArchived || row.IsModelArchived, row.Version));
        }

        return result;
    }
}
