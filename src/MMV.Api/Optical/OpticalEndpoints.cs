using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;
using MMV.Api.Auth;
using MMV.Api.Clients;
using MMV.Application.Audit;
using MMV.Application.Optical;
using MMV.Domain.Audit;
using MMV.Domain.Identity;
using MMV.Domain.Optical;

namespace MMV.Api.Optical;

/// <summary>
/// Routes du dossier optique (phase 04-B, contrat docs/phase/04/API_OPTICAL.md). DEUX familles d'écriture distinctes
/// (D-04-01) : <c>/prescriptions</c> (ordonnances médicales) et <c>/measurements</c> (mesures magasin), aucune route de
/// conversion, aucune route <c>DELETE</c>. Lecture croisée des deux types (<c>/records</c>) pour la liste et le dossier
/// d'un client. Autorisation exclusivement serveur, AVANT toute lecture (<see cref="AuthSetup.OpticalReadPolicy"/> et
/// suivantes). Chaque écriture exige <c>requestId</c> (idempotence : réémission → état courant, rien n'est réécrit) et,
/// sauf la création, la <c>version</c> lue. Réponses <c>Cache-Control: no-store</c> ; erreurs à codes bornés, jamais une
/// valeur saisie ; journaux limités aux identifiants techniques (aucune valeur optique, aucun nom).
/// </summary>
internal static partial class OpticalEndpoints
{
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    private static readonly HashSet<string> ListParameters = new(StringComparer.Ordinal)
    {
        "q", "kind", "status", "includeInvalidated", "sort", "dir", "page", "pageSize",
    };

    public static RouteGroupBuilder MapOpticalEndpoints(this RouteGroupBuilder api)
    {
        var optical = api.MapGroup("/optical")
            .WithTags("Optical")
            .AddEndpointFilter(NoStore)
            .WithMetadata(
                new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized, typeof(ErrorResponse), ["application/json"]),
                new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ErrorResponse), ["application/json"]));

        optical.MapGet("/records", ListAsync).WithName("ListOpticalRecords").RequireAuthorization(AuthSetup.OpticalReadPolicy)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                DescribeListParameters(operation);
                return Task.CompletedTask;
            });
        optical.MapGet("/clients/{clientId:guid}/records", ListForClientAsync).WithName("ListClientOpticalRecords").RequireAuthorization(AuthSetup.OpticalReadPolicy);
        optical.MapGet("/measurers", ListMeasurersAsync).WithName("ListOpticalMeasurers").RequireAuthorization(AuthSetup.OpticalReadPolicy);

        MapKind(optical, Kinds.Prescription);
        MapKind(optical, Kinds.Measurement);
        return optical;
    }

    // ---------- lecture croisée ----------

    private static async Task<Results<Ok<OpticalSummaryPageResponse>, BadRequest<ErrorResponse>>> ListAsync(
        HttpRequest request,
        OpticalQueryService service,
        CancellationToken cancellationToken,
        string? q = null,
        string? kind = null,
        string? status = null,
        string? includeInvalidated = null,
        string? sort = null,
        string? dir = null,
        string? page = null,
        string? pageSize = null)
    {
        var query = request.Query.All(p => ListParameters.Contains(p.Key) && p.Value.Count == 1)
            ? ParseListQuery(q, kind, status, includeInvalidated, sort, dir, page, pageSize)
            : null;
        if (query is null)
        {
            return TypedResults.BadRequest(new ErrorResponse("invalid_query"));
        }

        var result = await service.ListAsync(query, cancellationToken);
        return TypedResults.Ok(new OpticalSummaryPageResponse([.. result.Items.Select(ToSummary)], result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<Results<Ok<IReadOnlyList<OpticalSummaryResponse>>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>>> ListForClientAsync(
        Guid clientId, HttpRequest request, OpticalQueryService service, CancellationToken cancellationToken, string? includeInvalidated = null)
    {
        if (!request.Query.All(p => p.Key == "includeInvalidated" && p.Value.Count == 1) || ParseBoolean(includeInvalidated ?? "false") is not { } include)
        {
            return TypedResults.BadRequest(new ErrorResponse("invalid_query"));
        }

        if (await service.GetClientAsync(clientId, cancellationToken) is null)
        {
            return TypedResults.NotFound(new ErrorResponse("client_not_found"));
        }

        var items = await service.ListForClientAsync(clientId, include, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<OpticalSummaryResponse>>([.. items.Select(ToSummary)]);
    }

    private static async Task<Ok<IReadOnlyList<OpticalActorResponse>>> ListMeasurersAsync(OpticalQueryService service, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<OpticalActorResponse>>([.. (await service.ListMeasurersAsync(cancellationToken)).Select(a => new OpticalActorResponse(a.Id, a.DisplayName))]);

    /// <summary>Liste blanche et bornes ; toute autre valeur → <c>null</c> (400 invalid_query).</summary>
    internal static OpticalListQuery? ParseListQuery(
        string? q, string? kind, string? status, string? includeInvalidated, string? sort, string? dir, string? page, string? pageSize)
    {
        var search = q?.Trim();
        if (search is { Length: > OpticalListQuery.SearchMaxLength })
        {
            return null;
        }

        (bool Valid, OpticalKind? Value) kindFilter = (kind ?? "all") switch
        {
            "all" => (true, null),
            "prescription" => (true, OpticalKind.Prescription),
            "measurement" => (true, OpticalKind.Measurement),
            _ => (false, null),
        };
        OpticalStatusFilter? statusFilter = (status ?? "all") switch
        {
            "all" => OpticalStatusFilter.All,
            "draft" => OpticalStatusFilter.Draft,
            "confirmed" => OpticalStatusFilter.Confirmed,
            "rectification" => OpticalStatusFilter.Rectification,
            _ => null,
        };
        OpticalSortField? sortField = (sort ?? "date") switch
        {
            "date" => OpticalSortField.Date,
            "client" => OpticalSortField.Client,
            "recorded" => OpticalSortField.Recorded,
            _ => null,
        };
        bool? descending = (dir ?? "desc") switch
        {
            "asc" => false,
            "desc" => true,
            _ => null,
        };
        var include = ParseBoolean(includeInvalidated ?? "false");
        var pageNumber = ClientsEndpoints.BoundedInteger(page ?? "1", 1, OpticalListQuery.MaxPage);
        var size = ClientsEndpoints.BoundedInteger(pageSize ?? "20", 1, OpticalListQuery.MaxPageSize);

        return !kindFilter.Valid || statusFilter is null || sortField is null || descending is null || include is null || pageNumber is null || size is null
            ? null
            : new OpticalListQuery(
                string.IsNullOrEmpty(search) ? null : search, kindFilter.Value, statusFilter.Value, include.Value, sortField.Value, descending.Value,
                pageNumber.Value, size.Value);
    }

    private static bool? ParseBoolean(string value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    private static void DescribeListParameters(OpenApiOperation operation)
    {
        static OpenApiSchema Enumeration(string fallback, params string[] values) => new()
        {
            Type = JsonSchemaType.String,
            Enum = [.. values.Select(v => (JsonNode)JsonValue.Create(v))],
            Default = JsonValue.Create(fallback),
        };

        static OpenApiSchema Integer(int maximum, int fallback) => new()
        {
            Type = JsonSchemaType.Integer,
            Format = "int32",
            Minimum = "1",
            Maximum = maximum.ToString(CultureInfo.InvariantCulture),
            Default = JsonValue.Create(fallback),
        };

        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            parameter.Schema = parameter.Name switch
            {
                "q" => new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = OpticalListQuery.SearchMaxLength },
                "kind" => Enumeration("all", "all", "prescription", "measurement"),
                "status" => Enumeration("all", "all", "draft", "confirmed", "rectification"),
                "includeInvalidated" => Enumeration("false", "true", "false"),
                "sort" => Enumeration("date", "date", "client", "recorded"),
                "dir" => Enumeration("desc", "asc", "desc"),
                "page" => Integer(OpticalListQuery.MaxPage, 1),
                "pageSize" => Integer(OpticalListQuery.MaxPageSize, OpticalListQuery.DefaultPageSize),
                _ => parameter.Schema,
            };
        }
    }

    // ---------- routes d'un type ----------

    /// <summary>Description d'un type : service, conversions et droit d'écriture.</summary>
    private sealed record KindBinding<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance>(
        OpticalKind Kind,
        string Segment,
        string EntityType,
        string WritePolicy,
        Func<IServiceProvider, OpticalRecordService<TRecord, TRevision, TContent, TInput>> Service,
        Func<OpticalQueryService, Guid, CancellationToken, Task<TRecord?>> Get,
        Func<TContentRequest, TInput> ToInput,
        Func<TContent, TProvenance> Provenance,
        Func<TContent, IEnumerable<Guid>> CitedUsers)
        where TRecord : OpticalRecord<TRevision, TContent>
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
        where TContentRequest : class;

    private static class Kinds
    {
        public static readonly KindBinding<Prescription, PrescriptionRevision, PrescriptionContent, PrescriptionInput, PrescriptionContentRequest, PrescriptionProvenanceResponse> Prescription =
            new(
                OpticalKind.Prescription,
                "prescriptions",
                AuditActions.PrescriptionEntity,
                AuthSetup.OpticalPrescriptionsWritePolicy,
                services => services.GetRequiredService<PrescriptionService>(),
                (service, id, cancellationToken) => service.GetPrescriptionAsync(id, cancellationToken),
                request => request.ToInput(),
                content => PrescriptionProvenanceResponse.From(content.Provenance),
                _ => []);

        public static readonly KindBinding<Measurement, MeasurementRevision, MeasurementContent, MeasurementInput, MeasurementContentRequest, MeasurementProvenanceResponse> Measurement =
            new(
                OpticalKind.Measurement,
                "measurements",
                AuditActions.MeasurementEntity,
                AuthSetup.OpticalMeasurementsWritePolicy,
                services => services.GetRequiredService<MeasurementService>(),
                (service, id, cancellationToken) => service.GetMeasurementAsync(id, cancellationToken),
                request => request.ToInput(),
                content => MeasurementProvenanceResponse.From(content.Provenance),
                content => content.Provenance.MeasuredByUserId is { } id ? [id] : []);
    }

    private static void MapKind<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance>(
        RouteGroupBuilder optical, KindBinding<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance> kind)
        where TRecord : OpticalRecord<TRevision, TContent>
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
        where TContentRequest : class
    {
        var group = optical.MapGroup($"/{kind.Segment}");
        var name = kind.Kind == OpticalKind.Prescription ? "Prescription" : "Measurement";
        var location = $"/api/optical/{kind.Segment}";

        group.MapGet("/{id:guid}", async Task<Results<Ok<OpticalRecordResponse<TProvenance>>, NotFound<ErrorResponse>>> (
                Guid id, ClaimsPrincipal principal, OpticalQueryService service, CancellationToken cancellationToken) =>
            await kind.Get(service, id, cancellationToken) is { } record
                ? TypedResults.Ok(await DetailAsync(kind, record, principal, service, cancellationToken))
                : TypedResults.NotFound(NotFoundError))
            .WithName($"Get{name}").RequireAuthorization(AuthSetup.OpticalReadPolicy);

        group.MapPost("/", async Task<Results<Created<OpticalRecordResponse<TProvenance>>, Ok<OpticalRecordResponse<TProvenance>>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> (
                HttpRequest request, ClaimsPrincipal principal, IServiceProvider services, OpticalQueryService queries, ILoggerFactory loggers, CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<OpticalCreateRequest<TContentRequest>>(request, cancellationToken);
                if (body is null || body.Content is null)
                {
                    return TypedResults.BadRequest(InvalidBody);
                }

                if (body.RequestId is not { } requestId || requestId == Guid.Empty)
                {
                    return TypedResults.BadRequest(RequestIdRequired);
                }

                if (body.ClientId is not { } clientId)
                {
                    return TypedResults.BadRequest(new ErrorResponse("client_required"));
                }

                var userId = UserId(principal);
                var result = await kind.Service(services).CreateAsync(clientId, kind.ToInput(body.Content), body.Confirm ?? false, requestId, userId, cancellationToken);
                if (result.Status != OpticalWriteStatus.Success)
                {
                    var refused = Refuse(result.Status, result.Errors, Logger(loggers), kind.EntityType, Guid.Empty, userId);
                    return refused.Unprocessable is { } unprocessable ? unprocessable : refused.Missing is { } missing ? missing : refused.Conflict!;
                }

                var record = result.Record!;
                var logger = Logger(loggers);
                LogOpticalWrite(logger, kind.EntityType, result.Replayed ? "replayed" : "created", record.Id, userId);
                var detail = await DetailAsync(kind, await kind.Get(queries, record.Id, cancellationToken) ?? record, principal, queries, cancellationToken);
                return result.Replayed ? TypedResults.Ok(detail) : TypedResults.Created($"{location}/{record.Id}", detail);
            })
            .WithName($"Create{name}").RequireAuthorization(kind.WritePolicy).Accepts<OpticalCreateRequest<TContentRequest>>("application/json");

        group.MapPut("/{id:guid}/draft", (Guid id, HttpRequest request, ClaimsPrincipal principal, IServiceProvider services, OpticalQueryService queries, ILoggerFactory loggers, CancellationToken cancellationToken) =>
                SaveDraftAsync(kind, id, confirm: false, request, principal, services, queries, Logger(loggers), cancellationToken))
            .WithName($"Save{name}Draft").RequireAuthorization(kind.WritePolicy).Accepts<OpticalDraftRequest<TContentRequest>>("application/json");

        group.MapPost("/{id:guid}/draft/confirm", (Guid id, HttpRequest request, ClaimsPrincipal principal, IServiceProvider services, OpticalQueryService queries, ILoggerFactory loggers, CancellationToken cancellationToken) =>
                SaveDraftAsync(kind, id, confirm: true, request, principal, services, queries, Logger(loggers), cancellationToken))
            .WithName($"Confirm{name}Draft").RequireAuthorization(kind.WritePolicy).Accepts<OpticalDraftRequest<TContentRequest>>("application/json");

        group.MapPost("/{id:guid}/rectifications", async Task<Results<Created<OpticalRecordResponse<TProvenance>>, Ok<OpticalRecordResponse<TProvenance>>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> (
                Guid id, HttpRequest request, ClaimsPrincipal principal, IServiceProvider services, OpticalQueryService queries, ILoggerFactory loggers, CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<OpticalRectificationRequest>(request, cancellationToken);
                if (CheckEnvelope(body?.RequestId, body?.Version, body is not null) is { } invalid)
                {
                    return TypedResults.BadRequest(invalid);
                }

                if (body!.ExpectedRevisionNo is not { } expected)
                {
                    return TypedResults.BadRequest(new ErrorResponse("expected_revision_required"));
                }

                var userId = UserId(principal);
                var result = await kind.Service(services).StartRectificationAsync(
                    id, expected, body.ReasonCode, body.Comment, body.Version!.Value, body.RequestId!.Value, userId, cancellationToken);
                if (result.Status != OpticalWriteStatus.Success)
                {
                    var refused = Refuse(result.Status, result.Errors, Logger(loggers), kind.EntityType, id, userId);
                    return refused.Unprocessable is { } unprocessable ? unprocessable : refused.Missing is { } missing ? missing : refused.Conflict!;
                }

                var logger = Logger(loggers);
                LogOpticalWrite(logger, kind.EntityType, result.Replayed ? "replayed" : "rectification_started", id, userId);
                var detail = await DetailAsync(kind, await kind.Get(queries, id, cancellationToken) ?? result.Record!, principal, queries, cancellationToken);
                return result.Replayed ? TypedResults.Ok(detail) : TypedResults.Created($"{location}/{id}", detail);
            })
            .WithName($"Start{name}Rectification").RequireAuthorization(kind.WritePolicy).Accepts<OpticalRectificationRequest>("application/json");

        group.MapPost("/{id:guid}/draft/discard", async Task<Results<Ok<OpticalRecordResponse<TProvenance>>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> (
                Guid id, HttpRequest request, ClaimsPrincipal principal, IServiceProvider services, OpticalQueryService queries, ILoggerFactory loggers, CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<OpticalVersionRequest>(request, cancellationToken);
                if (CheckEnvelope(body?.RequestId, body?.Version, body is not null) is { } invalid)
                {
                    return TypedResults.BadRequest(invalid);
                }

                var userId = UserId(principal);
                var result = await kind.Service(services).DiscardRectificationAsync(id, body!.Version!.Value, body.RequestId!.Value, userId, cancellationToken);
                return await Completed(kind, result, id, "rectification_discarded", principal, queries, Logger(loggers), userId, cancellationToken);
            })
            .WithName($"Discard{name}Rectification").RequireAuthorization(kind.WritePolicy).Accepts<OpticalVersionRequest>("application/json");

        group.MapPost("/{id:guid}/invalidate", async Task<Results<Ok<OpticalRecordResponse<TProvenance>>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> (
                Guid id, HttpRequest request, ClaimsPrincipal principal, IServiceProvider services, OpticalQueryService queries, ILoggerFactory loggers, CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<OpticalInvalidationRequest>(request, cancellationToken);
                if (CheckEnvelope(body?.RequestId, body?.Version, body is not null) is { } invalid)
                {
                    return TypedResults.BadRequest(invalid);
                }

                var userId = UserId(principal);
                var result = await kind.Service(services).InvalidateAsync(
                    id, body!.ReasonCode, body.Comment, body.Version!.Value, body.RequestId!.Value, userId, cancellationToken);
                return await Completed(kind, result, id, "invalidated", principal, queries, Logger(loggers), userId, cancellationToken);
            })
            .WithName($"Invalidate{name}").RequireAuthorization(AuthSetup.OpticalInvalidatePolicy).Accepts<OpticalInvalidationRequest>("application/json");
    }

    private static async Task<Results<Ok<OpticalRecordResponse<TProvenance>>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> SaveDraftAsync<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance>(
        KindBinding<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance> kind,
        Guid id,
        bool confirm,
        HttpRequest request,
        ClaimsPrincipal principal,
        IServiceProvider services,
        OpticalQueryService queries,
        ILogger logger,
        CancellationToken cancellationToken)
        where TRecord : OpticalRecord<TRevision, TContent>
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
        where TContentRequest : class
    {
        var body = await ReadBodyAsync<OpticalDraftRequest<TContentRequest>>(request, cancellationToken);
        if (CheckEnvelope(body?.RequestId, body?.Version, body is not null) is { } invalid)
        {
            return TypedResults.BadRequest(invalid);
        }

        // Modification : contenu obligatoire. Confirmation : contenu facultatif (absent = brouillon confirmé tel quel).
        if (!confirm && body!.Content is null)
        {
            return TypedResults.BadRequest(InvalidBody);
        }

        var userId = UserId(principal);
        var input = body!.Content is { } content ? kind.ToInput(content) : default;
        var result = await kind.Service(services).SaveDraftAsync(id, input, confirm, body.Version!.Value, body.RequestId!.Value, userId, cancellationToken);
        return await Completed(kind, result, id, confirm ? "confirmed" : "draft_saved", principal, queries, logger, userId, cancellationToken);
    }

    private static async Task<Results<Ok<OpticalRecordResponse<TProvenance>>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> Completed<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance>(
        KindBinding<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance> kind,
        OpticalWriteResult<TRecord> result,
        Guid id,
        string operation,
        ClaimsPrincipal principal,
        OpticalQueryService queries,
        ILogger logger,
        Guid userId,
        CancellationToken cancellationToken)
        where TRecord : OpticalRecord<TRevision, TContent>
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
        where TContentRequest : class
    {
        if (result.Status != OpticalWriteStatus.Success)
        {
            var refused = Refuse(result.Status, result.Errors, logger, kind.EntityType, id, userId);
            return refused.Unprocessable is { } unprocessable ? unprocessable : refused.Missing is { } missing ? missing : refused.Conflict!;
        }

        LogOpticalWrite(logger, kind.EntityType, result.Replayed ? "replayed" : operation, id, userId);
        // Relu après l'enregistrement : version (xmin) et références attribuées par PostgreSQL.
        var record = await kind.Get(queries, id, cancellationToken) ?? result.Record!;
        return TypedResults.Ok(await DetailAsync(kind, record, principal, queries, cancellationToken));
    }

    private static ErrorResponse? CheckEnvelope(Guid? requestId, uint? version, bool hasBody) =>
        !hasBody ? InvalidBody
        : requestId is null || requestId == Guid.Empty ? RequestIdRequired
        : version is null ? new ErrorResponse("version_required")
        : null;

    /// <summary>Refus d'une écriture : exactement un des trois résultats est renseigné.</summary>
    private readonly record struct Refused(UnprocessableEntity<ValidationErrorResponse>? Unprocessable, NotFound<ErrorResponse>? Missing, Conflict<ErrorResponse>? Conflict);

    private static Refused Refuse(OpticalWriteStatus status, IReadOnlyDictionary<string, string[]>? errors, ILogger logger, string entityType, Guid id, Guid userId)
    {
        switch (status)
        {
            case OpticalWriteStatus.ValidationFailed:
                return new(TypedResults.UnprocessableEntity(new ValidationErrorResponse("validation_failed", errors ?? new Dictionary<string, string[]>())), null, null);
            case OpticalWriteStatus.Incomplete:
                return new(TypedResults.UnprocessableEntity(new ValidationErrorResponse("incomplete", errors ?? new Dictionary<string, string[]>())), null, null);
            case OpticalWriteStatus.NotFound:
                return new(null, TypedResults.NotFound(NotFoundError), null);
            case OpticalWriteStatus.ClientNotFound:
                return new(null, TypedResults.NotFound(new ErrorResponse("client_not_found")), null);
        }

        var code = status switch
        {
            OpticalWriteStatus.ClientArchived => "client_archived",
            OpticalWriteStatus.Invalidated => "invalidated",
            OpticalWriteStatus.AlreadyInvalidated => "already_invalidated",
            OpticalWriteStatus.NotDraft => "not_draft",
            OpticalWriteStatus.NotRectification => "not_rectification",
            OpticalWriteStatus.DraftExists => "draft_exists",
            OpticalWriteStatus.RevisionConflict => "revision_conflict",
            OpticalWriteStatus.RequestIdReused => "request_id_reused",
            _ => "version_conflict",
        };
        LogOpticalConflict(logger, entityType, code, id, userId);
        return new(null, null, TypedResults.Conflict(new ErrorResponse(code)));
    }

    // ---------- réponses ----------

    private static async Task<OpticalRecordResponse<TProvenance>> DetailAsync<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance>(
        KindBinding<TRecord, TRevision, TContent, TInput, TContentRequest, TProvenance> kind,
        TRecord record,
        ClaimsPrincipal principal,
        OpticalQueryService service,
        CancellationToken cancellationToken)
        where TRecord : OpticalRecord<TRevision, TContent>
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
        where TContentRequest : class
    {
        // Historique (toutes les révisions, abandonnées comprises, et journal de gestion) : audit.read ET optical.read.
        var history = principal.HasClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.AuditRead);
        var lastConfirmed = record.LastConfirmed;
        var openDraft = record.OpenDraft;
        var revisions = history
            ? record.Chronological
            : [.. record.Chronological.Where(r => r == lastConfirmed || r == openDraft)];
        var events = history ? await service.EventsAsync(kind.EntityType, record.Id, cancellationToken) : [];

        var users = new HashSet<Guid> { record.CreatedByUserId };
        if (record.InvalidatedByUserId is { } invalidatedBy)
        {
            users.Add(invalidatedBy);
        }

        foreach (var revision in revisions)
        {
            users.UnionWith([revision.StartedByUserId, revision.UpdatedByUserId]);
            users.UnionWith(new[] { revision.ConfirmedByUserId, revision.DiscardedByUserId }.OfType<Guid>());
            users.UnionWith(kind.CitedUsers(revision.Content));
        }

        users.UnionWith(events.Select(e => e.ActorUserId));
        var names = await service.DisplayNamesAsync(users, cancellationToken);
        var client = await service.GetClientAsync(record.ClientId, cancellationToken)
            ?? new OpticalClientView(record.ClientId, string.Empty, string.Empty, IsArchived: false);

        OpticalActorResponse Actor(Guid id) => new(id, names.GetValueOrDefault(id, "Compte inconnu"));
        OpticalRevisionResponse<TProvenance> Revision(TRevision revision) => new(
            revision.Id,
            revision.Status switch
            {
                OpticalRevisionStatus.Confirmed => RevisionStatusValue.Confirmed,
                OpticalRevisionStatus.Discarded => RevisionStatusValue.Discarded,
                _ => RevisionStatusValue.Draft,
            },
            revision.RevisionNo,
            revision.BaseRevisionNo,
            revision.ReasonCode,
            revision.ReasonComment,
            new OpticalContentResponse<TProvenance>(kind.Provenance(revision.Content), EyeValuesResponse.From(revision.Values.Od), EyeValuesResponse.From(revision.Values.Og), revision.Notes),
            revision.StartedAtUtc,
            Actor(revision.StartedByUserId),
            revision.UpdatedAtUtc,
            Actor(revision.UpdatedByUserId),
            revision.ConfirmedAtUtc,
            revision.ConfirmedByUserId is { } confirmedBy ? Actor(confirmedBy) : null,
            revision.DiscardedAtUtc,
            revision.DiscardedByUserId is { } discardedBy ? Actor(discardedBy) : null);

        var invalidation = record.InvalidatedAtUtc is { } at
            ? new OpticalInvalidationResponse(
                at,
                Actor(record.InvalidatedByUserId!.Value),
                record.InvalidationReasonCode!,
                record.InvalidationComment,
                record.Revisions.Any(r => r.DiscardedAtUtc == at && r.DiscardedByUserId == record.InvalidatedByUserId))
            : null;

        return new OpticalRecordResponse<TProvenance>(
            kind.Kind == OpticalKind.Prescription ? OpticalKindValue.Prescription : OpticalKindValue.Measurement,
            record.Id,
            record.Reference,
            ToClient(client),
            record.IsInvalidated ? OpticalLifecycleValue.Invalidated : OpticalLifecycleValue.Active,
            invalidation,
            lastConfirmed is null ? null : Revision(lastConfirmed),
            openDraft is null ? null : Revision(openDraft),
            [.. revisions.Select(Revision)],
            history,
            [.. events.Select(e => ToEvent(e, Actor(e.ActorUserId)))],
            record.CreatedAtUtc,
            Actor(record.CreatedByUserId),
            record.Version);
    }

    private static OpticalEventResponse ToEvent(AuditEvent entry, OpticalActorResponse actor)
    {
        var changes = AuditChanges.Deserialize(entry.Changes);
        var revisionNo = changes.FirstOrDefault(c => c.Field is "revisionNo")?.New;
        var reason = changes.FirstOrDefault(c => c.Field is "rectificationReasonCode" or "invalidationReasonCode")?.New;
        return new OpticalEventResponse(
            entry.Id, entry.OccurredAtUtc, entry.Action, actor,
            int.TryParse(revisionNo, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null,
            reason);
    }

    internal static OpticalSummaryResponse ToSummary(OpticalSummaryView view) => new(
        view.Kind == OpticalKind.Prescription ? OpticalKindValue.Prescription : OpticalKindValue.Measurement,
        view.Id,
        view.Reference,
        ToClient(view.Client),
        view.Status switch
        {
            OpticalDisplayStatus.Confirmed => DisplayStatusValue.Confirmed,
            OpticalDisplayStatus.Rectification => DisplayStatusValue.Rectification,
            OpticalDisplayStatus.Invalidated => DisplayStatusValue.Invalidated,
            _ => DisplayStatusValue.Draft,
        },
        view.RevisionNo,
        view.Date,
        view.Source,
        EyeValuesResponse.From(view.Values.Od),
        EyeValuesResponse.From(view.Values.Og),
        view.RecordedAtUtc);

    private static OpticalClientResponse ToClient(OpticalClientView client) =>
        new(client.Id, client.Reference, client.DisplayName, client.IsArchived ? OpticalClientStatusValue.Archived : OpticalClientStatusValue.Active);

    // ---------- outils ----------

    private static async ValueTask<object?> NoStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpRequest request, CancellationToken cancellationToken)
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

    private static readonly ErrorResponse InvalidBody = new("invalid_body");
    private static readonly ErrorResponse RequestIdRequired = new("request_id_required");
    private static readonly ErrorResponse NotFoundError = new("not_found");

    private static ILogger Logger(ILoggerFactory loggers) => loggers.CreateLogger("MMV.Api.Optical");

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Utilisateur authentifié sans identifiant."));

    // Identifiants techniques uniquement : jamais une valeur optique, un nom, une note ni un commentaire.
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Dossier optique {EntityType} {Operation} : {RecordId} par {UserId}")]
    private static partial void LogOpticalWrite(ILogger logger, string entityType, string operation, Guid recordId, Guid userId);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Dossier optique {EntityType} refusé ({Code}) : {RecordId} par {UserId}")]
    private static partial void LogOpticalConflict(ILogger logger, string entityType, string code, Guid recordId, Guid userId);
}
