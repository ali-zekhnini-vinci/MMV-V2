using MMV.Domain.Catalog;
using MMV.Domain.Identity;

namespace MMV.Application.Catalog;

/// <summary>Offre d'un fournisseur prévue par le parcours « Nouvelle gamme » (caractéristiques communes déjà recopiées).</summary>
public sealed record PlannedLensOffer(Guid SupplierId, LensOfferInput Offer);

/// <summary>Résultat : fabricant (créé ou repris), gamme (créée ou reprise), offres créées dans l'ordre de la saisie.</summary>
public sealed record LensCatalogCreated(Guid ManufacturerId, Guid RangeId, IReadOnlyList<Guid> OfferIds);

/// <summary>
/// Catalogue des verres unifié (UX-IA, 03/11/2026) : création en UNE transaction d'une gamme (et au besoin de son
/// fabricant) et des offres de PLUSIEURS fournisseurs. L'interface est unifiée, pas le modèle : fabricant et gamme restent
/// des références (<c>catalog.lens_references</c>), chaque offre reste propre à son fournisseur (<c>catalog.lens_offers</c>,
/// plages, restrictions, prix, délai, révision n° 1). Les services existants s'exécutent à l'identique (mêmes règles,
/// mêmes droits, même audit) ; le moindre refus annule tout. Idempotent : sous-requêtes dérivées du <c>requestId</c>.
/// Erreurs préfixées par l'élément (<c>manufacturer.name</c>, <c>range.name</c>, <c>offers[1].sphereMin</c>…).
/// </summary>
public sealed class LensCatalogService(ICatalogUnitOfWork unitOfWork, ICatalogStore store, CatalogService catalog, LensOfferService offers)
{
    public const int MaxOffers = 20;

    public async Task<Outcome<LensCatalogCreated>> CreateAsync(
        Guid? manufacturerId, string? manufacturerName, Guid? rangeId, string? rangeName, IReadOnlyList<PlannedLensOffer> planned, Guid requestId, CatalogActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (rangeId is null == string.IsNullOrWhiteSpace(rangeName))
        {
            // Une gamme existante OU un nom de nouvelle gamme, jamais les deux ni aucun.
            errors["range"] = [rangeId is null ? CatalogFieldErrors.Required : CatalogFieldErrors.NotAllowed];
        }

        if (rangeId is null && manufacturerId is null == string.IsNullOrWhiteSpace(manufacturerName))
        {
            errors["manufacturer"] = [manufacturerId is null ? CatalogFieldErrors.Required : CatalogFieldErrors.NotAllowed];
        }

        if (rangeId is not null && (manufacturerId is not null || !string.IsNullOrWhiteSpace(manufacturerName)))
        {
            errors["manufacturer"] = [CatalogFieldErrors.NotAllowed];
        }

        if (planned.Count > MaxOffers || (rangeId is not null && planned.Count == 0))
        {
            errors["offers"] = [planned.Count == 0 ? CatalogFieldErrors.Required : CatalogFieldErrors.TooLong];
        }

        if (planned.Select(p => p.SupplierId).Distinct().Count() != planned.Count)
        {
            errors["offers"] = [CatalogFieldErrors.Duplicate];
        }

        if (errors.Count > 0)
        {
            return Outcome.Invalid<LensCatalogCreated>(errors);
        }

        // Droits contrôlés AVANT toute écriture : coûts (prix fournisseur, suppléments) réservés à costs.write.
        if (!actor.Has(Permissions.CostsWrite) && planned.Any(p => p.Offer.PriceHt is not null || (p.Offer.Rules ?? []).Any(r => r.SurchargeHt is not null)))
        {
            return Outcome.Forbidden<LensCatalogCreated>("costs_forbidden");
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var replayed = true;
        Guid manufacturer;
        Guid range;
        if (rangeId is { } existingRange)
        {
            var reference = await store.FindLensReferenceAsync(existingRange, cancellationToken);
            if (reference is not { Kind: LensReferenceKind.Range } || reference.IsArchived || reference.ManufacturerId is null)
            {
                return Outcome.Invalid<LensCatalogCreated>("range", reference is null or { IsArchived: false } ? CatalogFieldErrors.Unknown : CatalogFieldErrors.Archived);
            }

            (manufacturer, range) = (reference.ManufacturerId.Value, existingRange);
        }
        else
        {
            if (manufacturerId is { } existingManufacturer)
            {
                manufacturer = existingManufacturer;
            }
            else
            {
                var created = await catalog.CreateLensReferenceAsync(
                    LensReferenceKind.Manufacturer, manufacturerName, null, null, ProductCreationService.Derive(requestId, "manufacturer"), actor.UserId, cancellationToken);
                if (!created.IsSuccess)
                {
                    return Prefixed(created, "manufacturer.");
                }

                (manufacturer, replayed) = (created.Value, replayed && created.Replayed);
            }

            var createdRange = await catalog.CreateLensReferenceAsync(
                LensReferenceKind.Range, rangeName, manufacturer, null, ProductCreationService.Derive(requestId, "range"), actor.UserId, cancellationToken);
            if (!createdRange.IsSuccess)
            {
                return Prefixed(createdRange, createdRange.Errors?.ContainsKey("manufacturerId") == true ? "manufacturer." : "range.");
            }

            (range, replayed) = (createdRange.Value, replayed && createdRange.Replayed);
        }

        var ids = new List<Guid>();
        for (var i = 0; i < planned.Count; i++)
        {
            var offer = await offers.CreateAsync(range, planned[i].SupplierId, planned[i].Offer, ProductCreationService.Derive(requestId, $"offer:{i}"), actor, cancellationToken);
            if (!offer.IsSuccess)
            {
                return Prefixed(offer, $"offers[{i}].");
            }

            ids.Add(offer.Value);
            replayed &= offer.Replayed;
        }

        await transaction.CommitAsync(cancellationToken);
        var result = new LensCatalogCreated(manufacturer, range, ids);
        return replayed ? Outcome.Ok(result, replayed: true) : Outcome.Created(result);
    }

    private static Outcome<LensCatalogCreated> Prefixed<T>(Outcome<T> outcome, string prefix) =>
        outcome.Kind == OutcomeKind.Invalid && outcome.Errors is { } errors
            ? Outcome.Invalid<LensCatalogCreated>(errors.ToDictionary(e => prefix + e.Key, e => e.Value, StringComparer.Ordinal))
            : new Outcome<LensCatalogCreated>(outcome.Kind, default, outcome.Code, null, false, new { step = prefix.TrimEnd('.'), details = outcome.Details });
}
