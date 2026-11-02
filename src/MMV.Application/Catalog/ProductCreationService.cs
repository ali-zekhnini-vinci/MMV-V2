using System.Security.Cryptography;
using MMV.Domain.Catalog;
using MMV.Domain.Identity;
using MMV.Domain.Stock;

namespace MMV.Application.Catalog;

/// <summary>Rattachement fournisseur prévu par l'assistant (référence, principal, prix fournisseur facultatif).</summary>
public sealed record PlannedSupplierLink(Guid SupplierId, string? SupplierReference, bool IsPrimary, string? SupplierPriceHt, string? SupplierCurrency, int? LeadTimeDays);

/// <summary>Tarif prévu : mode coefficient (prix d'achat × coefficient, aperçu serveur confirmé) ou prix manuel motivé.</summary>
public sealed record PlannedPrice(string Mode, string? PurchasePriceHt, string? Coefficient, string? ExpectedSalePriceHt, bool AcceptBelowCost, string? SalePriceHt, string? Reason);

/// <summary>Stock initial (variante stockée seulement) : emplacement du magasin de l'installation, quantité &gt; 0.</summary>
public sealed record PlannedStock(Guid LocationId, int Quantity);

public sealed record PlannedVariant(VariantInput Variant, IReadOnlyList<PlannedSupplierLink> Suppliers, PlannedPrice? Price, PlannedStock? Stock);

/// <summary>Résultat : modèle et variantes créés (dans l'ordre de la saisie, SKU définitifs).</summary>
public sealed record ProductCreated(Guid ModelId, IReadOnlyList<(Guid VariantId, string Sku)> Variants);

/// <summary>
/// Enregistrement final de l'assistant de création produit (COR-05-06) : modèle, variantes, rattachements fournisseur,
/// tarifs et stock initial dans UNE transaction — les services existants s'y exécutent à l'identique (mêmes règles,
/// mêmes droits, même audit), leurs portées de transaction devenant neutres. Le moindre refus annule TOUT : aucun
/// produit orphelin, aucun mouvement sans produit valide, aucun tarif partiel. Idempotence : la réémission du même
/// <c>requestId</c> renvoie le produit déjà créé (identifiants dérivés et déterministes pour chaque sous-écriture).
/// Erreurs : chemin de champ préfixé par l'étape (<c>variants[1].price.coefficient</c>…).
/// </summary>
public sealed class ProductCreationService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IPricingStore prices, CatalogService catalog, PricingService pricing, StockService stock)
{
    public const string CoefficientMode = "coefficient";
    public const string ManualMode = "manual";

    public async Task<Outcome<ProductCreated>> CreateAsync(
        ModelInput model, IReadOnlyList<PlannedVariant> variants, Guid storeId, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
        if (await store.FindModelByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.CreatedByUserId == actor.UserId
                ? Outcome.Ok(await DescribeAsync(existing.Id, cancellationToken), replayed: true)
                : Outcome.Conflict<ProductCreated>("request_id_reused");
        }

        if (Forbidden(variants, actor) is { } forbidden)
        {
            return Outcome.Forbidden<ProductCreated>(forbidden);
        }

        var shop = await store.GetStoreAsync(storeId, cancellationToken);
        if (shop is null)
        {
            return Outcome.Missing<ProductCreated>("store_not_found");
        }

        var planErrors = PlanErrors(variants, shop.Id, await store.ListLocationsAsync(shop.Id, cancellationToken));
        if (planErrors.Count > 0)
        {
            return Outcome.Invalid<ProductCreated>(planErrors);
        }

        // SKU définitifs AVANT l'écriture : les étapes suivantes retrouvent chaque variante par son SKU.
        var inputs = await catalog.WithGeneratedSkusAsync(model.FamilyCode, [.. variants.Select(v => v.Variant)], cancellationToken);
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var created = await catalog.CreateModelAsync(model, inputs, requestId, actor.UserId, cancellationToken);
        if (!created.IsSuccess)
        {
            return created.As<ProductCreated>();
        }

        if (created.Replayed)
        {
            return Outcome.Ok(await DescribeAsync(created.Value, cancellationToken), replayed: true);
        }

        var bySku = (await DescribeAsync(created.Value, cancellationToken)).Variants.ToDictionary(v => CatalogKeys.Normalize(v.Sku), v => v.VariantId, StringComparer.Ordinal);
        var product = new ProductCreated(created.Value, [.. inputs.Select(v => (bySku[CatalogKeys.Normalize(v.Sku!)], v.Sku!.Trim()))]);
        for (var i = 0; i < variants.Count; i++)
        {
            var variantId = bySku[CatalogKeys.Normalize(inputs[i].Sku!)];
            var plan = variants[i];
            for (var j = 0; j < plan.Suppliers.Count; j++)
            {
                var link = plan.Suppliers[j];
                var priceSpecified = link.SupplierPriceHt is not null || link.SupplierCurrency is not null;
                var linked = await catalog.SetSupplierLinkAsync(
                    variantId, link.SupplierId, new VariantSupplierInput(link.SupplierReference, priceSpecified, link.SupplierPriceHt, link.SupplierCurrency, link.LeadTimeDays, link.IsPrimary),
                    null, Derive(requestId, $"supplier:{i}:{j}"), actor, cancellationToken);
                if (!linked.IsSuccess)
                {
                    return Prefixed(linked, $"variants[{i}].suppliers[{j}].");
                }
            }

            if (plan.Price is { } price)
            {
                var priced = price.Mode == ManualMode
                    ? await ManualAsync(variantId, shop.Id, shop.CurrencyCode, price, requestId, i, actor, cancellationToken)
                    : await pricing.SetCoefficientPriceAsync(
                        variantId, shop.Id, shop.CurrencyCode, price.PurchasePriceHt, price.Coefficient, price.ExpectedSalePriceHt, price.AcceptBelowCost, null,
                        Derive(requestId, $"price:{i}"), actor, cancellationToken);
                if (!priced.IsSuccess)
                {
                    return Prefixed(priced, $"variants[{i}].price.");
                }
            }

            if (plan.Stock is { Quantity: > 0 } initial)
            {
                var moved = await stock.RecordMovementAsync(
                    variantId, initial.LocationId, MovementTypes.ManualIn, initial.Quantity, StockReasons.InitialStock, null, Derive(requestId, $"stock:{i}"), actor.UserId, cancellationToken);
                if (!moved.IsSuccess)
                {
                    return Prefixed(moved, $"variants[{i}].stock.");
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Created(product);
    }

    private async Task<Outcome<Guid>> ManualAsync(Guid variantId, Guid storeId, string currency, PlannedPrice price, Guid requestId, int index, CatalogActor actor, CancellationToken cancellationToken)
    {
        if (price.PurchasePriceHt is not null)
        {
            var purchase = await pricing.SetPurchasePriceAsync(variantId, storeId, currency, price.PurchasePriceHt, null, Derive(requestId, $"purchase:{index}"), actor.UserId, cancellationToken);
            if (!purchase.IsSuccess)
            {
                return purchase;
            }
        }

        var version = (await prices.GetPriceAsync(variantId, storeId, cancellationToken))?.Version;
        return await pricing.SetManualPriceAsync(variantId, storeId, currency, price.SalePriceHt, price.Reason, version, Derive(requestId, $"manual:{index}"), actor.UserId, cancellationToken);
    }

    /// <summary>Droits par section, contrôlés AVANT toute écriture (aucune écriture partielle refusée en cours de route).</summary>
    private static string? Forbidden(IReadOnlyList<PlannedVariant> variants, CatalogActor actor)
    {
        foreach (var plan in variants)
        {
            if (plan.Suppliers.Count > 0 && !actor.Has(Permissions.SuppliersRead))
            {
                return "suppliers_forbidden";
            }

            if (plan.Suppliers.Any(s => s.SupplierPriceHt is not null || s.SupplierCurrency is not null) && !actor.Has(Permissions.CostsWrite))
            {
                return "costs_forbidden";
            }

            switch (plan.Price)
            {
                case { Mode: CoefficientMode } when !actor.Has(Permissions.PricingWrite) || !actor.Has(Permissions.CostsRead):
                    return "pricing_forbidden";
                case { Mode: ManualMode } when !actor.Has(Permissions.PricingManual):
                    return "manual_price_forbidden";
                case { Mode: ManualMode, PurchasePriceHt: not null } when !actor.Has(Permissions.CostsWrite):
                    return "costs_forbidden";
            }

            if (plan.Stock is not null && !actor.Has(Permissions.StockMovementsWrite))
            {
                return "stock_forbidden";
            }
        }

        return null;
    }

    private static Dictionary<string, string[]> PlanErrors(IReadOnlyList<PlannedVariant> variants, Guid storeId, IReadOnlyList<Domain.Stores.Location> locations)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var i = 0; i < variants.Count; i++)
        {
            var plan = variants[i];
            if (plan.Price is { } price && price.Mode is not (CoefficientMode or ManualMode))
            {
                errors[$"variants[{i}].price.mode"] = [CatalogFieldErrors.InvalidFormat];
            }

            if (plan.Suppliers.Count(s => s.IsPrimary) > 1)
            {
                errors[$"variants[{i}].suppliers"] = [CatalogFieldErrors.Duplicate];
            }

            if (plan.Suppliers.Select(s => s.SupplierId).Distinct().Count() != plan.Suppliers.Count)
            {
                errors[$"variants[{i}].suppliers"] = [CatalogFieldErrors.Duplicate];
            }

            if (plan.Stock is { } initial)
            {
                var policy = StockPolicies.TryParse(plan.Variant.StockPolicy);
                if (policy != StockPolicy.Stocked)
                {
                    // Stock initial interdit pour une prestation ou un article exclusivement commandé.
                    errors[$"variants[{i}].stock"] = [CatalogFieldErrors.NotAllowed];
                }
                else if (initial.Quantity is < 0 or > 100_000)
                {
                    errors[$"variants[{i}].stock.quantity"] = [CatalogFieldErrors.OutOfRange];
                }
                else if (!locations.Any(l => l.Id == initial.LocationId && l.StoreId == storeId && !l.IsArchived))
                {
                    errors[$"variants[{i}].stock.locationId"] = [CatalogFieldErrors.Unknown];
                }
            }
        }

        return errors;
    }

    private async Task<ProductCreated> DescribeAsync(Guid modelId, CancellationToken cancellationToken)
    {
        var model = (await store.FindModelAsync(modelId, cancellationToken))!;
        return new ProductCreated(model.Id, [.. model.Variants.Select(v => (v.Id, v.Sku))]);
    }

    private static Outcome<ProductCreated> Prefixed<T>(Outcome<T> outcome, string prefix) =>
        outcome.Kind == OutcomeKind.Invalid && outcome.Errors is { } errors
            ? Outcome.Invalid<ProductCreated>(errors.ToDictionary(e => prefix + e.Key, e => e.Value, StringComparer.Ordinal))
            : new Outcome<ProductCreated>(outcome.Kind, default, outcome.Code, null, false, new { step = prefix.TrimEnd('.'), details = outcome.Details });

    /// <summary>Identifiant de sous-requête DÉTERMINISTE (même assistant, même étape ⇒ même identifiant) : réémission sûre.</summary>
    public static Guid Derive(Guid requestId, string step)
    {
        Span<byte> input = stackalloc byte[16 + System.Text.Encoding.UTF8.GetMaxByteCount(step.Length)];
        requestId.TryWriteBytes(input);
        var length = 16 + System.Text.Encoding.UTF8.GetBytes(step, input[16..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input[..length], hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80); // version 8 (RFC 9562 : identifiant défini par l'application)
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
