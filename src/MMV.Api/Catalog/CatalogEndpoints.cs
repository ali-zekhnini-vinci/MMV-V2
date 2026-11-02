using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using MMV.Api.Clients;
using MMV.Api.Installation;
using MMV.Api.Realtime;
using MMV.Application.Audit;
using MMV.Application.Catalog;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using static MMV.Api.Catalog.CatalogApi;

namespace MMV.Api.Catalog;

// ---------- requêtes ----------

internal sealed record VariantRequest(
    string? Sku = null, string? ManufacturerReference = null, string? Barcode = null, string? Label = null, Dictionary<string, string?>? Attributes = null,
    Guid? LensIndexId = null, List<Guid>? TreatmentIds = null, string? StockPolicy = null)
{
    public VariantInput ToInput() => new(Sku, ManufacturerReference, Barcode, Label, Attributes, LensIndexId, TreatmentIds, StockPolicy);
}

internal sealed record ModelCreateRequest(
    Guid? RequestId = null, string? FamilyCode = null, string? Brand = null, string? Name = null, string? Description = null, Guid? LensRangeId = null,
    Dictionary<string, string?>? Attributes = null, List<VariantRequest>? Variants = null);

/// <summary>Assistant de création produit (COR-05-06) : tout en une transaction, voir <see cref="ProductCreationService"/>.</summary>
internal sealed record ProductSupplierRequest(
    Guid? SupplierId = null, string? SupplierReference = null, bool? IsPrimary = null, string? SupplierPriceHt = null, string? SupplierCurrency = null, int? LeadTimeDays = null);

internal sealed record ProductPriceRequest(
    string? Mode = null, string? PurchasePriceHt = null, string? Coefficient = null, string? ExpectedSalePriceHt = null, bool? AcceptBelowCost = null,
    string? SalePriceHt = null, string? Reason = null);

internal sealed record ProductStockRequest(Guid? LocationId = null, int? Quantity = null);

internal sealed record ProductVariantRequest(
    VariantRequest? Variant = null, List<ProductSupplierRequest>? Suppliers = null, ProductPriceRequest? Price = null, ProductStockRequest? Stock = null);

internal sealed record ProductCreateRequest(
    Guid? RequestId = null, Guid? StoreId = null, string? FamilyCode = null, string? Brand = null, string? Name = null, string? Description = null, Guid? LensRangeId = null,
    Dictionary<string, string?>? Attributes = null, List<ProductVariantRequest>? Variants = null);

internal sealed record ProductCreatedResponse(Guid ModelId, IReadOnlyList<ProductCreatedVariantResponse> Variants, bool Replayed);

internal sealed record ProductCreatedVariantResponse(Guid VariantId, string Sku);

internal sealed record ModelUpdateRequest(
    Guid? RequestId = null, uint? Version = null, string? Brand = null, string? Name = null, string? Description = null, Guid? LensRangeId = null,
    Dictionary<string, string?>? Attributes = null);

internal sealed record VariantCreateRequest(
    Guid? RequestId = null, string? Sku = null, string? ManufacturerReference = null, string? Barcode = null, string? Label = null,
    Dictionary<string, string?>? Attributes = null, Guid? LensIndexId = null, List<Guid>? TreatmentIds = null, string? StockPolicy = null);

internal sealed record VariantUpdateRequest(
    Guid? RequestId = null, uint? Version = null, string? Sku = null, string? ManufacturerReference = null, string? Barcode = null, string? Label = null,
    Dictionary<string, string?>? Attributes = null, Guid? LensIndexId = null, List<Guid>? TreatmentIds = null);

internal sealed record PolicyRequest(Guid? RequestId = null, uint? Version = null, string? StockPolicy = null);

internal sealed record ArchiveRequest(Guid? RequestId = null, uint? Version = null, string? Reason = null);

internal sealed record VersionRequest(Guid? RequestId = null, uint? Version = null);

/// <summary>Offre de verres d'un fournisseur (COR-05-05) : niveau simple + règles avancées structurées.</summary>
internal sealed record LensRangeBody(string? Min = null, string? Max = null);

internal sealed record LensConditionsBody(
    List<Guid>? TreatmentIds = null, List<Guid>? IndexIds = null, string? Material = null, LensRangeBody? Sphere = null, LensRangeBody? Cylinder = null, LensRangeBody? Addition = null);

internal sealed record LensRuleBody(
    string? Kind = null, string? Effect = null, string? Manufacturing = null, LensConditionsBody? Conditions = null, LensRangeBody? Range = null, List<int>? Diameters = null,
    string? MaxSphereCylinderSum = null, string? SurchargeHt = null, int? ExtraLeadDays = null, string? Note = null);

internal sealed record LensOfferRequest(
    Guid? RequestId = null, uint? Version = null, Guid? RangeId = null, Guid? SupplierId = null, string? LensType = null, string? Material = null, Guid? IndexId = null,
    string? Production = null, int? LeadTimeDays = null, string? PriceHt = null, string? PriceCurrency = null, List<int>? Diameters = null,
    string? SphereMin = null, string? SphereMax = null, string? CylinderMin = null, string? CylinderMax = null, string? AdditionMin = null, string? AdditionMax = null,
    List<Guid>? TreatmentIds = null, List<LensRuleBody>? Rules = null, bool? RulesComplete = null, string? SourceReference = null, DateOnly? ValidFrom = null,
    DateOnly? ValidUntil = null, string? Notes = null, string? Reason = null)
{
    private static LensRangeInput? Range(LensRangeBody? body) => body is null ? null : new(body.Min, body.Max);

    public LensOfferInput ToInput() => new(
        LensType, Material, IndexId, Production, LeadTimeDays, PriceHt, PriceCurrency, Diameters, SphereMin, SphereMax, CylinderMin, CylinderMax, AdditionMin, AdditionMax,
        TreatmentIds,
        [.. (Rules ?? []).Select(r => new LensRuleInput(
            r.Kind, r.Effect, r.Manufacturing,
            r.Conditions is { } c ? new LensConditionsInput(c.TreatmentIds, c.IndexIds, c.Material, Range(c.Sphere), Range(c.Cylinder), Range(c.Addition)) : null,
            Range(r.Range), r.Diameters, r.MaxSphereCylinderSum, r.SurchargeHt, r.ExtraLeadDays, r.Note))],
        RulesComplete ?? false, SourceReference, ValidFrom, ValidUntil, Notes, Reason);
}

internal sealed record LensCheckRequest(string? Sphere = null, string? Cylinder = null, string? Addition = null, int? Diameter = null, List<Guid>? TreatmentIds = null, Guid? IndexId = null);

internal sealed record LensOfferResponse(
    Guid Id, Guid RangeId, Guid SupplierId, string SupplierName, string LensType, string? Material, Guid? IndexId, string Production, int? LeadTimeDays,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? PriceHt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? PriceCurrency,
    IReadOnlyList<int> Diameters, string? SphereMin, string? SphereMax, string? CylinderMin, string? CylinderMax, string? AdditionMin, string? AdditionMax,
    IReadOnlyList<Guid> TreatmentIds, IReadOnlyList<LensRule> Rules, bool RulesComplete, string? SourceReference, DateOnly? ValidFrom, DateOnly? ValidUntil, string? Notes,
    int Revision, string Status, DateTimeOffset UpdatedAtUtc, uint Version, bool CostsVisible);

internal sealed record LensRevisionResponse(int Revision, DateTimeOffset ChangedAtUtc, ActorResponse ChangedBy, string? Reason);

internal sealed record LensOfferDetailResponse(LensOfferResponse Offer, IReadOnlyList<LensRevisionResponse> Revisions);

internal sealed record LensCheckResponse(string Status, string Manufacturing, IReadOnlyList<LensCompatibilityReason> Reasons, int ExtraLeadDays, string Disclaimer,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? SurchargeHt);

internal sealed record LensReferenceRequest(Guid? RequestId = null, uint? Version = null, string? Name = null, Guid? ManufacturerId = null, string? IndexValue = null);

internal sealed record SupplierLinkRequest(
    Guid? RequestId = null, uint? Version = null, string? SupplierReference = null, string? SupplierPriceHt = null, string? SupplierCurrency = null,
    int? LeadTimeDays = null, bool? IsPrimary = null);

internal sealed record SupplierRequest(
    Guid? RequestId = null, uint? Version = null, string? Code = null, string? Name = null, string? CountryCode = null, string? ContactName = null, string? Email = null,
    string? Phone = null, string? AddressLine = null, string? PostalCode = null, string? City = null, Dictionary<string, string?>? LegalIdentifiers = null, string? Notes = null,
    bool? AllowDuplicateName = null)
{
    public SupplierInput ToInput() => new(Code, Name, CountryCode, ContactName, Email, Phone, AddressLine, PostalCode, City, LegalIdentifiers, Notes);
}

internal sealed record SupplierArchiveRequest(Guid? RequestId = null, uint? Version = null, string? Reason = null, bool? AcceptIncompleteVariants = null);

internal sealed record PreviewRequest(Guid? StoreId = null, string? PurchasePriceHt = null, string? Coefficient = null);

internal sealed record CoefficientPriceRequest(
    Guid? RequestId = null, uint? Version = null, Guid? StoreId = null, string? Currency = null, string? PurchasePriceHt = null, string? Coefficient = null,
    string? ExpectedSalePriceHt = null, bool? AcceptBelowCost = null);

internal sealed record PurchasePriceRequest(Guid? RequestId = null, uint? Version = null, Guid? StoreId = null, string? Currency = null, string? PurchasePriceHt = null);

internal sealed record RecalculateRequest(Guid? RequestId = null, uint? Version = null, Guid? StoreId = null, string? Currency = null, string? ExpectedSalePriceHt = null);

internal sealed record ManualPriceRequest(
    Guid? RequestId = null, uint? Version = null, Guid? StoreId = null, string? Currency = null, string? SalePriceHt = null, string? Reason = null);

internal sealed record RecalculationItemRequest(Guid? VariantId = null, uint? Version = null, string? ExpectedSalePriceHt = null);

internal sealed record RecalculationApplyRequest(Guid? RequestId = null, Guid? StoreId = null, List<RecalculationItemRequest>? Items = null);

// ---------- réponses ----------

internal sealed record MutationResponse(Guid Id, bool Replayed);

internal sealed record LocationResponse(Guid Id, string Code, string Name, bool IsDefault, string Status);

internal sealed record StoreResponse(Guid Id, string Code, string Name, string CountryCode, string Currency, string FiscalProfileStatus, IReadOnlyList<LocationResponse> Locations);

/// <summary>Magasin de l'installation ; <c>source</c> : <c>configuration</c> (Installation:StoreCode) ou <c>single_store</c>.</summary>
internal sealed record ActiveStoreResponse(StoreResponse Store, string Source);

internal sealed record AttributeResponse(string Key, string Label, string Kind, int MaxLength, int? Min, int? Max);

internal sealed record FamilyResponse(string Code, string Label, int SortOrder, IReadOnlyList<string> AllowedPolicies, IReadOnlyList<AttributeResponse> ModelAttributes, IReadOnlyList<AttributeResponse> VariantAttributes);

internal sealed record LensReferenceResponse(Guid Id, string Kind, string Name, Guid? ManufacturerId, string? IndexValue, string Status, uint Version);

internal sealed record LensReferencesResponse(
    IReadOnlyList<LensReferenceResponse> Manufacturers, IReadOnlyList<LensReferenceResponse> Ranges, IReadOnlyList<LensReferenceResponse> Indices, IReadOnlyList<LensReferenceResponse> Treatments);

internal sealed record ModelVariantResponse(
    VariantRowResponse Row, IReadOnlyDictionary<string, string> Attributes, Guid? LensIndexId, IReadOnlyList<Guid> TreatmentIds, string? ArchiveReason);

internal sealed record ModelResponse(
    Guid Id, string FamilyCode, string? Brand, string Name, string? Description, Guid? LensRangeId, IReadOnlyDictionary<string, string> Attributes, string Status,
    string? ArchiveReason, uint Version, IReadOnlyList<ModelVariantResponse> Variants);

internal sealed record SupplierLinkResponse(
    Guid SupplierId, string Code, string Name, string SupplierStatus, string? SupplierReference, bool IsPrimary, int? LeadTimeDays, bool Removed, uint Version,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? SupplierPriceHt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? SupplierCurrency);

internal sealed record BalanceResponse(Guid LocationId, string LocationCode, string LocationName, bool IsDefault, int OnHand, int Reserved, int Available, uint Version);

internal sealed record ModelSummaryResponse(Guid Id, string FamilyCode, string? Brand, string Name, string Status, uint Version);

internal sealed record VariantDetailResponse(
    VariantRowResponse Row, ModelSummaryResponse Model, IReadOnlyDictionary<string, string> Attributes, Guid? LensIndexId, IReadOnlyList<Guid> TreatmentIds,
    string? ArchiveReason, string? TaxCategoryCode, IReadOnlyList<SupplierLinkResponse>? Suppliers, IReadOnlyList<BalanceResponse>? Balances, bool CostsVisible);

internal sealed record PreviewResponse(string PurchasePriceHt, string Coefficient, string ComputedSalePriceHt, string Currency, string RoundingRule, bool BelowCost);

internal sealed record VariantPriceResponse(Guid VariantId, PriceResponse Price);

internal sealed record PriceHistoryResponse(
    Guid Id, string Field, string Origin, string Currency, Guid? StoreId, Guid? SupplierId, string? OldValue, string? NewValue, string? OldMode, string? NewMode,
    string? Reason, DateTimeOffset ChangedAtUtc, ActorResponse ChangedBy);

internal sealed record RecalculationItemResponse(Guid VariantId, string Sku, string Label, string? CurrentSalePriceHt, string ComputedSalePriceHt, uint Version);

internal sealed record SupplierRowResponse(Guid Id, string Code, string Name, string CountryCode, string? City, string? ContactName, string Status, int VariantCount, int PrimaryCount, uint Version);

internal sealed record SupplierVariantResponse(Guid VariantId, Guid ModelId, string Sku, string Label, string ModelName, string StockPolicy, string Status, bool IsPrimary, string? SupplierReference);

internal sealed record SupplierDetailResponse(
    Guid Id, string Code, string Name, string CountryCode, string? ContactName, string? Email, string? Phone, string? AddressLine, string? PostalCode, string? City,
    IReadOnlyDictionary<string, string> LegalIdentifiers, string? Notes, string Status, string? ArchiveReason, uint Version, IReadOnlyList<SupplierVariantResponse>? Variants);

/// <summary>
/// Routes Catalogue, Tarifs et Fournisseurs (05-A). Droits contrôlés par policies AVANT toute lecture (cumulatives) ;
/// coûts projetés selon <c>costs.read</c> ; écritures à <c>requestId</c> et <c>version</c> ; aucune route <c>DELETE</c>.
/// </summary>
internal static class CatalogEndpoints
{
    private static readonly HashSet<string> VariantListParameters = new(StringComparer.Ordinal)
    {
        "storeId", "q", "familyCode", "policy", "status", "availability", "incomplete", "supplierId", "modelId", "sort", "dir", "page", "pageSize",
    };

    public static void MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        // COR-05-01 : un serveur = un magasin. « /stores » ne sert plus que le magasin de l'installation (compatibilité) ;
        // « /store » le décrit avec l'origine de sa résolution. Aucun sélecteur côté poste.
        api.MapGet("/stores", ListStoresAsync).RequireAuthorization(Auth.AuthSetup.StoresReadPolicy).AddEndpointFilter(NoStore).RequireActiveStore().WithTags("Catalog");
        api.MapGet("/store", CurrentStoreAsync).RequireAuthorization(Auth.AuthSetup.StoresReadPolicy).AddEndpointFilter(NoStore).RequireActiveStore().WithTags("Catalog");

        // COR-05-02 : toute écriture réussie notifie les postes du magasin (sujets ci-dessous), jamais un refus ni une annulation.
        var catalog = api.MapGroup("/catalog").WithTags("Catalog").AddEndpointFilter(NoStore).RequireActiveStore()
            .PublishesChanges(ChangeTopics.Catalog, ChangeTopics.Stock, ChangeTopics.Suppliers);
        catalog.MapGet("/families", () => Results.Ok(Families.All.Select(ToFamily))).RequireAuthorization(P(Permissions.CatalogRead));
        catalog.MapGet("/lens-references", ListLensReferencesAsync).RequireAuthorization(P(Permissions.CatalogRead));
        catalog.MapPost("/lens-references/{kind}", CreateLensReferenceAsync).RequireAuthorization(P(Permissions.CatalogConfigure));
        catalog.MapPut("/lens-references/{kind}/{id:guid}", UpdateLensReferenceAsync).RequireAuthorization(P(Permissions.CatalogConfigure));
        catalog.MapPost("/lens-references/{kind}/{id:guid}/archive", (string kind, Guid id, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken ct) =>
            ArchiveLensReferenceAsync(id, archive: true, request, principal, service, ct)).RequireAuthorization(P(Permissions.CatalogConfigure));
        catalog.MapPost("/lens-references/{kind}/{id:guid}/reactivate", (string kind, Guid id, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken ct) =>
            ArchiveLensReferenceAsync(id, archive: false, request, principal, service, ct)).RequireAuthorization(P(Permissions.CatalogConfigure));

        // COR-05-05 : offres fournisseur du référentiel verres (capacités, restrictions, révisions, compatibilité).
        catalog.MapGet("/lens-offers", ListLensOffersAsync).RequireAuthorization(P(Permissions.CatalogRead));
        catalog.MapGet("/lens-offers/{id:guid}", GetLensOfferAsync).RequireAuthorization(P(Permissions.CatalogRead));
        catalog.MapPost("/lens-offers", CreateLensOfferAsync).RequireAuthorization(P(Permissions.CatalogConfigure), P(Permissions.SuppliersRead));
        catalog.MapPut("/lens-offers/{id:guid}", UpdateLensOfferAsync).RequireAuthorization(P(Permissions.CatalogConfigure));
        catalog.MapPost("/lens-offers/{id:guid}/archive", (Guid id, HttpRequest r, ClaimsPrincipal p, LensOfferService s, CancellationToken ct) => ArchiveLensOfferAsync(id, true, r, p, s, ct))
            .RequireAuthorization(P(Permissions.CatalogConfigure));
        catalog.MapPost("/lens-offers/{id:guid}/reactivate", (Guid id, HttpRequest r, ClaimsPrincipal p, LensOfferService s, CancellationToken ct) => ArchiveLensOfferAsync(id, false, r, p, s, ct))
            .RequireAuthorization(P(Permissions.CatalogConfigure));
        catalog.MapPost("/lens-offers/{id:guid}/check", CheckLensOfferAsync).RequireAuthorization(P(Permissions.CatalogRead)).WithMetadata(NoChangeNotification.Instance);
        catalog.MapGet("/variants", ListVariantsAsync).RequireAuthorization(P(Permissions.CatalogRead));
        catalog.MapGet("/variants/{id:guid}", GetVariantAsync).RequireAuthorization(P(Permissions.CatalogRead));
        catalog.MapGet("/models/{id:guid}", GetModelAsync).RequireAuthorization(P(Permissions.CatalogRead));
        catalog.MapPost("/models", CreateModelAsync).RequireAuthorization(P(Permissions.CatalogWrite));
        catalog.MapPost("/products", CreateProductAsync).RequireAuthorization(P(Permissions.CatalogWrite));
        catalog.MapPut("/models/{id:guid}", UpdateModelAsync).RequireAuthorization(P(Permissions.CatalogWrite));
        catalog.MapPost("/models/{id:guid}/variants", AddVariantAsync).RequireAuthorization(P(Permissions.CatalogWrite));
        catalog.MapPut("/variants/{id:guid}", UpdateVariantAsync).RequireAuthorization(P(Permissions.CatalogWrite));
        catalog.MapPost("/variants/{id:guid}/policy", ChangePolicyAsync).RequireAuthorization(P(Permissions.CatalogWrite));
        catalog.MapPost("/models/{id:guid}/archive", (Guid id, HttpRequest r, ClaimsPrincipal p, CatalogService s, CancellationToken ct) => ArchiveAsync(id, model: true, archive: true, r, p, s, ct))
            .RequireAuthorization(P(Permissions.CatalogArchive));
        catalog.MapPost("/models/{id:guid}/reactivate", (Guid id, HttpRequest r, ClaimsPrincipal p, CatalogService s, CancellationToken ct) => ArchiveAsync(id, model: true, archive: false, r, p, s, ct))
            .RequireAuthorization(P(Permissions.CatalogArchive));
        catalog.MapPost("/variants/{id:guid}/archive", (Guid id, HttpRequest r, ClaimsPrincipal p, CatalogService s, CancellationToken ct) => ArchiveAsync(id, model: false, archive: true, r, p, s, ct))
            .RequireAuthorization(P(Permissions.CatalogArchive));
        catalog.MapPost("/variants/{id:guid}/reactivate", (Guid id, HttpRequest r, ClaimsPrincipal p, CatalogService s, CancellationToken ct) => ArchiveAsync(id, model: false, archive: false, r, p, s, ct))
            .RequireAuthorization(P(Permissions.CatalogArchive));
        catalog.MapPut("/variants/{id:guid}/suppliers/{supplierId:guid}", SetSupplierLinkAsync).RequireAuthorization(P(Permissions.CatalogWrite), P(Permissions.SuppliersRead));
        catalog.MapPost("/variants/{id:guid}/suppliers/{supplierId:guid}/remove", RemoveSupplierLinkAsync).RequireAuthorization(P(Permissions.CatalogWrite), P(Permissions.SuppliersRead));
        catalog.MapGet("/history/{entityType}/{id:guid}", HistoryAsync).RequireAuthorization(P(Permissions.CatalogRead), P(Permissions.AuditRead));

        var pricing = api.MapGroup("/pricing").WithTags("Pricing").AddEndpointFilter(NoStore).RequireActiveStore().PublishesChanges(ChangeTopics.Pricing);
        pricing.MapGet("/variants/{id:guid}", GetPriceAsync).RequireAuthorization(P(Permissions.CatalogRead));
        pricing.MapGet("/variants/{id:guid}/history", PriceHistoryAsync).RequireAuthorization(P(Permissions.CatalogRead));
        pricing.MapPost("/preview", PreviewAsync).RequireAuthorization(P(Permissions.PricingWrite), P(Permissions.CostsRead)).WithMetadata(NoChangeNotification.Instance);
        pricing.MapPut("/variants/{id:guid}", SetCoefficientPriceAsync).RequireAuthorization(P(Permissions.PricingWrite), P(Permissions.CostsRead));
        pricing.MapPut("/variants/{id:guid}/purchase-price", SetPurchasePriceAsync).RequireAuthorization(P(Permissions.CostsWrite));
        pricing.MapPost("/variants/{id:guid}/recalculate", RecalculateAsync).RequireAuthorization(P(Permissions.PricingWrite), P(Permissions.CostsRead));
        pricing.MapPut("/variants/{id:guid}/manual-price", SetManualPriceAsync).RequireAuthorization(P(Permissions.PricingManual));
        pricing.MapGet("/recalculations", PendingRecalculationsAsync).RequireAuthorization(P(Permissions.PricingWrite), P(Permissions.CostsRead));
        pricing.MapPost("/recalculations/apply", ApplyRecalculationsAsync).RequireAuthorization(P(Permissions.PricingWrite), P(Permissions.CostsRead));

        var suppliers = api.MapGroup("/suppliers").WithTags("Suppliers").AddEndpointFilter(NoStore).RequireActiveStore()
            .PublishesChanges(ChangeTopics.Suppliers, ChangeTopics.Catalog);
        suppliers.MapGet("/", ListSuppliersAsync).RequireAuthorization(P(Permissions.SuppliersRead));
        suppliers.MapGet("/{id:guid}", GetSupplierAsync).RequireAuthorization(P(Permissions.SuppliersRead));
        suppliers.MapPost("/", CreateSupplierAsync).RequireAuthorization(P(Permissions.SuppliersWrite));
        suppliers.MapPut("/{id:guid}", UpdateSupplierAsync).RequireAuthorization(P(Permissions.SuppliersWrite));
        suppliers.MapPost("/{id:guid}/archive", ArchiveSupplierAsync).RequireAuthorization(P(Permissions.SuppliersWrite));
        suppliers.MapPost("/{id:guid}/reactivate", ReactivateSupplierAsync).RequireAuthorization(P(Permissions.SuppliersWrite));
    }

    // ---------- magasins, familles, référentiel verres ----------

    private static async Task<StoreResponse> ToStoreAsync(Domain.Stores.Store shop, ICatalogStore store, CancellationToken cancellationToken)
    {
        var locations = await store.ListLocationsAsync(shop.Id, cancellationToken);
        return new StoreResponse(
            shop.Id, shop.Code, shop.Name, shop.CountryCode, shop.CurrencyCode, shop.FiscalProfileStatus,
            [.. locations.Select(l => new LocationResponse(l.Id, l.Code, l.Name, l.IsDefault, l.IsArchived ? "archived" : "active"))]);
    }

    /// <summary>Liste réduite au SEUL magasin de l'installation (les autres magasins de la base ne sont jamais servis).</summary>
    private static async Task<IResult> ListStoresAsync(HttpContext context, ICatalogStore store, CancellationToken cancellationToken) =>
        Results.Ok(new[] { await ToStoreAsync(ActiveStore.Of(context), store, cancellationToken) });

    private static async Task<IResult> CurrentStoreAsync(HttpContext context, ICatalogStore store, InstallationStoreResolver resolver, CancellationToken cancellationToken)
    {
        var resolution = await resolver.ResolveAsync(store, cancellationToken);
        return Results.Ok(new ActiveStoreResponse(await ToStoreAsync(ActiveStore.Of(context), store, cancellationToken), resolution.Source ?? "configuration"));
    }

    private static FamilyResponse ToFamily(FamilyDefinition family)
    {
        static AttributeResponse Attribute(AttributeDefinition a) => new(
            a.Key, a.Label, a.Kind switch { AttributeKind.WholeNumber => "integer", AttributeKind.DecimalNumber => "decimal", _ => "text" },
            a.MaxLength, a.Kind == AttributeKind.WholeNumber ? a.Min : null, a.Kind == AttributeKind.WholeNumber ? a.Max : null);
        return new FamilyResponse(
            family.Code, family.Label, family.SortOrder, [.. family.AllowedPolicies.Select(StockPolicies.ToCode)],
            [.. family.ModelAttributes.Select(Attribute)], [.. family.VariantAttributes.Select(Attribute)]);
    }

    private static LensReferenceResponse ToLens(LensReference r) =>
        new(r.Id, r.KindCode, r.Name, r.ManufacturerId, r.IndexValue is { } v ? LensReferenceKinds.IndexKey(v) : null, r.IsArchived ? "archived" : "active", r.Version);

    private static async Task<IResult> ListLensReferencesAsync(ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var all = await reads.LensReferencesAsync(cancellationToken);
        IReadOnlyList<LensReferenceResponse> Of(LensReferenceKind kind) => [.. all.Where(r => r.Kind == kind).Select(ToLens)];
        return Results.Ok(new LensReferencesResponse(Of(LensReferenceKind.Manufacturer), Of(LensReferenceKind.Range), Of(LensReferenceKind.Index), Of(LensReferenceKind.Treatment)));
    }

    private static async Task<IResult> CreateLensReferenceAsync(
        string kind, HttpRequest request, ClaimsPrincipal principal, CatalogService service, ICatalogStore store, CancellationToken cancellationToken)
    {
        if (LensReferenceKinds.FromSegment(kind) is not { } lensKind)
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<LensReferenceRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.CreateLensReferenceAsync(lensKind, body!.Name, body.ManufacturerId, body.IndexValue, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, async id => ToLens((await store.FindLensReferenceAsync(id, cancellationToken))!));
    }

    private static async Task<IResult> UpdateLensReferenceAsync(
        string kind, Guid id, HttpRequest request, ClaimsPrincipal principal, CatalogService service, ICatalogStore store, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<LensReferenceRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.UpdateLensReferenceAsync(id, body!.Name, body.IndexValue, body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, async x => (object?)new MutationResponse(x, outcome.Replayed));
    }

    private static async Task<IResult> ArchiveLensReferenceAsync(Guid id, bool archive, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<VersionRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.SetLensReferenceArchivedAsync(id, archive, body!.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    // ---------- offres de verres (COR-05-05) ----------

    private const string LensDisclaimer = "Évaluation selon les seules règles fournisseur enregistrées dans MMV : ni validation clinique, ni garantie du fabricant.";

    private static string? Diopter(decimal? value) => MoneyText.Format(value, 2);

    private static LensOfferResponse ToOffer(Domain.Catalog.LensOffer offer, string supplierName, bool costs)
    {
        var rules = LensRuleJson.Deserialize(offer.RulesJson);
        return new LensOfferResponse(
            offer.Id, offer.RangeId, offer.SupplierId, supplierName, offer.LensTypeCode, offer.Material, offer.IndexId, offer.ProductionCode, offer.LeadTimeDays,
            costs ? Cost(offer.PriceHt) : null, costs ? offer.PriceCurrency : null, LensRuleJson.DeserializeDiameters(offer.DiametersJson),
            Diopter(offer.SphereMin), Diopter(offer.SphereMax), Diopter(offer.CylinderMin), Diopter(offer.CylinderMax), Diopter(offer.AdditionMin), Diopter(offer.AdditionMax),
            [.. offer.Treatments.Select(t => t.TreatmentId)],
            costs ? rules : [.. rules.Select(r => r with { SurchargeHt = null })],
            offer.RulesComplete, offer.SourceReference, offer.ValidFrom, offer.ValidUntil, offer.Notes, offer.Revision, offer.IsArchived ? "archived" : "active",
            offer.UpdatedAtUtc, offer.Version, costs);
    }

    private static async Task<IResult> ListLensOffersAsync(HttpRequest request, ClaimsPrincipal principal, ILensOfferStore offers, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var q = request.Query;
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "rangeId", "supplierId", "status" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        var range = q.ContainsKey("rangeId") ? ParseGuid(q["rangeId"]) : null;
        var supplier = q.ContainsKey("supplierId") ? ParseGuid(q["supplierId"]) : null;
        var status = Status(q["status"].FirstOrDefault());
        if ((q.ContainsKey("rangeId") && range is null) || (q.ContainsKey("supplierId") && supplier is null) || status is null)
        {
            return Results.BadRequest(InvalidQuery);
        }

        var list = (await offers.ListAsync(range, supplier, status != StatusFilter.Active, cancellationToken))
            .Where(o => status == StatusFilter.All || (status == StatusFilter.Archived) == o.IsArchived).ToList();
        var names = await SupplierNamesAsync(reads, list.Select(o => o.SupplierId), cancellationToken);
        var costs = Has(principal, Permissions.CostsRead);
        return Results.Ok(list.Select(o => ToOffer(o, names.GetValueOrDefault(o.SupplierId, "?"), costs)));
    }

    private static async Task<Dictionary<Guid, string>> SupplierNamesAsync(ICatalogReadStore reads, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var id in ids.Distinct())
        {
            if (await reads.GetSupplierAsync(id, cancellationToken) is { } supplier)
            {
                names[id] = supplier.Name;
            }
        }

        return names;
    }

    private static async Task<IResult> GetLensOfferAsync(Guid id, ClaimsPrincipal principal, ILensOfferStore offers, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var offer = await offers.FindAsync(id, cancellationToken);
        if (offer is null)
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var names = await SupplierNamesAsync(reads, [offer.SupplierId], cancellationToken);
        var revisions = await offers.RevisionsAsync(id, cancellationToken);
        var people = await reads.DisplayNamesAsync([.. revisions.Select(r => r.ChangedByUserId)], cancellationToken);
        return Results.Ok(new LensOfferDetailResponse(
            ToOffer(offer, names.GetValueOrDefault(offer.SupplierId, "?"), Has(principal, Permissions.CostsRead)),
            [.. revisions.Select(r => new LensRevisionResponse(r.Revision, r.ChangedAtUtc, new ActorResponse(r.ChangedByUserId, people.GetValueOrDefault(r.ChangedByUserId, "Compte inconnu")), r.Reason))]));
    }

    private static async Task<IResult> CreateLensOfferAsync(HttpRequest request, ClaimsPrincipal principal, LensOfferService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<LensOfferRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.CreateAsync(body!.RangeId, body.SupplierId, body.ToInput(), body.RequestId!.Value, Actor(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)), $"/api/catalog/lens-offers/{outcome.Value}");
    }

    private static async Task<IResult> UpdateLensOfferAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, LensOfferService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<LensOfferRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        if (body!.RangeId is not null || body.SupplierId is not null)
        {
            // Gamme et fournisseur d'une offre immuables (une autre combinaison = une autre offre).
            return Results.BadRequest(InvalidBody);
        }

        var outcome = await service.UpdateAsync(id, body.ToInput(), body.Version!.Value, body.RequestId!.Value, Actor(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> ArchiveLensOfferAsync(Guid id, bool archive, HttpRequest request, ClaimsPrincipal principal, LensOfferService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ArchiveRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.SetArchivedAsync(id, archive, body!.Reason, body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    /// <summary>Évaluation sans écriture (aucune notification temps réel : route POST non transactionnelle, réponse 200).</summary>
    private static async Task<IResult> CheckLensOfferAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, LensOfferService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<LensCheckRequest>(request, cancellationToken);
        if (body is null)
        {
            return Results.BadRequest(InvalidBody);
        }

        var outcome = await service.CheckAsync(id, new LensPrescriptionInput(body.Sphere, body.Cylinder, body.Addition, body.Diameter, body.TreatmentIds, body.IndexId), cancellationToken);
        var costs = Has(principal, Permissions.CostsRead);
        return await ToResultAsync(outcome, r => Task.FromResult<object?>(new LensCheckResponse(
            r.Status switch { LensCompatibilityStatus.Compatible => "compatible", LensCompatibilityStatus.Incompatible => "incompatible", _ => "to_verify" },
            r.Manufacturing, r.Reasons, r.ExtraLeadDays, LensDisclaimer, costs ? Cost(r.SurchargeHt) : null)));
    }

    // ---------- modèles et variantes ----------

    private static async Task<IResult> ListVariantsAsync(HttpRequest request, ClaimsPrincipal principal, ICatalogReadStore reads, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        var q = request.Query;
        if (!OnlyAllowed(request, VariantListParameters))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var search = q["q"].ToString().Trim();
        var family = q["familyCode"].ToString() is { Length: > 0 } f ? f : null;
        var policyText = q["policy"].ToString();
        StockPolicy? policy = policyText.Length == 0 ? null : StockPolicies.TryParse(policyText);
        var availabilityText = q["availability"].ToString();
        var availability = availabilityText.Length == 0 ? null : Availabilities.FromCode(availabilityText);
        var incompleteText = q["incomplete"].ToString();
        var incomplete = incompleteText.Length == 0 ? null : ParseBoolean(incompleteText);
        var supplier = q.ContainsKey("supplierId") ? ParseGuid(q["supplierId"]) : null;
        var model = q.ContainsKey("modelId") ? ParseGuid(q["modelId"]) : null;
        var status = Status(q["status"].FirstOrDefault());
        var sort = Sort(q["sort"].FirstOrDefault());
        var descending = Descending(q["dir"].FirstOrDefault());
        var page = Bounded(q["page"].FirstOrDefault(), 1, 1, VariantListQuery.MaxPage);
        var size = Bounded(q["pageSize"].FirstOrDefault(), VariantListQuery.DefaultPageSize, 1, VariantListQuery.MaxPageSize);
        if (search.Length > VariantListQuery.SearchMaxLength || (family is not null && Families.Find(family) is null) || (policyText.Length > 0 && policy is null)
            || (availabilityText.Length > 0 && availability is null) || (incompleteText.Length > 0 && incomplete is null)
            || (q.ContainsKey("supplierId") && supplier is null) || (q.ContainsKey("modelId") && model is null)
            || status is null || sort is null || descending is null || page is null || size is null)
        {
            return Results.BadRequest(InvalidQuery);
        }

        var result = await reads.ListVariantsAsync(
            new VariantListQuery(storeId, search.Length == 0 ? null : search, family, policy, status.Value, availability, incomplete, supplier, model, sort.Value, descending.Value, page.Value, size.Value),
            cancellationToken);
        var costs = Has(principal, Permissions.CostsRead);
        var stock = Has(principal, Permissions.StockRead);
        var items = new List<VariantRowResponse>();
        foreach (var row in result.Items)
        {
            items.Add(Row(row, shop, await TaxAsync(pricing, shop, row.TaxCategoryCode, cancellationToken), costs, stock));
        }

        return Results.Ok(new PageResponse<VariantRowResponse>(items, result.PageNumber, result.PageSize, result.TotalCount));
    }

    private static async Task<TaxCategory?> TaxAsync(IPricingStore pricing, Domain.Stores.Store shop, string? code, CancellationToken cancellationToken) =>
        shop.IsFiscalProfileValidated && code is not null ? await pricing.TaxCategoryAsync(shop.Id, code, cancellationToken) : null;

    private static async Task<IResult> GetModelAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, ICatalogReadStore reads, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var model = await reads.GetModelAsync(id, cancellationToken);
        if (model is null)
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var rows = await reads.ListVariantsAsync(new VariantListQuery(storeId, Status: StatusFilter.All, ModelId: id, PageSize: VariantListQuery.MaxPageSize), cancellationToken);
        var costs = Has(principal, Permissions.CostsRead);
        var stock = Has(principal, Permissions.StockRead);
        var variants = new List<ModelVariantResponse>();
        foreach (var variant in model.Variants.OrderBy(v => v.Label, StringComparer.CurrentCultureIgnoreCase))
        {
            var row = rows.Items.First(r => r.VariantId == variant.Id);
            variants.Add(new ModelVariantResponse(
                Row(row, shop, await TaxAsync(pricing, shop, row.TaxCategoryCode, cancellationToken), costs, stock),
                variant.Attributes, variant.LensIndexId, [.. variant.Treatments.Select(t => t.TreatmentId)], variant.ArchiveReason));
        }

        return Results.Ok(new ModelResponse(
            model.Id, model.FamilyCode, model.Brand, model.Name, model.Description, model.LensRangeId, model.Attributes, model.IsArchived ? "archived" : "active",
            model.ArchiveReason, model.Version, variants));
    }

    private static async Task<IResult> GetVariantAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, ICatalogReadStore reads, IPricingStore pricing, ICatalogStore store, CancellationToken cancellationToken)
    {
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var detail = await reads.GetVariantAsync(id, storeId, cancellationToken);
        if (detail is null)
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var costs = Has(principal, Permissions.CostsRead);
        var stock = Has(principal, Permissions.StockRead);
        var suppliers = Has(principal, Permissions.SuppliersRead)
            ? detail.Suppliers.Select(s => new SupplierLinkResponse(
                s.Link.SupplierId, s.SupplierCode, s.SupplierName, s.SupplierArchived ? "archived" : "active", s.Link.SupplierReference, s.Link.IsPrimary, s.Link.LeadTimeDays,
                s.Link.IsRemoved, s.Link.Version, costs ? Cost(s.Link.SupplierPriceHt) : null, costs ? s.Link.SupplierCurrency : null)).ToList()
            : null;
        var balances = stock && detail.Variant.StockPolicy == StockPolicy.Stocked
            ? detail.Balances.Select(b => new BalanceResponse(b.LocationId, b.LocationCode, b.LocationName, b.IsDefault, b.OnHand, b.Reserved, b.OnHand - b.Reserved, b.Version)).ToList()
            : null;
        return Results.Ok(new VariantDetailResponse(
            Row(detail.Row, shop, await TaxAsync(pricing, shop, detail.Variant.TaxCategoryCode, cancellationToken), costs, stock),
            new ModelSummaryResponse(detail.Model.Id, detail.Model.FamilyCode, detail.Model.Brand, detail.Model.Name, detail.Model.IsArchived ? "archived" : "active", detail.Model.Version),
            detail.Variant.Attributes, detail.Variant.LensIndexId, [.. detail.Variant.Treatments.Select(t => t.TreatmentId)], detail.Variant.ArchiveReason,
            detail.Variant.TaxCategoryCode, suppliers, balances, costs));
    }

    private static async Task<IResult> CreateModelAsync(HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ModelCreateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.CreateModelAsync(
            new ModelInput(body!.FamilyCode, body.Brand, body.Name, body.Description, body.LensRangeId, body.Attributes),
            [.. (body.Variants ?? []).Select(v => v.ToInput())], body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, id => Task.FromResult<object?>(new MutationResponse(id, outcome.Replayed)), $"/api/catalog/models/{outcome.Value}");
    }

    /// <summary>
    /// Enregistrement final de l'assistant : modèle + variantes + fournisseurs + tarifs + stock initial, tout ou rien.
    /// Magasin = celui de l'installation ; les droits de chaque section sont contrôlés avant toute écriture.
    /// </summary>
    private static async Task<IResult> CreateProductAsync(HttpRequest request, ClaimsPrincipal principal, ProductCreationService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ProductCreateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var variants = body.Variants ?? [];
        if (variants.Any(v => v.Variant is null || (v.Suppliers ?? []).Any(s => s.SupplierId is null) || v.Stock is { LocationId: null } or { Quantity: null }))
        {
            return Results.BadRequest(InvalidBody);
        }

        var outcome = await service.CreateAsync(
            new ModelInput(body.FamilyCode, body.Brand, body.Name, body.Description, body.LensRangeId, body.Attributes),
            [.. variants.Select(v => new PlannedVariant(
                v.Variant!.ToInput(),
                [.. (v.Suppliers ?? []).Select(s => new PlannedSupplierLink(s.SupplierId!.Value, s.SupplierReference, s.IsPrimary ?? false, s.SupplierPriceHt, s.SupplierCurrency, s.LeadTimeDays))],
                v.Price is { } p ? new PlannedPrice(p.Mode ?? ProductCreationService.CoefficientMode, p.PurchasePriceHt, p.Coefficient, p.ExpectedSalePriceHt, p.AcceptBelowCost ?? false, p.SalePriceHt, p.Reason) : null,
                v.Stock is { } st ? new PlannedStock(st.LocationId!.Value, st.Quantity!.Value) : null))],
            shop.Id, body.RequestId!.Value, Actor(principal), cancellationToken);
        return await ToResultAsync(outcome, created => Task.FromResult<object?>(new ProductCreatedResponse(
            created.ModelId, [.. created.Variants.Select(v => new ProductCreatedVariantResponse(v.VariantId, v.Sku))], outcome.Replayed)), $"/api/catalog/models/{outcome.Value?.ModelId}");
    }

    private static async Task<IResult> UpdateModelAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ModelUpdateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.UpdateModelAsync(
            id, new ModelInput(null, body!.Brand, body.Name, body.Description, body.LensRangeId, body.Attributes), body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> AddVariantAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<VariantCreateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.AddVariantAsync(
            id, new VariantInput(body!.Sku, body.ManufacturerReference, body.Barcode, body.Label, body.Attributes, body.LensIndexId, body.TreatmentIds, body.StockPolicy),
            body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)), $"/api/catalog/variants/{outcome.Value}");
    }

    private static async Task<IResult> UpdateVariantAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<VariantUpdateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.UpdateVariantAsync(
            id, new VariantInput(body!.Sku, body.ManufacturerReference, body.Barcode, body.Label, body.Attributes, body.LensIndexId, body.TreatmentIds, null),
            body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> ChangePolicyAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<PolicyRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.ChangePolicyAsync(id, body!.StockPolicy, body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> ArchiveAsync(Guid id, bool model, bool archive, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ArchiveRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        if (!archive && body!.Reason is not null)
        {
            return Results.BadRequest(InvalidBody);
        }

        var (version, requestId, userId) = (body!.Version!.Value, body.RequestId!.Value, UserId(principal));
        var outcome = (model, archive) switch
        {
            (true, true) => await service.ArchiveModelAsync(id, body.Reason, version, requestId, userId, cancellationToken),
            (true, false) => await service.ReactivateModelAsync(id, version, requestId, userId, cancellationToken),
            (false, true) => await service.ArchiveVariantAsync(id, body.Reason, version, requestId, userId, cancellationToken),
            _ => await service.ReactivateVariantAsync(id, version, requestId, userId, cancellationToken),
        };
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> SetSupplierLinkAsync(Guid id, Guid supplierId, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        // Présence d'un champ de prix (même vide) : sans costs.write, refus 403 — jamais d'effacement par omission.
        request.EnableBuffering();
        bool priceSpecified;
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            priceSpecified = document.RootElement.ValueKind == JsonValueKind.Object
                && (document.RootElement.TryGetProperty("supplierPriceHt", out _) || document.RootElement.TryGetProperty("supplierCurrency", out _));
        }
        catch (JsonException)
        {
            return Results.BadRequest(InvalidBody);
        }

        request.Body.Position = 0;
        var body = await ReadBodyAsync<SupplierLinkRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.SetSupplierLinkAsync(
            id, supplierId, new VariantSupplierInput(body!.SupplierReference, priceSpecified, body.SupplierPriceHt, body.SupplierCurrency, body.LeadTimeDays, body.IsPrimary ?? false),
            body.Version, body.RequestId!.Value, Actor(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> RemoveSupplierLinkAsync(Guid id, Guid supplierId, HttpRequest request, ClaimsPrincipal principal, CatalogService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<VersionRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.RemoveSupplierLinkAsync(id, supplierId, body!.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    /// <summary>Historique d'audit d'une fiche (<c>audit.read</c> ET <c>catalog.read</c>) ; aucune valeur de coût n'y figure jamais.</summary>
    private static async Task<IResult> HistoryAsync(string entityType, Guid id, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        string[]? modules = entityType switch
        {
            AuditActions.ModelEntity or AuditActions.LensReferenceEntity => [AuditActions.CatalogModule],
            AuditActions.VariantEntity => [AuditActions.CatalogModule, AuditActions.PricingModule, AuditActions.StockModule],
            AuditActions.SupplierEntity => [AuditActions.SuppliersModule],
            AuditActions.InventoryEntity => [AuditActions.InventoryModule],
            AuditActions.ReceiptEntity => [AuditActions.StockModule],
            AuditActions.ImportEntity => [AuditActions.ImportsModule],
            _ => null,
        };
        if (modules is null)
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var events = new List<AuditEvent>();
        foreach (var module in modules)
        {
            events.AddRange(await reads.EventsAsync(module, entityType, id, cancellationToken));
        }

        var names = await reads.DisplayNamesAsync([.. events.Select(e => e.ActorUserId)], cancellationToken);
        return Results.Ok(events.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Select(e => new EventResponse(
            e.Id, e.OccurredAtUtc, e.Action, new ActorResponse(e.ActorUserId, names.GetValueOrDefault(e.ActorUserId, "Compte inconnu")),
            [.. AuditChanges.Deserialize(e.Changes).Select(c => new EventFieldResponse(c.Field, c.Old, c.New, c.Masked))])));
    }

    // ---------- tarifs ----------

    private static async Task<IResult> PriceResultAsync(Outcome<Guid> outcome, Guid variantId, Guid storeId, ClaimsPrincipal principal, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken) =>
        await ToResultAsync(outcome, async _ => (object?)await VariantPriceAsync(variantId, storeId, principal, store, pricing, cancellationToken));

    private static async Task<VariantPriceResponse> VariantPriceAsync(Guid variantId, Guid storeId, ClaimsPrincipal principal, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        var shop = (await store.GetStoreAsync(storeId, cancellationToken))!;
        var variant = await store.FindVariantAsync(variantId, cancellationToken);
        var price = await pricing.GetPriceAsync(variantId, storeId, cancellationToken);
        return new VariantPriceResponse(variantId, Price(price, shop, await TaxAsync(pricing, shop, variant?.TaxCategoryCode, cancellationToken), Has(principal, Permissions.CostsRead)));
    }

    private static async Task<IResult> GetPriceAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        if (await store.FindVariantAsync(id, cancellationToken) is null)
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        return Results.Ok(await VariantPriceAsync(id, storeId, principal, store, pricing, cancellationToken));
    }

    /// <summary>Historique des prix : lignes de coût (prix d'achat, coefficient, prix fournisseur) seulement avec <c>costs.read</c>.</summary>
    private static async Task<IResult> PriceHistoryAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, IPricingStore pricing, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var costs = Has(principal, Permissions.CostsRead);
        var entries = (await pricing.HistoryAsync(id, storeId, cancellationToken)).Where(e => costs || !PriceFields.IsCost(e.Field)).ToList();
        var names = await reads.DisplayNamesAsync([.. entries.Select(e => e.ChangedByUserId)], cancellationToken);
        return Results.Ok(entries.Select(e => new PriceHistoryResponse(
            e.Id, e.Field, e.Origin, e.CurrencyCode, e.StoreId, e.SupplierId,
            e.Field == PriceFields.SalePriceHt ? Money(e.OldValue) : e.Field == PriceFields.Coefficient ? MoneyText.Format(e.OldValue, MoneyText.CoefficientScale) : Cost(e.OldValue),
            e.Field == PriceFields.SalePriceHt ? Money(e.NewValue) : e.Field == PriceFields.Coefficient ? MoneyText.Format(e.NewValue, MoneyText.CoefficientScale) : Cost(e.NewValue),
            e.OldMode, e.NewMode, e.Reason, e.ChangedAtUtc, new ActorResponse(e.ChangedByUserId, names.GetValueOrDefault(e.ChangedByUserId, "Compte inconnu")))));
    }

    private static async Task<IResult> PreviewAsync(HttpRequest request, PricingService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<PreviewRequest>(request, cancellationToken);
        if (body is null)
        {
            return Results.BadRequest(InvalidBody);
        }

        if (BodyStore(request, body.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var outcome = await service.PreviewAsync(shop.Id, body.PurchasePriceHt, body.Coefficient, cancellationToken);
        return await ToResultAsync(outcome, p => Task.FromResult<object?>(new PreviewResponse(
            Cost(p.PurchasePriceHt)!, MoneyText.Format(p.Coefficient, MoneyText.CoefficientScale), Money(p.ComputedSalePriceHt), p.Currency, SalePriceCalculator.RoundingRule, p.BelowCost)));
    }

    private static async Task<IResult> SetCoefficientPriceAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, PricingService service, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<CoefficientPriceRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var outcome = await service.SetCoefficientPriceAsync(
            id, storeId, body.Currency, body.PurchasePriceHt, body.Coefficient, body.ExpectedSalePriceHt, body.AcceptBelowCost ?? false, body.Version, body.RequestId!.Value, Actor(principal), cancellationToken);
        return await PriceResultAsync(outcome, id, storeId, principal, store, pricing, cancellationToken);
    }

    private static async Task<IResult> SetPurchasePriceAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, PricingService service, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<PurchasePriceRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var outcome = await service.SetPurchasePriceAsync(id, storeId, body.Currency, body.PurchasePriceHt, body.Version, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await PriceResultAsync(outcome, id, storeId, principal, store, pricing, cancellationToken);
    }

    private static async Task<IResult> RecalculateAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, PricingService service, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<RecalculateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var outcome = await service.RecalculateAsync(id, storeId, body.Currency, body.ExpectedSalePriceHt, body.Version, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await PriceResultAsync(outcome, id, storeId, principal, store, pricing, cancellationToken);
    }

    private static async Task<IResult> SetManualPriceAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, PricingService service, ICatalogStore store, IPricingStore pricing, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ManualPriceRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var outcome = await service.SetManualPriceAsync(id, storeId, body.Currency, body.SalePriceHt, body.Reason, body.Version, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await PriceResultAsync(outcome, id, storeId, principal, store, pricing, cancellationToken);
    }

    private static async Task<IResult> PendingRecalculationsAsync(HttpRequest request, PricingService service, ICatalogStore store, CancellationToken cancellationToken)
    {
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var outcome = await service.PreviewRecalculationsAsync(storeId, cancellationToken);
        return await ToResultAsync(outcome, async items =>
        {
            var variants = (await store.FindVariantsAsync([.. items.Select(i => i.VariantId)], cancellationToken)).ToDictionary(v => v.Id);
            return items.Select(i => new RecalculationItemResponse(i.VariantId, variants[i.VariantId].Sku, variants[i.VariantId].Label, Money(i.CurrentSalePriceHt), Money(i.ComputedSalePriceHt), i.Version)).ToList();
        });
    }

    private static async Task<IResult> ApplyRecalculationsAsync(HttpRequest request, ClaimsPrincipal principal, PricingService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<RecalculationApplyRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;
        if (body.Items is null || body.Items.Any(i => i.VariantId is null || i.Version is null || MoneyText.TryParse(i.ExpectedSalePriceHt, MoneyText.SaleScale) is null))
        {
            return Results.BadRequest(InvalidBody);
        }

        var outcome = await service.ApplyRecalculationsAsync(
            storeId, [.. body.Items.Select(i => new RecalculationConfirmation(i.VariantId!.Value, i.Version!.Value, MoneyText.TryParse(i.ExpectedSalePriceHt, MoneyText.SaleScale)!.Value))],
            body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, applied => Task.FromResult<object?>(new { applied, replayed = outcome.Replayed }));
    }

    // ---------- fournisseurs ----------

    private static async Task<IResult> ListSuppliersAsync(HttpRequest request, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var q = request.Query;
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "q", "countryCode", "status", "dir", "page", "pageSize" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        var search = q["q"].ToString().Trim();
        var country = q["countryCode"].ToString() is { Length: > 0 } c ? c.ToUpperInvariant() : null;
        var status = Status(q["status"].FirstOrDefault());
        var descending = Descending(q["dir"].FirstOrDefault());
        var page = Bounded(q["page"].FirstOrDefault(), 1, 1, VariantListQuery.MaxPage);
        var size = Bounded(q["pageSize"].FirstOrDefault(), 50, 1, VariantListQuery.MaxPageSize);
        if (search.Length > VariantListQuery.SearchMaxLength || country is { Length: not 2 } || status is null || descending is null || page is null || size is null)
        {
            return Results.BadRequest(InvalidQuery);
        }

        var result = await reads.ListSuppliersAsync(new SupplierListQuery(search.Length == 0 ? null : search, country, status.Value, descending.Value, page.Value, size.Value), cancellationToken);
        return Results.Ok(new PageResponse<SupplierRowResponse>(
            [.. result.Items.Select(r => new SupplierRowResponse(
                r.Supplier.Id, r.Supplier.Code, r.Supplier.Name, r.Supplier.CountryCode, r.Supplier.City, r.Supplier.ContactName, r.Supplier.IsArchived ? "archived" : "active",
                r.VariantCount, r.PrimaryCount, r.Supplier.Version))],
            result.PageNumber, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> GetSupplierAsync(Guid id, ClaimsPrincipal principal, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var supplier = await reads.GetSupplierAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var variants = Has(principal, Permissions.CatalogRead)
            ? (await reads.SupplierVariantsAsync(id, cancellationToken)).Select(v => new SupplierVariantResponse(
                v.Link.VariantId, v.ModelId, v.Sku, v.Label, v.ModelName, StockPolicies.ToCode(v.Policy), v.IsArchived ? "archived" : "active", v.Link.IsPrimary, v.Link.SupplierReference)).ToList()
            : null;
        return Results.Ok(new SupplierDetailResponse(
            supplier.Id, supplier.Code, supplier.Name, supplier.CountryCode, supplier.ContactName, supplier.Email, supplier.Phone, supplier.AddressLine, supplier.PostalCode,
            supplier.City, supplier.LegalIdentifiers, supplier.Notes, supplier.IsArchived ? "archived" : "active", supplier.ArchiveReason, supplier.Version, variants));
    }

    private static async Task<IResult> CreateSupplierAsync(HttpRequest request, ClaimsPrincipal principal, SupplierService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<SupplierRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (body!.Version is not null)
        {
            return Results.BadRequest(InvalidBody);
        }

        var outcome = await service.CreateAsync(body.ToInput(), body.RequestId!.Value, UserId(principal), cancellationToken, body.AllowDuplicateName ?? false);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)), $"/api/suppliers/{outcome.Value}");
    }

    private static async Task<IResult> UpdateSupplierAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, SupplierService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<SupplierRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.UpdateAsync(id, body!.ToInput(), body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> ArchiveSupplierAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, SupplierService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<SupplierArchiveRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.ArchiveAsync(id, body!.Reason, body.AcceptIncompleteVariants ?? false, body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> ReactivateSupplierAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, SupplierService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<VersionRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.ReactivateAsync(id, body!.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    internal static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
