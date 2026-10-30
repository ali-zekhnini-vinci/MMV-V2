using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stores;

namespace MMV.Application.Catalog;

/// <summary>Aperçu de prix (calcul pur, aucune écriture).</summary>
public sealed record PricePreview(decimal PurchasePriceHt, decimal Coefficient, decimal ComputedSalePriceHt, string Currency, bool BelowCost);

/// <summary>Élément d'un recalcul par lot : prix stocké, prix calculé, version de la ligne de tarif.</summary>
public sealed record RecalculationItem(Guid VariantId, decimal? CurrentSalePriceHt, decimal ComputedSalePriceHt, uint Version);

/// <summary>Élément confirmé d'un recalcul par lot (liste EXACTE de l'aperçu).</summary>
public sealed record RecalculationConfirmation(Guid VariantId, uint Version, decimal ExpectedSalePriceHt);

/// <summary>
/// Tarifs par (variante, magasin) — D-05-16 : prix de vente HT = arrondi(prix d'achat HT × coefficient), UN arrondi au
/// centime, demi vers l'extérieur ; aperçu serveur et enregistrement par la MÊME fonction ; recalcul jamais silencieux
/// (prix aperçu exigé, <c>409 price_preview_stale</c> sinon) ; prix manuel par <c>pricing.manual</c> ; historique de chaque
/// champ modifié ; audit sans aucune valeur de coût. Aucune conversion de devise : la devise est celle du magasin.
/// Ordre des verrous : magasin (FOR SHARE) → variante (FOR SHARE) → tarif (FOR UPDATE).
/// </summary>
public sealed class PricingService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IPricingStore pricing, TimeProvider clock, IAuditRecorder audit)
{
    /// <summary>Aperçu : aucune écriture. Erreurs de champ si les montants sont invalides.</summary>
    public async Task<Outcome<PricePreview>> PreviewAsync(Guid storeId, string? purchasePriceHt, string? coefficient, CancellationToken cancellationToken)
    {
        var shop = await store.GetStoreAsync(storeId, cancellationToken);
        if (shop is null)
        {
            return Outcome.Missing<PricePreview>("store_not_found");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var purchase = Purchase(purchasePriceHt, errors, required: true);
        var coef = Coefficient(coefficient, errors);
        return errors.Count > 0
            ? Outcome.Invalid<PricePreview>(errors)
            : Outcome.Ok(new PricePreview(purchase!.Value, coef!.Value, SalePriceCalculator.Compute(purchase.Value, coef.Value), shop.CurrencyCode, coef < 1));
    }

    /// <summary>
    /// Enregistre coefficient (et prix d'achat s'il est fourni, <c>costs.write</c> exigé s'il change) et le prix calculé,
    /// passe en mode coefficient. <paramref name="expectedSalePriceHt"/> : prix montré par l'aperçu ; différent du calcul
    /// serveur → 409 <c>price_preview_stale</c>, rien n'est écrit.
    /// </summary>
    public async Task<Outcome<Guid>> SetCoefficientPriceAsync(
        Guid variantId, Guid storeId, string? currency, string? purchasePriceHt, string? coefficient, string? expectedSalePriceHt, bool acceptBelowCost,
        uint? version, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var purchaseInput = Purchase(purchasePriceHt, errors, required: false);
        var coef = Coefficient(coefficient, errors);
        var expected = Sale(expectedSalePriceHt, "expectedSalePriceHt", errors);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (price, refusal, replayed) = await LockAsync(variantId, storeId, currency, version, requestId, actor.UserId, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        if (replayed)
        {
            return Outcome.Ok<Guid>(variantId, replayed: true);
        }

        var purchase = purchaseInput ?? price!.PurchasePriceHt;
        if (purchaseInput is { } newPurchase && newPurchase != price!.PurchasePriceHt && !actor.Has(Permissions.CostsWrite))
        {
            return Outcome.Forbidden<Guid>("costs_forbidden");
        }

        if (purchase is null)
        {
            return Outcome.Invalid<Guid>("purchasePriceHt", CatalogFieldErrors.Required);
        }

        if (coef < 1 && !acceptBelowCost)
        {
            return Outcome.Refuse<Guid>("coefficient_below_one_unconfirmed");
        }

        var computed = SalePriceCalculator.Compute(purchase.Value, coef!.Value);
        if (computed != expected)
        {
            return Outcome.Conflict<Guid>("price_preview_stale", new { computedSalePriceHt = MoneyText.Format(computed, MoneyText.SaleScale) });
        }

        var before = Snapshot(price!);
        if (before.Purchase == purchase && before.Coefficient == coef && before.Mode == PricingModes.Coefficient && before.Sale == computed)
        {
            return Outcome.Ok<Guid>(variantId);
        }

        var now = CatalogClock.Now(clock);
        price!.SetCoefficientPrice(purchase, coef.Value, computed, actor.UserId, now, requestId);
        var fields = History(price, before, PriceOrigins.Edit, null, actor.UserId, now, requestId);
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.PricingModule, AuditActions.PriceChanged, AuditActions.VariantEntity, variantId, fields);
        return await CommitAsync(transaction, variantId, cancellationToken);
    }

    /// <summary>Prix d'achat HT seul (<c>costs.write</c>) : le prix de vente NE CHANGE PAS ; en mode coefficient il passe en « recalcul en attente ».</summary>
    public async Task<Outcome<Guid>> SetPurchasePriceAsync(
        Guid variantId, Guid storeId, string? currency, string? purchasePriceHt, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var purchase = Purchase(purchasePriceHt, errors, required: true);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (price, refusal, replayed) = await LockAsync(variantId, storeId, currency, version, requestId, userId, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        if (replayed)
        {
            return Outcome.Ok<Guid>(variantId, replayed: true);
        }

        if (price!.PurchasePriceHt == purchase)
        {
            return Outcome.Ok<Guid>(variantId);
        }

        var before = Snapshot(price);
        var now = CatalogClock.Now(clock);
        price.SetPurchasePrice(purchase, userId, now, requestId);
        var fields = History(price, before, PriceOrigins.Edit, null, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.PricingModule, AuditActions.PricePurchaseChanged, AuditActions.VariantEntity, variantId, fields);
        return await CommitAsync(transaction, variantId, cancellationToken);
    }

    /// <summary>Recalcul explicite d'une variante (mode coefficient) : applique le prix calculé montré par l'aperçu.</summary>
    public async Task<Outcome<Guid>> RecalculateAsync(
        Guid variantId, Guid storeId, string? currency, string? expectedSalePriceHt, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var expected = Sale(expectedSalePriceHt, "expectedSalePriceHt", errors);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (price, refusal, replayed) = await LockAsync(variantId, storeId, currency, version, requestId, userId, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        if (replayed)
        {
            return Outcome.Ok<Guid>(variantId, replayed: true);
        }

        if (price!.Mode != PricingMode.Coefficient || price.ComputedSalePriceHt is not { } computed)
        {
            return Outcome.Refuse<Guid>("recalculation_not_applicable");
        }

        if (computed != expected)
        {
            return Outcome.Conflict<Guid>("price_preview_stale", new { computedSalePriceHt = MoneyText.Format(computed, MoneyText.SaleScale) });
        }

        if (price.SalePriceHt == computed)
        {
            return Outcome.Ok<Guid>(variantId);
        }

        var before = Snapshot(price);
        var now = CatalogClock.Now(clock);
        price.SetCoefficientPrice(price.PurchasePriceHt, price.Coefficient!.Value, computed, userId, now, requestId);
        var fields = History(price, before, PriceOrigins.Recalculation, null, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.PricingModule, AuditActions.PriceRecalculated, AuditActions.VariantEntity, variantId, fields);
        return await CommitAsync(transaction, variantId, cancellationToken);
    }

    /// <summary>Prix de vente HT manuel (<c>pricing.manual</c>) : le coefficient enregistré est conservé, non appliqué.</summary>
    public async Task<Outcome<Guid>> SetManualPriceAsync(
        Guid variantId, Guid storeId, string? currency, string? salePriceHt, string? reason, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var sale = Sale(salePriceHt, "salePriceHt", errors);
        var text = CatalogText.Clean(reason, "reason", Domain.Catalog.CatalogLimits.ReasonMaxLength, required: false, errors, multiline: true);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (price, refusal, replayed) = await LockAsync(variantId, storeId, currency, version, requestId, userId, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        if (replayed)
        {
            return Outcome.Ok<Guid>(variantId, replayed: true);
        }

        if (price!.Mode == PricingMode.Manual && price.SalePriceHt == sale)
        {
            return Outcome.Ok<Guid>(variantId);
        }

        var before = Snapshot(price);
        var now = CatalogClock.Now(clock);
        price.SetManualPrice(sale!.Value, userId, now, requestId);
        var fields = History(price, before, PriceOrigins.ManualPrice, text, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.PricingModule, AuditActions.PriceManualSet, AuditActions.VariantEntity, variantId,
            [.. fields, .. text is null ? Array.Empty<AuditChange>() : [CatalogAudit.Field("reason")]]);
        return await CommitAsync(transaction, variantId, cancellationToken);
    }

    /// <summary>Aperçu du recalcul par lot : variantes du magasin en « recalcul en attente » (aucune écriture).</summary>
    public async Task<Outcome<IReadOnlyList<RecalculationItem>>> PreviewRecalculationsAsync(Guid storeId, CancellationToken cancellationToken)
    {
        if (await store.GetStoreAsync(storeId, cancellationToken) is null)
        {
            return Outcome.Missing<IReadOnlyList<RecalculationItem>>("store_not_found");
        }

        var pending = await pricing.PendingRecalculationsAsync(storeId, cancellationToken);
        return Outcome.Ok<IReadOnlyList<RecalculationItem>>(
            [.. pending.Select(p => new RecalculationItem(p.VariantId, p.SalePriceHt, p.ComputedSalePriceHt!.Value, p.Version))]);
    }

    /// <summary>
    /// Applique EXACTEMENT la liste confirmée, tout ou rien : tarifs verrouillés dans l'ordre des variantes ; un élément
    /// modifié depuis l'aperçu (version ou prix calculé) → 409 <c>price_preview_stale</c> avec la liste des éléments périmés.
    /// </summary>
    public async Task<Outcome<int>> ApplyRecalculationsAsync(
        Guid storeId, IReadOnlyList<RecalculationConfirmation> items, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (items.Count == 0 || items.Count > 500 || items.Select(i => i.VariantId).Distinct().Count() != items.Count)
        {
            return Outcome.Invalid<int>("items", items.Count == 0 ? CatalogFieldErrors.Required : CatalogFieldErrors.InvalidFormat);
        }

        if (await pricing.RequestAppliedAsync(requestId, cancellationToken))
        {
            return Outcome.Ok(items.Count, replayed: true);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await store.LockStoreAsync(storeId, cancellationToken))
        {
            return Outcome.Missing<int>("store_not_found");
        }

        var prices = (await pricing.LockPricesAsync(storeId, [.. items.Select(i => i.VariantId)], cancellationToken)).ToDictionary(p => p.VariantId);
        var stale = new List<Guid>();
        foreach (var item in items)
        {
            if (!prices.TryGetValue(item.VariantId, out var price) || price.Version != item.Version || price.Mode != PricingMode.Coefficient
                || price.ComputedSalePriceHt != item.ExpectedSalePriceHt)
            {
                stale.Add(item.VariantId);
            }
        }

        if (stale.Count > 0)
        {
            return Outcome.Conflict<int>("price_preview_stale", new { variantIds = stale });
        }

        var now = CatalogClock.Now(clock);
        var applied = 0;
        foreach (var item in items.OrderBy(i => i.VariantId))
        {
            var price = prices[item.VariantId];
            if (price.SalePriceHt == item.ExpectedSalePriceHt)
            {
                continue;
            }

            var before = Snapshot(price);
            price.SetCoefficientPrice(price.PurchasePriceHt, price.Coefficient!.Value, item.ExpectedSalePriceHt, userId, now, requestId);
            var fields = History(price, before, PriceOrigins.Recalculation, null, userId, now, requestId);
            CatalogAudit.Record(audit, userId, now, AuditActions.PricingModule, AuditActions.PriceRecalculated, AuditActions.VariantEntity, item.VariantId, fields);
            applied++;
        }

        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return CatalogService.UniqueRefusal<int>(commit);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Ok(applied);
    }

    // ---------- outils ----------

    private sealed record PriceSnapshot(decimal? Purchase, decimal? Coefficient, string Mode, decimal? Sale);

    private static PriceSnapshot Snapshot(VariantStorePrice price) => new(price.PurchasePriceHt, price.Coefficient, price.PricingModeCode, price.SalePriceHt);

    /// <summary>
    /// Verrous (magasin, variante, tarif), réémission, version ; magasin ou variante archivés → 409 <c>archived</c> ; devise
    /// annoncée différente de celle du magasin → 422 <c>currency_mismatch</c> (aucune conversion).
    /// </summary>
    private async Task<(VariantStorePrice? Price, Outcome<Guid>? Refusal, bool Replayed)> LockAsync(
        Guid variantId, Guid storeId, string? currency, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await store.LockStoreAsync(storeId, cancellationToken))
        {
            return (null, Outcome.Missing<Guid>("store_not_found"), false);
        }

        var shop = (await store.GetStoreAsync(storeId, cancellationToken))!;
        if (currency is not null && currency != shop.CurrencyCode)
        {
            return (null, Outcome.Refuse<Guid>("currency_mismatch"), false);
        }

        if (!await store.LockVariantAsync(variantId, RowLock.Share, cancellationToken))
        {
            return (null, Outcome.Missing<Guid>(), false);
        }

        var variant = (await store.FindVariantAsync(variantId, cancellationToken))!;
        var (price, created) = await pricing.LockPriceAsync(variantId, storeId, shop.CurrencyCode, userId, cancellationToken);
        if (!created && price.LastRequestId == requestId)
        {
            return (price, null, true);
        }

        if (created ? version is not (null or 0) : version != price.Version)
        {
            return (null, Outcome.Conflict<Guid>(), false);
        }

        if (variant.IsArchived || shop.IsArchived)
        {
            return (null, Outcome.Conflict<Guid>("archived"), false);
        }

        return (price, null, false);
    }

    /// <summary>Une ligne d'historique par champ modifié ; renvoie les champs pour l'audit (noms seulement, mode conservé).</summary>
    private List<AuditChange> History(VariantStorePrice price, PriceSnapshot before, string origin, string? reason, Guid userId, DateTimeOffset now, Guid requestId)
    {
        var fields = new List<AuditChange> { CatalogAudit.Field("storeId", null, price.StoreId.ToString()) };
        void Value(string field, decimal? old, decimal? current)
        {
            if (old != current)
            {
                pricing.Add(PriceHistoryEntry.Value(
                    Guid.CreateVersion7(now), price.VariantId, price.StoreId, null, price.CurrencyCode, field, old, current, origin, reason, userId, now, requestId));
                fields.Add(CatalogAudit.Field(AuditName(field)));
            }
        }

        Value(PriceFields.PurchasePriceHt, before.Purchase, price.PurchasePriceHt);
        Value(PriceFields.Coefficient, before.Coefficient, price.Coefficient);
        if (before.Mode != price.PricingModeCode)
        {
            pricing.Add(PriceHistoryEntry.ModeChange(
                Guid.CreateVersion7(now), price.VariantId, price.StoreId, price.CurrencyCode, before.Mode, price.PricingModeCode, origin, reason, userId, now, requestId));
            fields.Add(CatalogAudit.Field("pricingMode", before.Mode, price.PricingModeCode));
        }

        Value(PriceFields.SalePriceHt, before.Sale, price.SalePriceHt);
        return fields;
    }

    private static string AuditName(string field) => field switch
    {
        PriceFields.PurchasePriceHt => "purchasePriceHt",
        PriceFields.Coefficient => "coefficient",
        PriceFields.SalePriceHt => "salePriceHt",
        _ => field,
    };

    private async Task<Outcome<Guid>> CommitAsync(ITransactionScope transaction, Guid id, CancellationToken cancellationToken)
    {
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return CatalogService.Commit(commit, id);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Ok<Guid>(id);
    }

    internal static decimal? Purchase(string? text, IDictionary<string, string[]> errors, bool required)
    {
        if (text is null)
        {
            if (required)
            {
                errors["purchasePriceHt"] = [CatalogFieldErrors.Required];
            }

            return null;
        }

        var value = MoneyText.TryParse(text, MoneyText.PurchaseScale);
        if (value is null or > MoneyText.MaxPurchasePrice)
        {
            errors["purchasePriceHt"] = [CatalogFieldErrors.InvalidFormat];
            return null;
        }

        return value;
    }

    internal static decimal? Coefficient(string? text, IDictionary<string, string[]> errors)
    {
        var value = text is null ? null : MoneyText.TryParse(text, MoneyText.CoefficientScale);
        if (value is not > 0m || value > MoneyText.MaxCoefficient)
        {
            errors["coefficient"] = [text is null ? CatalogFieldErrors.Required : value is null ? CatalogFieldErrors.InvalidFormat : CatalogFieldErrors.OutOfRange];
            return null;
        }

        return value;
    }

    internal static decimal? Sale(string? text, string field, IDictionary<string, string[]> errors)
    {
        var value = text is null ? null : MoneyText.TryParse(text, MoneyText.SaleScale);
        if (value is null or > SalePriceCalculator.MaxSalePrice)
        {
            errors[field] = [text is null ? CatalogFieldErrors.Required : CatalogFieldErrors.InvalidFormat];
            return null;
        }

        return value;
    }

    /// <summary>Devise d'un magasin (aucune conversion) ; usage : contrôles d'import.</summary>
    public static bool SameCurrency(Store store, string? currency) => currency is null || currency == store.CurrencyCode;
}
