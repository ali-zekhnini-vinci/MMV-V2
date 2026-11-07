using System.Globalization;
using System.Text.Json;
using MMV.Application.Audit;
using MMV.Application.Catalog.Assisted;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Pricing;
using MMV.Domain.Stores;

namespace MMV.Application.Catalog;

/// <summary>Ligne planifiée du nouveau parcours (valeurs d'écriture, issue, erreurs bornées).</summary>
public sealed record AssistedPlanRow(int LineNo, IReadOnlyDictionary<string, string> Values, string Outcome, IReadOnlyList<ImportError> Errors, string? TargetKey);

/// <summary>Avant / après d'un article existant modifié (étape 4).</summary>
public sealed record AssistedChange(int Line, string Sku, string Field, string? Before, string? After);

/// <summary>Plan du nouveau parcours : lignes, droits requis, actions à exécuter dans LA transaction d'application.</summary>
/// <param name="Basis">
/// GATE D (protection contre les plans périmés) : état ACTUEL de chaque champ FOURNI par le fichier pour les articles
/// existants (libellé, références, code-barres, attributs, rattachement fournisseur, tarif du magasin : achat, vente, mode,
/// coefficient, devise), une entrée par ligne. Il entre dans l'empreinte du plan métier comparée entre le contrôle et
/// l'application ; un champ que le fichier ne fournit pas n'y figure pas (sa modification est sans conséquence).
/// </param>
public sealed record AssistedPlan(
    IReadOnlyList<AssistedPlanRow> Rows, bool ContainsCosts, bool ContainsManualPrices, IReadOnlyList<Func<Guid, DateTimeOffset, Guid, CancellationToken, Task>> Actions,
    IReadOnlyList<AssistedChange> Changes, IReadOnlyList<string> Basis);

/// <summary>
/// Nouveau parcours d'import (CSV-IMPORT-AI-01) : réutilise le plan et l'application du catalogue (transaction, idempotence,
/// refus du réimport, droits, audit) sur des lignes MATÉRIALISÉES par le serveur. Les routes actuelles refusent un lot du
/// nouveau parcours (<c>409 pipeline_mismatch</c>) : marqueur <c>@pipeline</c> dans la correspondance du lot.
/// </summary>
public sealed partial class ImportService
{
    /// <summary>Marqueur d'un lot du nouveau parcours (sans dépendance aux tables de 0009).</summary>
    public const string PipelineMarker = "@pipeline";

    public static bool IsAssistedBatch(ImportBatch batch) => batch.MappingJson.Contains("\"" + PipelineMarker + "\"", StringComparison.Ordinal);

    public static string AssistedMapping(int revision) =>
        JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { [PipelineMarker] = AssistantPipelines.AssistantV2, ["@revision"] = revision.ToString(CultureInfo.InvariantCulture) }, Json);

    /// <summary>Plan (contrôle à blanc ou application) des lignes matérialisées du catalogue des produits.</summary>
    public async Task<AssistedPlan> PlanAssistedCatalogAsync(Guid? supplierId, Guid? priceStoreId, IReadOnlyList<MaterializedRow> rows, CancellationToken cancellationToken)
    {
        var supplier = supplierId is { } sid ? await store.FindSupplierAsync(sid, cancellationToken) : null;
        var plan = await PlanCatalogAsync(supplier, [.. rows.Select(r => (r.LineNo, new Dictionary<string, string>(r.Values, StringComparer.Ordinal)))], cancellationToken, priceStoreId);
        var pre = rows.ToDictionary(r => r.LineNo, r => r.PreErrors);
        var planned = plan.Rows.Select(r =>
        {
            var errors = pre.GetValueOrDefault(r.LineNo) is { Count: > 0 } extra ? [.. extra, .. r.Errors] : r.Errors;
            return new AssistedPlanRow(r.LineNo, r.Values, errors.Count > 0 ? ImportOutcomes.Error : r.Outcome, errors, r.TargetKey);
        }).ToList();
        var changes = await ChangesAsync(planned, priceStoreId, cancellationToken);
        var basis = await BasisAsync(planned, priceStoreId, cancellationToken);
        return new AssistedPlan(planned, plan.ContainsCosts, plan.ContainsManualPrices, [.. plan.Actions.Select(a => (Func<Guid, DateTimeOffset, Guid, CancellationToken, Task>)((u, n, q, c) => a(u, n, q, c)))], changes, basis);
    }

    /// <summary>
    /// GATE D : verrous pris dans LA transaction d'application AVANT la relecture du plan, pour qu'aucune écriture concurrente
    /// ne s'intercale entre la relecture et l'écriture. Variantes existantes visées par le plan (mise à jour ou inchangée) en
    /// <c>FOR NO KEY UPDATE</c> (attend les modifications de fiche et les écritures de tarif, qui prennent la variante en
    /// <c>FOR SHARE</c>), puis leurs tarifs du magasin en <c>FOR UPDATE</c> (recalcul groupé). Ordre croissant des
    /// identifiants, comme les autres services (aucun interblocage).
    /// </summary>
    public async Task LockAssistedTargetsAsync(AssistedPlan plan, Guid? priceStoreId, CancellationToken cancellationToken)
    {
        var skus = plan.Rows.Where(r => r.Outcome is ImportOutcomes.Update or ImportOutcomes.Unchanged && r.Values.ContainsKey("sku"))
            .Select(r => CatalogKeys.Normalize(r.Values["sku"])).Distinct(StringComparer.Ordinal).ToList();
        if (skus.Count == 0)
        {
            return;
        }

        var ids = (await imports.VariantsBySkuAsync(skus, cancellationToken)).Values.Select(v => v.Id).Distinct().ToList();
        await store.LockVariantsAsync(ids, RowLock.NoKeyUpdate, cancellationToken);
        if (priceStoreId is { } psid && ids.Count > 0)
        {
            await pricing.LockPricesAsync(psid, ids, cancellationToken);
        }
    }

    /// <summary>État actuel des champs FOURNIS des articles existants visés (voir <see cref="AssistedPlan.Basis"/>).</summary>
    private async Task<List<string>> BasisAsync(List<AssistedPlanRow> rows, Guid? priceStoreId, CancellationToken cancellationToken)
    {
        var existing = rows.Where(r => r.Outcome is ImportOutcomes.Update or ImportOutcomes.Unchanged && r.Values.ContainsKey("sku")).ToList();
        if (existing.Count == 0)
        {
            return [];
        }

        var variants = await imports.VariantsBySkuAsync([.. existing.Select(r => CatalogKeys.Normalize(r.Values["sku"])).Distinct(StringComparer.Ordinal)], cancellationToken);
        var prices = priceStoreId is { } psid && variants.Count > 0
            ? (await pricing.PricesAsync(psid, [.. variants.Values.Select(v => v.Id)], cancellationToken)).ToDictionary(p => p.VariantId)
            : [];
        var basis = new List<string>();
        foreach (var row in existing.OrderBy(r => r.LineNo))
        {
            if (!variants.TryGetValue(CatalogKeys.Normalize(row.Values["sku"]), out var variant))
            {
                basis.Add($"{row.LineNo}|absent");
                continue;
            }

            var state = new SortedDictionary<string, string?>(StringComparer.Ordinal) { ["variant"] = variant.Id.ToString("N") };
            if (row.Values.ContainsKey("variantLabel"))
            {
                state["variantLabel"] = variant.Label;
            }

            if (row.Values.ContainsKey("manufacturerReference"))
            {
                state["manufacturerReference"] = variant.ManufacturerReference;
            }

            if (row.Values.ContainsKey("barcode"))
            {
                state["barcode"] = variant.Barcode;
            }

            foreach (var key in row.Values.Keys.Where(k => k.StartsWith("attr:", StringComparison.Ordinal)).Append(row.Values.ContainsKey("color") ? "attr:color" : null).OfType<string>())
            {
                state[key] = variant.Attributes.GetValueOrDefault(key[5..]);
            }

            if (row.Values.ContainsKey("supplierCode") || row.Values.ContainsKey("supplierReference"))
            {
                var links = await store.LinksForVariantAsync(variant.Id, cancellationToken);
                state["links"] = string.Join(";", links.OrderBy(l => l.SupplierId).Select(l => $"{l.SupplierId:N},{l.SupplierReference},{l.IsPrimary},{l.IsRemoved}"));
            }

            if (row.Values.ContainsKey("price:purchasePriceHt") || row.Values.ContainsKey("price:salePriceHt"))
            {
                var price = prices.GetValueOrDefault(variant.Id);
                state["price"] = price is null ? "none" : string.Join(",",
                    price.PurchasePriceHt?.ToString(CultureInfo.InvariantCulture), price.SalePriceHt?.ToString(CultureInfo.InvariantCulture),
                    price.Coefficient?.ToString(CultureInfo.InvariantCulture), price.PricingModeCode, price.CurrencyCode);
            }

            basis.Add($"{row.LineNo}|{JsonSerializer.Serialize(state, Json)}");
        }

        return basis;
    }

    /// <summary>Avant / après des variantes existantes modifiées (libellé, références, code-barres, attributs, tarif).</summary>
    private async Task<List<AssistedChange>> ChangesAsync(List<AssistedPlanRow> rows, Guid? priceStoreId, CancellationToken cancellationToken)
    {
        var updates = rows.Where(r => r.Outcome == ImportOutcomes.Update && r.Values.ContainsKey("sku")).ToList();
        if (updates.Count == 0)
        {
            return [];
        }

        var variants = await imports.VariantsBySkuAsync([.. updates.Select(r => CatalogKeys.Normalize(r.Values["sku"]))], cancellationToken);
        var prices = priceStoreId is { } psid
            ? (await pricing.PricesAsync(psid, [.. variants.Values.Select(v => v.Id)], cancellationToken)).ToDictionary(p => p.VariantId)
            : [];
        var changes = new List<AssistedChange>();
        foreach (var row in updates)
        {
            if (!variants.TryGetValue(CatalogKeys.Normalize(row.Values["sku"]), out var variant))
            {
                continue;
            }

            void Compare(string field, string? before, string? after)
            {
                if (after is not null && !string.Equals(before, after, StringComparison.Ordinal))
                {
                    changes.Add(new AssistedChange(row.LineNo, variant.Sku, field, before, after));
                }
            }

            Compare("variantLabel", variant.Label, row.Values.GetValueOrDefault("variantLabel"));
            Compare("manufacturerReference", variant.ManufacturerReference, row.Values.GetValueOrDefault("manufacturerReference"));
            Compare("barcode", variant.Barcode, row.Values.GetValueOrDefault("barcode"));
            foreach (var (key, value) in row.Values.Where(v => v.Key.StartsWith("attr:", StringComparison.Ordinal)))
            {
                Compare(key, variant.Attributes.GetValueOrDefault(key[5..]), value);
            }

            if (row.Values.ContainsKey("color"))
            {
                Compare("attr:color", variant.Attributes.GetValueOrDefault("color"), row.Values["color"]);
            }

            var price = prices.GetValueOrDefault(variant.Id);
            Compare("purchasePriceHt", price?.PurchasePriceHt?.ToString(CultureInfo.InvariantCulture), Decimal(row.Values.GetValueOrDefault("price:purchasePriceHt")));
            Compare("salePriceHt", price?.SalePriceHt?.ToString(CultureInfo.InvariantCulture), Decimal(row.Values.GetValueOrDefault("price:salePriceHt")));
        }

        return changes;

        static string? Decimal(string? text) =>
            text is not null && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d.ToString(CultureInfo.InvariantCulture) : null;
    }

    /// <summary>Montants confirmés d'une ligne (chaînes décimales normalisées par le validateur) ; devise du magasin exigée.</summary>
    private static (decimal? Purchase, decimal? Sale) AssistedPrices(Dictionary<string, string> values, Store? shop, Guid? priceStoreId, List<ImportError> errors)
    {
        var purchaseText = values.GetValueOrDefault("price:purchasePriceHt");
        var saleText = values.GetValueOrDefault("price:salePriceHt");
        if (purchaseText is null && saleText is null)
        {
            return (null, null);
        }

        if (priceStoreId is null || shop is null)
        {
            errors.Add(new("price", "store_required"));
            return (null, null);
        }

        if (values.GetValueOrDefault("price:currency") is { } currency && !string.Equals(currency, shop.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            // Aucune conversion de devise.
            errors.Add(new("currency", "currency_mismatch"));
        }

        var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var purchase = purchaseText is not null ? PricingService.Purchase(purchaseText, fieldErrors, required: false) : null;
        var sale = saleText is not null ? PricingService.Sale(saleText, "salePriceHt", fieldErrors) : null;
        errors.AddRange(fieldErrors.Select(e => new ImportError(e.Key, e.Value[0])));
        return (purchase, sale);
    }

    private async Task<bool> AssistedPriceDiffersAsync(Guid variantId, Guid storeId, decimal? purchase, decimal? sale, CancellationToken cancellationToken)
    {
        var found = await pricing.PricesAsync(storeId, [variantId], cancellationToken);
        var current = found.Count > 0 ? found[0] : null;
        return (purchase is not null && purchase != current?.PurchasePriceHt) || (sale is not null && (current?.Mode != PricingMode.Manual || current.SalePriceHt != sale));
    }

    /// <summary>Tarif importé : prix d'achat HT (coût) et / ou prix de vente HT manuel, historisés et audités comme l'import des prix.</summary>
    private ImportAction AssistedPriceAction(Guid variantId, Store shop, decimal? purchase, decimal? sale) => async (userId, now, requestId, ct) =>
    {
        var (price, _) = await pricing.LockPriceAsync(variantId, shop.Id, shop.CurrencyCode, userId, ct);
        var before = (Purchase: price.PurchasePriceHt, Coefficient: price.Coefficient, Mode: price.PricingModeCode, Sale: price.SalePriceHt);
        if (purchase is not null)
        {
            price.SetPurchasePrice(purchase, userId, now, requestId);
        }

        if (sale is { } manualSale)
        {
            price.SetManualPrice(manualSale, userId, now, requestId);
        }

        var fields = new List<AuditChange> { CatalogAudit.Field("storeId", null, shop.Id.ToString()), CatalogAudit.Field("import") };
        void History(string field, string auditName, decimal? old, decimal? value)
        {
            if (old != value)
            {
                pricing.Add(PriceHistoryEntry.Value(Guid.CreateVersion7(now), variantId, shop.Id, null, shop.CurrencyCode, field, old, value, PriceOrigins.Import, null, userId, now, requestId));
                fields.Add(CatalogAudit.Field(auditName));
            }
        }

        History(PriceFields.PurchasePriceHt, "purchasePriceHt", before.Purchase, price.PurchasePriceHt);
        History(PriceFields.Coefficient, "coefficient", before.Coefficient, price.Coefficient);
        if (before.Mode != price.PricingModeCode)
        {
            pricing.Add(PriceHistoryEntry.ModeChange(Guid.CreateVersion7(now), variantId, shop.Id, shop.CurrencyCode, before.Mode, price.PricingModeCode, PriceOrigins.Import, null, userId, now, requestId));
            fields.Add(CatalogAudit.Field("pricingMode", before.Mode, price.PricingModeCode));
        }

        History(PriceFields.SalePriceHt, "salePriceHt", before.Sale, price.SalePriceHt);
        CatalogAudit.Record(audit, userId, now, AuditActions.PricingModule, AuditActions.PriceChanged, AuditActions.VariantEntity, variantId, fields);
    };
}
