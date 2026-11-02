using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using MMV.Api.Clients;
using MMV.Api.Installation;
using MMV.Api.Realtime;
using MMV.Application.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using static MMV.Api.Catalog.CatalogApi;

namespace MMV.Api.Catalog;

// ---------- requêtes ----------

internal sealed record MovementRequest(
    Guid? RequestId = null, Guid? VariantId = null, Guid? LocationId = null, string? Type = null, int? Quantity = null, string? ReasonCode = null, string? Comment = null);

internal sealed record AdjustmentRequest(
    Guid? RequestId = null, Guid? VariantId = null, Guid? LocationId = null, int? ExpectedOnHand = null, int? CountedQuantity = null, string? ReasonCode = null, string? Comment = null);

internal sealed record ThresholdRequest(Guid? RequestId = null, uint? Version = null, Guid? StoreId = null, int? LowStockThreshold = null);

internal sealed record ReceiptLineRequest(Guid? VariantId = null, int? AnnouncedQuantity = null, int? ReceivedQuantity = null, string? UnitCostHt = null);

internal sealed record ReceiptRequest(
    Guid? RequestId = null, Guid? LocationId = null, Guid? SupplierId = null, string? Currency = null, string? SupplierDocumentReference = null, string? Notes = null,
    Guid? CompletesReceiptId = null, List<ReceiptLineRequest>? Lines = null);

internal sealed record InventoryOpenRequest(
    Guid? RequestId = null, Guid? StoreId = null, List<Guid>? LocationIds = null, string? ScopeType = null, string? ScopeFamilyCode = null, List<Guid>? VariantIds = null, bool? Blind = null);

internal sealed record CountRequest(Guid? RequestId = null, uint? Version = null, int? CountedQuantity = null);

internal sealed record JustifyRequest(Guid? RequestId = null, uint? Version = null, string? ReasonCode = null, string? Comment = null);

internal sealed record ValidateRequest(Guid? RequestId = null, uint? Version = null, string? Uncounted = null, bool? AcceptChangedLines = null);

internal sealed record ImportUploadRequest(Guid? RequestId = null, string? Kind = null, Guid? StoreId = null, string? FileName = null, string? Content = null);

internal sealed record ImportCheckRequest(Guid? RequestId = null, uint? Version = null, Dictionary<string, string>? Mapping = null);

// ---------- réponses ----------

internal sealed record BalanceRowResponse(
    Guid VariantId, Guid ModelId, string Sku, string Label, string ModelName, string? Brand, string FamilyCode, Guid LocationId, string LocationCode,
    int OnHand, int Reserved, int Available, int? LowStockThreshold, string Availability, uint Version, uint ThresholdVersion);

internal sealed record BalancePageResponse(IReadOnlyList<BalanceRowResponse> Items, int Page, int PageSize, int TotalCount, DateTimeOffset AsOfUtc);

internal sealed record AlertResponse(Guid VariantId, Guid ModelId, string Sku, string Label, string ModelName, int OnHand, int Reserved, int Available, int? LowStockThreshold, string Availability);

internal sealed record AlertsResponse(IReadOnlyList<AlertResponse> Items, int LowStockCount, int OutOfStockCount, DateTimeOffset AsOfUtc);

internal sealed record MovementResponse(
    Guid Id, long Seq, Guid VariantId, string Sku, string Label, Guid LocationId, string LocationCode, string Type, int QuantityDelta, int QuantityBefore, int QuantityAfter,
    string ReasonCode, string? Comment, string? SupplierName, string? SourceType, Guid? SourceId, string? SourceNumber, DateTimeOffset PerformedAtUtc, ActorResponse PerformedBy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UnitCostHt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Currency);

internal sealed record AdjustmentResponse(Guid? MovementId, bool Replayed);

internal sealed record ReceiptSummaryResponse(
    Guid Id, string Number, Guid StoreId, Guid LocationId, string LocationCode, Guid SupplierId, string SupplierName, string? SupplierDocumentReference, Guid? CompletesReceiptId,
    DateTimeOffset ReceivedAtUtc, ActorResponse ReceivedBy, int LineCount, int ReceivedQuantity, int? OutstandingQuantity);

internal sealed record ReceiptLineResponse(
    int LineNo, Guid VariantId, string Sku, string Label, int? AnnouncedQuantity, int ReceivedQuantity, Guid? MovementId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UnitCostHt);

internal sealed record OutstandingResponse(Guid VariantId, int Announced, int Received, int Outstanding);

internal sealed record ReceiptDetailResponse(ReceiptSummaryResponse Summary, string Currency, string? Notes, IReadOnlyList<ReceiptLineResponse> Lines, IReadOnlyList<OutstandingResponse> Outstanding, bool CostsVisible);

internal sealed record InventorySummaryResponse(
    Guid Id, string Number, Guid StoreId, IReadOnlyList<Guid> LocationIds, string ScopeType, string? ScopeFamilyCode, bool Blind, string Status, DateTimeOffset OpenedAtUtc,
    ActorResponse OpenedBy, DateTimeOffset? ValidatedAtUtc, DateTimeOffset? AbandonedAtUtc, string? AbandonReason, string? UncountedResolution, int LineCount, int CountedCount,
    int DifferenceCount, uint Version);

/// <summary>Ligne : en session ouverte À L'AVEUGLE, quantités théoriques et écarts ne sont pas servis.</summary>
internal sealed record InventoryLineResponse(
    Guid VariantId, Guid LocationId, string Sku, string Label, string ModelName, string LocationCode, int? ExpectedAtOpen, int? ExpectedAtCount, int? CountedQuantity, int? Difference,
    int? CurrentOnHand, bool ChangedSinceCount, string? CountedBy, DateTimeOffset? CountedAtUtc, string? ReasonCode, string? Comment, string? Resolution, int? AppliedDelta, uint Version);

internal sealed record InventoryDetailResponse(InventorySummaryResponse Summary, IReadOnlyList<InventoryLineResponse> Lines);

internal sealed record ImportFieldResponse(string Key, bool Required);

internal sealed record ImportPreviewResponse(Guid BatchId, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> SampleRows, IReadOnlyDictionary<string, string> SuggestedMapping, int RowCount, IReadOnlyList<ImportFieldResponse> Fields, bool Replayed);

internal sealed record ImportBatchResponse(
    Guid Id, string Kind, Guid? StoreId, string FileName, string FileSha256, int RowCount, string Status, int? ErrorCount, bool ContainsCosts, bool ContainsManualPrices,
    DateTimeOffset UploadedAtUtc, string UploadedBy, DateTimeOffset? CheckedAtUtc, DateTimeOffset? AppliedAtUtc, IReadOnlyDictionary<string, string> Mapping, uint Version);

internal sealed record ImportRowResponse(int LineNo, string Outcome, IReadOnlyList<ImportError> Errors, IReadOnlyDictionary<string, string>? Values);

internal sealed record ImportReportResponse(ImportBatchResponse Batch, ImportSummary Summary, IReadOnlyList<ImportRowResponse> Rows, bool ValuesVisible);

/// <summary>
/// Routes Stock (05-B), Inventaires (05-C) et Import CSV (05-D). Coûts de réception projetés selon <c>costs.read</c> ;
/// comptage (<c>inventory.count</c>) et validation (<c>inventory.validate</c>) séparés ; aucune route <c>DELETE</c>.
/// </summary>
internal static class StockEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapStockEndpoints(this RouteGroupBuilder api)
    {
        var stock = api.MapGroup("/stock").WithTags("Stock").AddEndpointFilter(NoStore).RequireActiveStore().PublishesChanges(ChangeTopics.Stock, ChangeTopics.Catalog);
        stock.MapGet("/balances", ListBalancesAsync).RequireAuthorization(P(Permissions.StockRead));
        stock.MapGet("/alerts", AlertsAsync).RequireAuthorization(P(Permissions.StockRead));
        stock.MapPut("/thresholds/{variantId:guid}", SetThresholdAsync).RequireAuthorization(P(Permissions.StockAdjust));
        stock.MapGet("/movements", ListMovementsAsync).RequireAuthorization(P(Permissions.StockRead));
        stock.MapGet("/movements/{id:guid}", GetMovementAsync).RequireAuthorization(P(Permissions.StockRead));
        stock.MapPost("/movements", RecordMovementAsync).RequireAuthorization(P(Permissions.StockMovementsWrite));
        stock.MapPost("/adjustments", AdjustAsync).RequireAuthorization(P(Permissions.StockAdjust));
        stock.MapGet("/receipts", ListReceiptsAsync).RequireAuthorization(P(Permissions.StockRead));
        stock.MapGet("/receipts/{id:guid}", GetReceiptAsync).RequireAuthorization(P(Permissions.StockRead));
        stock.MapPost("/receipts", ReceiveAsync).RequireAuthorization(P(Permissions.StockMovementsWrite), P(Permissions.SuppliersRead));

        var inventories = api.MapGroup("/inventories").WithTags("Inventories").AddEndpointFilter(NoStore).RequireActiveStore()
            .PublishesChanges(ChangeTopics.Inventory, ChangeTopics.Stock, ChangeTopics.Catalog);
        inventories.MapGet("/", ListInventoriesAsync).RequireAuthorization(P(Permissions.StockRead));
        inventories.MapGet("/{id:guid}", GetInventoryAsync).RequireAuthorization(P(Permissions.StockRead));
        inventories.MapPost("/", OpenInventoryAsync).RequireAuthorization(P(Permissions.InventoryCount));
        inventories.MapPut("/{id:guid}/lines/{variantId:guid}/{locationId:guid}", CountAsync).RequireAuthorization(P(Permissions.InventoryCount));
        inventories.MapPut("/{id:guid}/lines/{variantId:guid}/{locationId:guid}/reason", JustifyAsync).RequireAuthorization(P(Permissions.InventoryValidate));
        inventories.MapPost("/{id:guid}/validate", ValidateAsync).RequireAuthorization(P(Permissions.InventoryValidate));
        inventories.MapPost("/{id:guid}/abandon", AbandonAsync).RequireAuthorization(P(Permissions.InventoryValidate));

        var imports = api.MapGroup("/imports").WithTags("Imports").AddEndpointFilter(NoStore).RequireActiveStore().RequireAuthorization(P(Permissions.CatalogImport))
            .PublishesChanges(ChangeTopics.Imports, ChangeTopics.Catalog, ChangeTopics.Pricing, ChangeTopics.Suppliers, ChangeTopics.Stock);
        imports.MapGet("/fields", () => Results.Ok(ImportService.Fields.ToDictionary(k => k.Key, k => k.Value.Select(f => new ImportFieldResponse(f.Key, f.Required)).ToList())));
        imports.MapGet("/", ListImportsAsync);
        imports.MapPost("/", UploadAsync);
        imports.MapGet("/{id:guid}", ReportAsync);
        imports.MapPost("/{id:guid}/check", CheckAsync);
        imports.MapPost("/{id:guid}/apply", ApplyAsync);
        imports.MapPost("/{id:guid}/cancel", CancelAsync);
    }

    // ---------- soldes, alertes, seuils ----------

    private static async Task<IResult> ListBalancesAsync(HttpRequest request, ICatalogReadStore reads, ICatalogStore store, TimeProvider clock, CancellationToken cancellationToken)
    {
        var q = request.Query;
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId", "locationId", "q", "familyCode", "availability", "sort", "dir", "page", "pageSize" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var location = q.ContainsKey("locationId") ? ParseGuid(q["locationId"]) : null;
        var availabilityText = q["availability"].ToString();
        var availability = availabilityText.Length == 0 ? null : Availabilities.FromCode(availabilityText);
        var search = q["q"].ToString().Trim();
        var family = q["familyCode"].ToString() is { Length: > 0 } f ? f : null;
        var sort = Sort(q["sort"].FirstOrDefault());
        var descending = Descending(q["dir"].FirstOrDefault());
        var page = Bounded(q["page"].FirstOrDefault(), 1, 1, VariantListQuery.MaxPage);
        var size = Bounded(q["pageSize"].FirstOrDefault(), 50, 1, VariantListQuery.MaxPageSize);
        if ((q.ContainsKey("locationId") && location is null) || (availabilityText.Length > 0 && availability is null) || search.Length > VariantListQuery.SearchMaxLength
            || (family is not null && Domain.Catalog.Families.Find(family) is null) || sort is null || descending is null || page is null || size is null)
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (location is { } locationId && !await OwnsLocationAsync(request, store, locationId, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("location_not_found"));
        }

        var asOf = clock.GetUtcNow();
        var result = await reads.ListBalancesAsync(
            new BalanceQuery(storeId, location, search.Length == 0 ? null : search, family, availability, sort.Value, descending.Value, page.Value, size.Value), cancellationToken);
        return Results.Ok(new BalancePageResponse(
            [.. result.Items.Select(b => new BalanceRowResponse(
                b.VariantId, b.ModelId, b.Sku, b.Label, b.ModelName, b.Brand, b.FamilyCode, b.LocationId, b.LocationCode, b.OnHand, b.Reserved, b.Available, b.LowStockThreshold,
                Availabilities.ToCode(b.Availability), b.Version ?? 0, b.ThresholdVersion ?? 0))],
            result.PageNumber, result.PageSize, result.TotalCount, asOf));
    }

    private static async Task<IResult> AlertsAsync(HttpRequest request, ICatalogReadStore reads, TimeProvider clock, CancellationToken cancellationToken)
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

        var asOf = clock.GetUtcNow();
        var alerts = await reads.AlertsAsync(storeId, cancellationToken);
        return Results.Ok(new AlertsResponse(
            [.. alerts.Select(a => new AlertResponse(a.VariantId, a.ModelId, a.Sku, a.Label, a.ModelName, a.OnHand, a.Reserved, a.Available, a.LowStockThreshold, Availabilities.ToCode(a.Availability)))],
            alerts.Count(a => a.Availability == Availability.LowStock), alerts.Count(a => a.Availability == Availability.OutOfStock), asOf));
    }

    private static async Task<IResult> SetThresholdAsync(Guid variantId, HttpRequest request, ClaimsPrincipal principal, StockService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ThresholdRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var outcome = await service.SetThresholdAsync(variantId, shop.Id, body.LowStockThreshold, body.Version, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    // ---------- mouvements ----------

    private static MovementResponse ToMovement(MovementView view, bool costs) => new(
        view.Movement.Id, view.Movement.Seq, view.Movement.VariantId, view.Sku, view.Label, view.Movement.LocationId, view.LocationCode, view.Movement.MovementType,
        view.Movement.QuantityDelta, view.Movement.QuantityBefore, view.Movement.QuantityAfter, view.Movement.ReasonCode, view.Movement.Comment, view.SupplierName,
        view.Movement.SourceType, view.Movement.SourceId, view.SourceNumber, view.Movement.PerformedAtUtc,
        new ActorResponse(view.Movement.PerformedByUserId, view.PerformedByName),
        costs ? Cost(view.Movement.UnitCostHt) : null, costs ? view.Movement.CurrencyCode : null);

    private static async Task<IResult> ListMovementsAsync(HttpRequest request, ClaimsPrincipal principal, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var q = request.Query;
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId", "locationId", "variantId", "type", "from", "to", "page", "pageSize" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var location = q.ContainsKey("locationId") ? ParseGuid(q["locationId"]) : null;
        var variant = q.ContainsKey("variantId") ? ParseGuid(q["variantId"]) : null;
        var type = q["type"].ToString() is { Length: > 0 } t ? t : null;
        DateTimeOffset? from = q.ContainsKey("from") && DateTimeOffset.TryParse(q["from"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var f) ? f : null;
        DateTimeOffset? to = q.ContainsKey("to") && DateTimeOffset.TryParse(q["to"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var u) ? u : null;
        var page = Bounded(q["page"].FirstOrDefault(), 1, 1, VariantListQuery.MaxPage);
        var size = Bounded(q["pageSize"].FirstOrDefault(), 50, 1, VariantListQuery.MaxPageSize);
        if ((q.ContainsKey("locationId") && location is null) || (q.ContainsKey("variantId") && variant is null) || (type is not null && !MovementTypes.Phase05.Contains(type))
            || (q.ContainsKey("from") && from is null) || (q.ContainsKey("to") && to is null) || page is null || size is null)
        {
            return Results.BadRequest(InvalidQuery);
        }

        var result = await reads.ListMovementsAsync(new MovementQuery(storeId, location, variant, type, from, to, page.Value, size.Value), cancellationToken);
        var costs = Has(principal, Permissions.CostsRead);
        return Results.Ok(new PageResponse<MovementResponse>([.. result.Items.Select(m => ToMovement(m, costs))], result.PageNumber, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> GetMovementAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, ICatalogReadStore reads, ICatalogStore store, CancellationToken cancellationToken) =>
        await reads.GetMovementAsync(id, cancellationToken) is { } view && await OwnsLocationAsync(request, store, view.Movement.LocationId, cancellationToken)
            ? Results.Ok(ToMovement(view, Has(principal, Permissions.CostsRead)))
            : Results.NotFound(new ErrorResponse("not_found"));

    private static async Task<IResult> RecordMovementAsync(HttpRequest request, ClaimsPrincipal principal, StockService service, ICatalogReadStore reads, ICatalogStore store, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<MovementRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (body!.VariantId is not { } variantId || body.LocationId is not { } locationId)
        {
            return Results.BadRequest(InvalidBody);
        }

        if (!await OwnsLocationAsync(request, store, locationId, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("location_not_found"));
        }

        var outcome = await service.RecordMovementAsync(variantId, locationId, body.Type, body.Quantity, body.ReasonCode, body.Comment, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, async id => (object?)ToMovement((await reads.GetMovementAsync(id, cancellationToken))!, Has(principal, Permissions.CostsRead)), $"/api/stock/movements/{outcome.Value}");
    }

    private static async Task<IResult> AdjustAsync(HttpRequest request, ClaimsPrincipal principal, StockService service, ICatalogStore store, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdjustmentRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (body!.VariantId is not { } variantId || body.LocationId is not { } locationId)
        {
            return Results.BadRequest(InvalidBody);
        }

        if (!await OwnsLocationAsync(request, store, locationId, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("location_not_found"));
        }

        var outcome = await service.AdjustAsync(variantId, locationId, body.ExpectedOnHand, body.CountedQuantity, body.ReasonCode, body.Comment, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, id => Task.FromResult<object?>(new AdjustmentResponse(id, outcome.Replayed)));
    }

    // ---------- réceptions ----------

    private static ReceiptSummaryResponse ToReceipt(ReceiptSummaryView view) => new(
        view.Receipt.Id, view.Receipt.Number, view.Receipt.StoreId, view.Receipt.LocationId, view.LocationCode, view.Receipt.SupplierId, view.SupplierName,
        view.Receipt.SupplierDocumentReference, view.Receipt.CompletesReceiptId, view.Receipt.ReceivedAtUtc, new ActorResponse(view.Receipt.ReceivedByUserId, view.ReceivedByName),
        view.LineCount, view.ReceivedQuantity, view.OutstandingQuantity);

    private static async Task<IResult> ListReceiptsAsync(HttpRequest request, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var q = request.Query;
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId", "supplierId", "page", "pageSize" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        var storeId = shop.Id;

        var supplier = q.ContainsKey("supplierId") ? ParseGuid(q["supplierId"]) : null;
        var page = Bounded(q["page"].FirstOrDefault(), 1, 1, VariantListQuery.MaxPage);
        var size = Bounded(q["pageSize"].FirstOrDefault(), 50, 1, VariantListQuery.MaxPageSize);
        if ((q.ContainsKey("supplierId") && supplier is null) || page is null || size is null)
        {
            return Results.BadRequest(InvalidQuery);
        }

        var result = await reads.ListReceiptsAsync(storeId, supplier, page.Value, size.Value, cancellationToken);
        return Results.Ok(new PageResponse<ReceiptSummaryResponse>([.. result.Items.Select(ToReceipt)], result.PageNumber, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, ClaimsPrincipal principal, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var detail = await reads.GetReceiptAsync(id, cancellationToken);
        if (detail is null || !ActiveStore.Owns(context, detail.Summary.Receipt.StoreId))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var costs = Has(principal, Permissions.CostsRead);
        return Results.Ok(new ReceiptDetailResponse(
            ToReceipt(detail.Summary), detail.Summary.Receipt.CurrencyCode, detail.Summary.Receipt.Notes,
            [.. detail.Lines.Select(l => new ReceiptLineResponse(l.Line.LineNo, l.Line.VariantId, l.Sku, l.Label, l.Line.AnnouncedQuantity, l.Line.ReceivedQuantity, l.Line.MovementId, costs ? Cost(l.Line.UnitCostHt) : null))],
            [.. detail.Outstanding.Select(o => new OutstandingResponse(o.VariantId, o.Announced, o.Received, Math.Max(0, o.Announced - o.Received)))],
            costs));
    }

    private static async Task<IResult> ReceiveAsync(HttpRequest request, ClaimsPrincipal principal, StockService service, ICatalogReadStore reads, ICatalogStore store, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ReceiptRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (body!.LocationId is { } locationId && !await OwnsLocationAsync(request, store, locationId, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("location_not_found"));
        }

        var outcome = await service.ReceiveAsync(
            new ReceiptInput(body!.LocationId, body.SupplierId, body.Currency, body.SupplierDocumentReference, body.Notes, body.CompletesReceiptId,
                [.. (body.Lines ?? []).Select(l => new ReceiptLineInput(l.VariantId, l.AnnouncedQuantity, l.ReceivedQuantity, l.UnitCostHt))]),
            body.RequestId!.Value, Actor(principal), cancellationToken);
        return await ToResultAsync(outcome, async id => (object?)ToReceipt((await reads.GetReceiptAsync(id, cancellationToken))!.Summary), $"/api/stock/receipts/{outcome.Value}");
    }

    // ---------- inventaires ----------

    private static InventorySummaryResponse ToInventory(InventorySummaryView view) => new(
        view.Session.Id, view.Session.Number, view.Session.StoreId, view.Session.LocationIds, view.Session.ScopeType, view.Session.ScopeFamilyCode, view.Session.Blind,
        view.Session.Status, view.Session.OpenedAtUtc, new ActorResponse(view.Session.OpenedByUserId, view.OpenedByName), view.Session.ValidatedAtUtc, view.Session.AbandonedAtUtc,
        view.Session.AbandonReason, view.Session.UncountedResolution, view.LineCount, view.Session.Blind && view.Session.IsOpen ? view.LineCount : view.CountedCount,
        view.Session.Blind && view.Session.IsOpen ? 0 : view.DifferenceCount, view.Session.Version);

    private static async Task<IResult> ListInventoriesAsync(HttpRequest request, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var q = request.Query;
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "storeId", "status", "page", "pageSize" }))
        {
            return Results.BadRequest(InvalidQuery);
        }

        if (QueryStore(request, out var shop) is { } refused)
        {
            return refused;
        }

        Guid? store = shop.Id;
        var status = q["status"].ToString() is { Length: > 0 } s ? s : null;
        var page = Bounded(q["page"].FirstOrDefault(), 1, 1, VariantListQuery.MaxPage);
        var size = Bounded(q["pageSize"].FirstOrDefault(), 50, 1, VariantListQuery.MaxPageSize);
        if (status is not (null or InventoryStatuses.Open or InventoryStatuses.Validated or InventoryStatuses.Abandoned)
            || page is null || size is null)
        {
            return Results.BadRequest(InvalidQuery);
        }

        var result = await reads.ListInventoriesAsync(store, status, page.Value, size.Value, cancellationToken);
        var items = result.Items.Select(ToInventory).ToList();
        return Results.Ok(new PageResponse<InventorySummaryResponse>(items, result.PageNumber, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> GetInventoryAsync(Guid id, HttpRequest request, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        if (!OnlyAllowed(request, new HashSet<string>(StringComparer.Ordinal) { "onlyDifferences" }) || (request.Query.ContainsKey("onlyDifferences") && ParseBoolean(request.Query["onlyDifferences"]) is null))
        {
            return Results.BadRequest(InvalidQuery);
        }

        var detail = await reads.GetInventoryAsync(id, cancellationToken);
        if (detail is null || !ActiveStore.Owns(request.HttpContext, detail.Summary.Session.StoreId))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var hidden = detail.Summary.Session.Blind && detail.Summary.Session.IsOpen;
        var onlyDifferences = ParseBoolean(request.Query["onlyDifferences"]) ?? false;
        var lines = detail.Lines
            .Where(l => !onlyDifferences || hidden || l.Line.Difference is not (null or 0))
            .Select(l => new InventoryLineResponse(
                l.Line.VariantId, l.Line.LocationId, l.Sku, l.Label, l.ModelName, l.LocationCode, hidden ? null : l.Line.ExpectedAtOpen, hidden ? null : l.Line.ExpectedAtCount,
                l.Line.CountedQuantity, hidden ? null : l.Line.Difference, hidden ? null : l.CurrentOnHand,
                !hidden && detail.Summary.Session.IsOpen && l.Line.ExpectedAtCount is { } e && e != l.CurrentOnHand,
                l.CountedByName, l.Line.CountedAtUtc, l.Line.ReasonCode, l.Line.Comment, l.Line.Resolution, l.Line.AppliedDelta, l.Line.Version))
            .ToList();
        return Results.Ok(new InventoryDetailResponse(ToInventory(detail.Summary), lines));
    }

    private static async Task<IResult> OpenInventoryAsync(HttpRequest request, ClaimsPrincipal principal, InventoryService service, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<InventoryOpenRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        var outcome = await service.OpenAsync(
            new InventoryOpenInput(shop.Id, body.LocationIds, body.ScopeType, body.ScopeFamilyCode, body.VariantIds, body.Blind ?? false),
            body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, async id => (object?)ToInventory((await reads.GetInventoryAsync(id, cancellationToken))!.Summary), $"/api/inventories/{outcome.Value}");
    }

    private static async Task<IResult> CountAsync(Guid id, Guid variantId, Guid locationId, HttpRequest request, ClaimsPrincipal principal, InventoryService service, IInventoryStore sessions, CancellationToken cancellationToken)
    {
        if (!await OwnsSessionAsync(request, sessions, id, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<CountRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.CountAsync(id, variantId, locationId, body!.CountedQuantity, body.Version, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> JustifyAsync(Guid id, Guid variantId, Guid locationId, HttpRequest request, InventoryService service, IInventoryStore sessions, CancellationToken cancellationToken)
    {
        if (!await OwnsSessionAsync(request, sessions, id, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<JustifyRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.JustifyAsync(id, variantId, locationId, body!.ReasonCode, body.Comment, body.Version, body.RequestId!.Value, cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> ValidateAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, InventoryService service, IInventoryStore sessions, CancellationToken cancellationToken)
    {
        if (!await OwnsSessionAsync(request, sessions, id, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<ValidateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.ValidateAsync(id, body!.Uncounted, body.AcceptChangedLines ?? false, body.Version, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    private static async Task<IResult> AbandonAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, InventoryService service, IInventoryStore sessions, CancellationToken cancellationToken)
    {
        if (!await OwnsSessionAsync(request, sessions, id, cancellationToken))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<ArchiveRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.AbandonAsync(id, body!.Reason, body.Version, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, x => Task.FromResult<object?>(new MutationResponse(x, outcome.Replayed)));
    }

    /// <summary>Session d'inventaire du magasin de l'installation (lecture sans verrou ; le service reverrouille).</summary>
    private static async Task<bool> OwnsSessionAsync(HttpRequest request, IInventoryStore sessions, Guid id, CancellationToken cancellationToken) =>
        await sessions.FindSessionAsync(id, lockForUpdate: false, cancellationToken) is { } session && ActiveStore.Owns(request.HttpContext, session.StoreId);

    /// <summary>Lot d'import sans magasin (catalogue, fournisseurs…) ou du magasin de l'installation.</summary>
    private static bool Visible(HttpContext context, ImportBatch batch) => batch.StoreId is not { } storeId || ActiveStore.Owns(context, storeId);

    // ---------- import CSV ----------

    private static ImportBatchResponse ToBatch(ImportBatch batch, string uploadedBy) => new(
        batch.Id, batch.Kind, batch.StoreId, batch.FileName, batch.FileSha256, batch.RowCount, batch.Status, batch.ErrorCount, batch.ContainsCosts, batch.ContainsManualPrices,
        batch.UploadedAtUtc, uploadedBy, batch.CheckedAtUtc, batch.AppliedAtUtc,
        JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson, Json) ?? [], batch.Version);

    private static async Task<IResult> ListImportsAsync(HttpContext context, ICatalogReadStore reads, CancellationToken cancellationToken) =>
        Results.Ok((await reads.ListImportsAsync(50, cancellationToken)).Where(b => Visible(context, b.Batch)).Select(b => ToBatch(b.Batch, b.UploadedByName)));

    private static async Task<IResult> UploadAsync(HttpRequest request, ClaimsPrincipal principal, ImportService service, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ImportUploadRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (BodyStore(request, body!.StoreId, out var shop) is { } refused)
        {
            return refused;
        }

        // Tarifs : magasin de l'installation par défaut ; autres types : aucun magasin (refus du service si fourni).
        var storeId = body.Kind == ImportKinds.Prices ? shop.Id : body.StoreId;
        var outcome = await service.UploadAsync(body.Kind, storeId, body.FileName, body.Content, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, p => Task.FromResult<object?>(new ImportPreviewResponse(
            p.BatchId, p.Headers, p.SampleRows, p.SuggestedMapping, p.RowCount,
            [.. ImportService.Fields[body.Kind!].Select(f => new ImportFieldResponse(f.Key, f.Required))], outcome.Replayed)), $"/api/imports/{outcome.Value?.BatchId}");
    }

    /// <summary>Rapport ; valeurs des lignes d'un lot contenant des coûts servies seulement avec <c>costs.read</c>.</summary>
    private static async Task<IResult> ReportAsync(Guid id, HttpContext context, ClaimsPrincipal principal, IImportStore imports, ImportService service, ICatalogReadStore reads, CancellationToken cancellationToken)
    {
        var batch = await imports.FindBatchAsync(id, cancellationToken);
        if (batch is null || !Visible(context, batch))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var visible = !batch.ContainsCosts && batch.Kind != ImportKinds.Prices || Has(principal, Permissions.CostsRead);
        var rows = await imports.RowsAsync(id, cancellationToken);
        var names = await reads.DisplayNamesAsync([batch.UploadedByUserId], cancellationToken);
        return Results.Ok(new ImportReportResponse(
            ToBatch(batch, names.GetValueOrDefault(batch.UploadedByUserId, "Compte inconnu")),
            await service.SummaryAsync(batch, cancellationToken),
            [.. rows.Select(r => new ImportRowResponse(
                r.LineNo, r.Outcome, JsonSerializer.Deserialize<List<ImportError>>(r.ErrorsJson, Json) ?? [],
                visible ? JsonSerializer.Deserialize<Dictionary<string, string>>(r.ValuesJson, Json) : null))],
            visible));
    }

    private static async Task<IResult> CheckAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, ImportService service, IImportStore imports, CancellationToken cancellationToken)
    {
        if (await imports.FindBatchAsync(id, cancellationToken) is { } owned && !Visible(request.HttpContext, owned))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<ImportCheckRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.CheckAsync(id, body!.Mapping, body.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, s => Task.FromResult<object?>(s));
    }

    private static async Task<IResult> ApplyAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, ImportService service, IImportStore imports, CancellationToken cancellationToken)
    {
        if (await imports.FindBatchAsync(id, cancellationToken) is { } owned && !Visible(request.HttpContext, owned))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<VersionRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.ApplyAsync(id, body!.Version!.Value, body.RequestId!.Value, Actor(principal), cancellationToken);
        return await ToResultAsync(outcome, s => Task.FromResult<object?>(s));
    }

    private static async Task<IResult> CancelAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, ImportService service, IImportStore imports, CancellationToken cancellationToken)
    {
        if (await imports.FindBatchAsync(id, cancellationToken) is { } owned && !Visible(request.HttpContext, owned))
        {
            return Results.NotFound(new ErrorResponse("not_found"));
        }

        var body = await ReadBodyAsync<VersionRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.CancelAsync(id, body!.Version!.Value, body.RequestId!.Value, UserId(principal), cancellationToken);
        return await ToResultAsync(outcome, s => Task.FromResult<object?>(s));
    }

    internal static string Format(decimal? value) => MoneyText.Format(value, MoneyText.SaleScale) ?? string.Empty;
}
