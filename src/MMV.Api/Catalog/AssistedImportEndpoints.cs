using System.Security.Claims;
using MMV.Api.Clients;
using MMV.Api.Installation;
using MMV.Application.Catalog;
using MMV.Application.Catalog.Assisted;
using static MMV.Api.Catalog.CatalogApi;

namespace MMV.Api.Catalog;

internal sealed record AssistantInspectRequest(string? FileName, string? ContentBase64);

internal sealed record AssistantCreateRequest(Guid? RequestId, string? FileName, string? ContentBase64, Guid? SupplierId, string? Target, string? Analysis);

internal sealed record AssistantAnalysisRequest(Guid? RequestId, uint? Version);

internal sealed record AssistantDecisionsRequest(Guid? RequestId, int? BaseRevision, List<DecisionInput>? Decisions);

internal sealed record AssistantCheckRequest(Guid? RequestId, uint? Version, int? Revision);

internal sealed record AssistantApplyRequest(Guid? RequestId, uint? Version, int? Revision, bool? Confirmed);

/// <summary>
/// Routes ADDITIVES du nouvel assistant d'import (Gate B § 7) sous le groupe <c>/api/imports</c> (magasin actif,
/// <c>catalog.import</c>, <c>no-store</c>, notifications temps réel). Les routes actuelles sont inchangées et refusent un
/// lot du nouveau parcours (<c>409 pipeline_mismatch</c>). Écran désactivé (<c>CatalogInterpreter:WizardEnabled</c> faux,
/// par défaut tant que 0009 n'est pas appliquée) : <c>404 assistant_disabled</c>, sauf l'état du service.
/// Refus techniques du dépôt : <c>400</c> avec leur code ; aucune anomalie métier ne refuse un fichier.
/// </summary>
internal static class AssistedImportEndpoints
{
    private static readonly HashSet<string> TechnicalRefusals =
        ["too_large", "too_many_rows", "empty_file", "binary_content", "spreadsheet_not_csv", "undecodable", "invalid_base64"];

    public static void Map(RouteGroupBuilder imports)
    {
        imports.MapGet("/assistant/status", async (AssistedImportService service, CancellationToken ct) => Results.Ok(await service.StatusAsync(ct)));
        imports.MapGet("/summary", async (HttpContext context, AssistedImportOptions options, IServiceProvider services, CancellationToken ct) =>
            options.WizardEnabled ? Results.Ok(await services.GetRequiredService<AssistedImportService>().SummaryAsync(ct)) : Disabled());
        imports.MapPost("/assistant/inspect", InspectAsync);
        imports.MapPost("/assistant", CreateAsync);
        imports.MapGet("/{id:guid}/assistant", GetAsync);
        imports.MapGet("/{id:guid}/assistant/analysis", AnalysisAsync);
        imports.MapPost("/{id:guid}/assistant/analysis", RetryAsync);
        imports.MapDelete("/{id:guid}/assistant/analysis", CancelAnalysisAsync);
        // GATE D (07/11/2026, recette native) : la politique CORS du poste (desktop-api) n'admet jamais DELETE (phase 02) ;
        // dans Tauri, l'annulation en DELETE était refusée avant d'atteindre le serveur. Même traitement en POST pour le poste.
        imports.MapPost("/{id:guid}/assistant/analysis/cancel", CancelAnalysisAsync);
        imports.MapGet("/{id:guid}/assistant/rows", RowsAsync);
        imports.MapGet("/{id:guid}/assistant/issues/{key}", IssueAsync);
        imports.MapPost("/{id:guid}/assistant/decisions", DecideAsync);
        imports.MapPost("/{id:guid}/assistant/check", CheckAsync);
        imports.MapPost("/{id:guid}/assistant/apply", ApplyAsync);
    }

    private static IResult Disabled() => Results.NotFound(new ErrorResponse("assistant_disabled"));

    private static async Task<IResult> Map<T>(Outcome<T> outcome, string? location = null)
    {
        if (outcome.Kind == OutcomeKind.Refused && outcome.Code is { } code && TechnicalRefusals.Contains(code))
        {
            return Results.BadRequest(new ErrorResponse(code));
        }

        return await ToResultAsync(outcome, v => Task.FromResult<object?>(v), location);
    }

    private static async Task<IResult> InspectAsync(HttpRequest request, AssistedImportOptions options, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        var body = await ReadBodyAsync<AssistantInspectRequest>(request, cancellationToken);
        return body is null ? Results.BadRequest(InvalidBody) : await Map(AssistedImportService.Inspect(body.ContentBase64));
    }

    private static async Task<IResult> CreateAsync(HttpRequest request, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        var body = await ReadBodyAsync<AssistantCreateRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        // Montants confirmés : tarif du magasin de l'installation (jamais un autre magasin).
        var shop = ActiveStore.Of(request.HttpContext);
        var outcome = await service.CreateAsync(body!.RequestId!.Value, body.FileName, body.ContentBase64, body.SupplierId, body.Target, body.Analysis, shop.Id, Actor(principal), cancellationToken);
        return await Map(outcome, $"/api/imports/{outcome.Value?.BatchId}/assistant");
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken) =>
        options.WizardEnabled ? await Map(await service.GetAsync(id, Actor(principal), cancellationToken)) : Disabled();

    private static async Task<IResult> AnalysisAsync(Guid id, HttpRequest request, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        var after = 0L;
        if (request.Query.TryGetValue("after", out var value) && (!long.TryParse(value, out after) || after < 0))
        {
            return Results.BadRequest(InvalidQuery);
        }

        return await Map(await service.AnalysisAsync(id, after, cancellationToken));
    }

    private static async Task<IResult> RetryAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        var body = await ReadBodyAsync<AssistantAnalysisRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        var outcome = await service.RetryAsync(id, body!.RequestId!.Value, body.Version!.Value, Actor(principal), cancellationToken);
        return outcome.IsSuccess ? Results.Json(outcome.Value, statusCode: StatusCodes.Status202Accepted) : await Map(outcome);
    }

    private static async Task<IResult> CancelAnalysisAsync(Guid id, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken) =>
        options.WizardEnabled ? await Map(await service.CancelAnalysisAsync(id, Actor(principal), cancellationToken)) : Disabled();

    private static async Task<IResult> RowsAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        if (!OnlyAllowed(request, new HashSet<string>(["filter", "page", "pageSize"], StringComparer.Ordinal)))
        {
            return Results.BadRequest(InvalidQuery);
        }

        var page = request.Query.TryGetValue("page", out var p) && int.TryParse(p, out var pv) ? pv : 1;
        var size = request.Query.TryGetValue("pageSize", out var s) && int.TryParse(s, out var sv) ? sv : 50;
        if (page < 1 || size is < 1 or > AssistedImportService.MaxPageSize)
        {
            return Results.BadRequest(InvalidQuery);
        }

        return await Map(await service.RowsAsync(id, request.Query["filter"].FirstOrDefault(), page, size, Actor(principal), cancellationToken));
    }

    private static async Task<IResult> IssueAsync(Guid id, string key, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken) =>
        !options.WizardEnabled ? Disabled()
        : key.Length is 0 or > 200 ? Results.BadRequest(InvalidQuery)
        : await Map(await service.IssueAsync(id, key, Actor(principal), cancellationToken));

    private static async Task<IResult> DecideAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        var body = await ReadBodyAsync<AssistantDecisionsRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, null, requireVersion: false) is { } invalid)
        {
            return invalid;
        }

        if (body!.BaseRevision is null)
        {
            return Results.BadRequest(new ErrorResponse("base_revision_required"));
        }

        return await Map(await service.DecideAsync(id, body.RequestId!.Value, body.BaseRevision.Value, body.Decisions, Actor(principal), cancellationToken));
    }

    private static async Task<IResult> CheckAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        var body = await ReadBodyAsync<AssistantCheckRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        return body!.Revision is null
            ? Results.BadRequest(new ErrorResponse("revision_required"))
            : await Map(await service.CheckAsync(id, body.RequestId!.Value, body.Version!.Value, body.Revision.Value, Actor(principal), cancellationToken));
    }

    private static async Task<IResult> ApplyAsync(Guid id, HttpRequest request, ClaimsPrincipal principal, AssistedImportOptions options, AssistedImportService service, CancellationToken cancellationToken)
    {
        if (!options.WizardEnabled)
        {
            return Disabled();
        }

        var body = await ReadBodyAsync<AssistantApplyRequest>(request, cancellationToken);
        if (Envelope(body, body?.RequestId, body?.Version, requireVersion: true) is { } invalid)
        {
            return invalid;
        }

        return body!.Revision is null
            ? Results.BadRequest(new ErrorResponse("revision_required"))
            : await Map(await service.ApplyAsync(id, body.RequestId!.Value, body.Version!.Value, body.Revision.Value, body.Confirmed == true, Actor(principal), cancellationToken));
    }
}
