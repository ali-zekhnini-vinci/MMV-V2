using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using MMV.Api.Auth;
using MMV.Api.Clients;
using MMV.Api.Installation;
using MMV.Application.Catalog;
using MMV.Domain.Catalog;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;

namespace MMV.Api.Catalog;

/// <summary>
/// Outils communs des routes de la Phase 05 : corps JSON stricts, paramètres en liste blanche, enveloppe d'écriture
/// (<c>requestId</c>, <c>version</c>), traduction des résultats en réponses à codes bornés, PROJECTION DES COÛTS selon
/// <c>costs.read</c> (clés jamais émises sans le droit), montants en chaînes décimales à échelle fixe.
/// </summary>
internal static partial class CatalogApi
{
    public static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static readonly ErrorResponse InvalidBody = new("invalid_body");
    public static readonly ErrorResponse InvalidQuery = new("invalid_query");

    public static string P(string permission) => AuthSetup.Permission(permission);

    public static async ValueTask<object?> NoStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }

    public static async Task<T?> ReadBodyAsync<T>(HttpRequest request, CancellationToken cancellationToken)
        where T : class
    {
        if (!request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await request.ReadFromJsonAsync<T>(BodyOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Utilisateur authentifié sans identifiant."));

    public static CatalogActor Actor(ClaimsPrincipal principal) =>
        new(UserId(principal), principal.FindAll(SessionAuthenticationHandler.PermissionClaim).Select(c => c.Value).ToHashSet(StringComparer.Ordinal));

    public static bool Has(ClaimsPrincipal principal, string permission) => principal.HasClaim(SessionAuthenticationHandler.PermissionClaim, permission);

    /// <summary>Enveloppe : corps présent, <c>requestId</c> non vide ; <paramref name="requireVersion"/> : version présente.</summary>
    public static IResult? Envelope(object? body, Guid? requestId, uint? version, bool requireVersion) =>
        body is null ? Results.BadRequest(InvalidBody)
        : requestId is null || requestId == Guid.Empty ? Results.BadRequest(new ErrorResponse("request_id_required"))
        : requireVersion && version is null ? Results.BadRequest(new ErrorResponse("version_required"))
        : null;

    /// <summary>Traduction bornée d'un résultat ; succès : <paramref name="success"/> construit la réponse (relue).</summary>
    public static async Task<IResult> ToResultAsync<T>(Outcome<T> outcome, Func<T, Task<object?>> success, string? location = null)
    {
        switch (outcome.Kind)
        {
            case OutcomeKind.Ok:
                return Results.Ok(await success(outcome.Value!));
            case OutcomeKind.Created:
                var body = await success(outcome.Value!);
                return location is null ? Results.Json(body, statusCode: StatusCodes.Status201Created) : Results.Created(location, body);
            case OutcomeKind.NotFound:
                return Results.NotFound(new ErrorResponse(outcome.Code ?? "not_found"));
            case OutcomeKind.Invalid:
                return Results.UnprocessableEntity(new ValidationErrorResponse("validation_failed", outcome.Errors ?? new Dictionary<string, string[]>()));
            case OutcomeKind.Forbidden:
                return Results.Json(new ErrorResponse(outcome.Code!), statusCode: StatusCodes.Status403Forbidden);
            case OutcomeKind.Conflict:
                return Results.Conflict(new DetailedErrorResponse(outcome.Code ?? "version_conflict", outcome.Details));
            default:
                return Results.UnprocessableEntity(new DetailedErrorResponse(outcome.Code!, outcome.Details));
        }
    }

    // ---------- paramètres ----------

    public static bool OnlyAllowed(HttpRequest request, IReadOnlySet<string> allowed) =>
        request.Query.All(p => allowed.Contains(p.Key) && p.Value.Count == 1);

    public static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;

    // ---------- magasin de l'installation (COR-05-01) ----------

    /// <summary>
    /// Paramètre <c>storeId</c> FACULTATIF : absent, le magasin de l'installation ; présent, il doit le désigner
    /// (sinon <c>404 store_not_found</c>, comme un magasin inexistant). Retourne la réponse de refus ou null.
    /// </summary>
    public static IResult? QueryStore(HttpRequest request, out Store store)
    {
        store = ActiveStore.Of(request.HttpContext);
        if (!request.Query.ContainsKey("storeId"))
        {
            return null;
        }

        return ParseGuid(request.Query["storeId"]) is not { } requested ? Results.BadRequest(InvalidQuery)
            : requested == store.Id ? null
            : Results.NotFound(ActiveStore.StoreNotFound);
    }

    /// <summary>Même règle pour un <c>storeId</c> de corps : absent ⇒ magasin de l'installation.</summary>
    public static IResult? BodyStore(HttpRequest request, Guid? requested, out Store store)
    {
        store = ActiveStore.Of(request.HttpContext);
        return requested is null || requested == store.Id ? null : Results.NotFound(ActiveStore.StoreNotFound);
    }

    /// <summary>Emplacement du magasin de l'installation (sinon refus 404 identique à un emplacement inconnu).</summary>
    public static async Task<bool> OwnsLocationAsync(HttpRequest request, ICatalogStore store, Guid locationId, CancellationToken cancellationToken) =>
        await store.GetLocationAsync(locationId, cancellationToken) is { } location && ActiveStore.Owns(request.HttpContext, location.StoreId);

    public static bool? ParseBoolean(string? value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    public static int? Bounded(string? value, int fallback, int min, int max) =>
        value is null ? fallback : ClientsEndpoints.BoundedInteger(value, min, max);

    public static StatusFilter? Status(string? value) => (value ?? "active") switch
    {
        "active" => StatusFilter.Active,
        "archived" => StatusFilter.Archived,
        "all" => StatusFilter.All,
        _ => null,
    };

    public static VariantSort? Sort(string? value) => (value ?? "name") switch
    {
        "name" => VariantSort.Name,
        "sku" => VariantSort.Sku,
        "family" => VariantSort.Family,
        "updated" => VariantSort.Updated,
        "price" => VariantSort.Price,
        "stock" => VariantSort.Stock,
        _ => null,
    };

    public static bool? Descending(string? value) => (value ?? "asc") switch
    {
        "asc" => false,
        "desc" => true,
        _ => null,
    };

    // ---------- projections ----------

    public static string Money(decimal value) => MoneyText.Format(value, MoneyText.SaleScale);

    public static string? Money(decimal? value) => MoneyText.Format(value, MoneyText.SaleScale);

    public static string? Cost(decimal? value) => MoneyText.Format(value, MoneyText.PurchaseScale);

    public static string PriceStatusCode(PriceStatus status) => status switch
    {
        PriceStatus.UpToDate => "up_to_date",
        PriceStatus.RecalculationPending => "recalculation_pending",
        _ => "incomplete",
    };

    /// <summary>
    /// Tarif d'une variante pour un magasin. Sans <c>costs.read</c> : AUCUNE clé de coût (prix d'achat, coefficient, prix
    /// calculé — il révélerait le coût en mode manuel —, coefficient effectif, marge). TTC seulement avec fiscalité validée.
    /// </summary>
    public static PriceResponse Price(VariantStorePrice? price, Store store, TaxCategory? category, bool costsRead)
    {
        var mode = price?.Mode ?? PricingMode.Coefficient;
        var status = price?.Status ?? PriceStatus.Incomplete;
        var (ttc, ttcStatus) = TaxCalculator.Compute(price?.SalePriceHt, store.IsFiscalProfileValidated, category);
        return new PriceResponse(
            store.Id,
            store.CurrencyCode,
            PricingModes.ToCode(mode),
            PriceStatusCode(status),
            Money(price?.SalePriceHt),
            Money(ttc),
            ttcStatus,
            price?.Version ?? 0,
            costsRead,
            costsRead ? Cost(price?.PurchasePriceHt) : null,
            costsRead ? MoneyText.Format(price?.Coefficient, MoneyText.CoefficientScale) : null,
            costsRead ? Money(price?.ComputedSalePriceHt) : null,
            costsRead ? MoneyText.Format(SalePriceCalculator.EffectiveCoefficient(price?.SalePriceHt, price?.PurchasePriceHt), MoneyText.CoefficientScale) : null,
            costsRead ? Cost(SalePriceCalculator.Margin(price?.SalePriceHt, price?.PurchasePriceHt)) : null);
    }

    public static VariantRowResponse Row(VariantRowView row, Store store, TaxCategory? category, bool costsRead, bool stockRead) => new(
        row.VariantId,
        row.ModelId,
        row.Sku,
        row.Label,
        row.ModelName,
        row.Brand,
        row.FamilyCode,
        StockPolicies.ToCode(row.Policy),
        row.Barcode,
        row.ManufacturerReference,
        row.IsArchived || row.IsModelArchived ? "archived" : "active",
        row.IsIncomplete,
        row.PrimarySupplierId is { } supplierId ? new PrimarySupplierResponse(supplierId, row.PrimarySupplierName!, row.PrimaryLeadTimeDays) : null,
        Price(row.Price, store, category, costsRead),
        stockRead && row.Policy == StockPolicy.Stocked ? new StockSummaryResponse(row.OnHand, row.Reserved, row.Available, row.LowStockThreshold) : null,
        Availabilities.ToCode(row.Availability),
        row.Version);
}

internal sealed record DetailedErrorResponse(string Code, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details);

/// <summary>Tarif ; les clés de coût sont ABSENTES sans <c>costs.read</c> (<c>costsVisible</c> faux).</summary>
internal sealed record PriceResponse(
    Guid StoreId,
    string Currency,
    string PricingMode,
    string PriceStatus,
    string? SalePriceHt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SalePriceTtc,
    string TtcStatus,
    uint Version,
    bool CostsVisible,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PurchasePriceHt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Coefficient,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ComputedSalePriceHt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EffectiveCoefficient,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Margin);

internal sealed record PrimarySupplierResponse(Guid Id, string Name, int? LeadTimeDays);

internal sealed record StockSummaryResponse(int OnHand, int Reserved, int Available, int? LowStockThreshold);

internal sealed record VariantRowResponse(
    Guid VariantId,
    Guid ModelId,
    string Sku,
    string Label,
    string ModelName,
    string? Brand,
    string FamilyCode,
    string StockPolicy,
    string? Barcode,
    string? ManufacturerReference,
    string Status,
    bool IsIncomplete,
    PrimarySupplierResponse? PrimarySupplier,
    PriceResponse Price,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StockSummaryResponse? Stock,
    string Availability,
    uint Version);

internal sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

internal sealed record ActorResponse(Guid Id, string DisplayName);

/// <summary>Événement d'historique (code d'action, auteur, champs cités ; valeurs seulement pour les codes non sensibles).</summary>
internal sealed record EventResponse(Guid Id, DateTimeOffset AtUtc, string Code, ActorResponse Actor, IReadOnlyList<EventFieldResponse> Fields);

internal sealed record EventFieldResponse(string Field, string? Old, string? New, bool Masked);
