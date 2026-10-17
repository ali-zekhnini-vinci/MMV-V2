using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;
using MMV.Api.Auth;
using MMV.Api.Clients;
using MMV.Application.Appointments;
using MMV.Domain.Appointments;

namespace MMV.Api.Appointments;

/// <summary>
/// Routes Rendez-vous (phase 03-B2, contrat docs/phase/03/API_APPOINTMENTS.md, décisions D-03-13 à D-03-18).
/// Autorisation exclusivement serveur par <c>appointments.read</c>, <c>appointments.write</c>,
/// <c>appointments.cancel</c> ; <c>system.admin</c> n'en donne aucune. Tant que les paramètres du magasin ne sont
/// pas configurés : 503 <c>appointments_not_configured</c> (après authentification et autorisation). Aucune
/// suppression (pas de DELETE). Agenda minimisé : ni téléphone, ni email, ni notes dans la liste. Journaux limités
/// aux identifiants techniques : jamais de nom de client, de note ni de motif d'annulation (D-03-17).
/// </summary>
internal static partial class AppointmentsEndpoints
{
    public const string NotConfiguredCode = "appointments_not_configured";

    // Corps stricts : un champ inconnu est refusé, jamais ignoré en silence ; nombres jamais lus depuis une chaîne.
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    private static readonly HashSet<string> ListParameters = new(StringComparer.Ordinal) { "from", "to", "status" };

    public static RouteGroupBuilder MapAppointmentsEndpoints(this RouteGroupBuilder api)
    {
        var appointments = api.MapGroup("/appointments")
            .WithTags("Appointments")
            .WithMetadata(
                new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized, typeof(ErrorResponse), ["application/json"]),
                new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ErrorResponse), ["application/json"]),
                new ProducesResponseTypeMetadata(StatusCodes.Status503ServiceUnavailable, typeof(ErrorResponse), ["application/json"]))
            .AddEndpointFilter(RequireConfiguredStore);

        appointments.MapGet("/options", GetOptionsAsync).WithName("GetAppointmentOptions").RequireAuthorization(AuthSetup.AppointmentsReadPolicy);
        appointments.MapGet("/", ListAsync).WithName("ListAppointments").RequireAuthorization(AuthSetup.AppointmentsReadPolicy)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                DescribeListParameters(operation);
                return Task.CompletedTask;
            });
        appointments.MapGet("/{id:guid}", GetAsync).WithName("GetAppointment").RequireAuthorization(AuthSetup.AppointmentsReadPolicy);
        appointments.MapPost("/", CreateAsync).WithName("CreateAppointment").RequireAuthorization(AuthSetup.AppointmentsWritePolicy)
            .Accepts<AppointmentInputRequest>("application/json");
        appointments.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateAppointment").RequireAuthorization(AuthSetup.AppointmentsWritePolicy)
            .Accepts<AppointmentUpdateRequest>("application/json");
        appointments.MapPost("/{id:guid}/cancel", CancelAsync).WithName("CancelAppointment").RequireAuthorization(AuthSetup.AppointmentsCancelPolicy)
            .Accepts<AppointmentCancelRequest>("application/json");
        appointments.MapPost("/{id:guid}/restore", RestoreAsync).WithName("RestoreAppointment").RequireAuthorization(AuthSetup.AppointmentsCancelPolicy)
            .Accepts<AppointmentVersionRequest>("application/json");

        return appointments;
    }

    private static async ValueTask<object?> RequireConfiguredStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.RequestServices.GetRequiredService<AppointmentSettings>().IsConfigured
            ? await next(context)
            : TypedResults.Json(new ErrorResponse(NotConfiguredCode), statusCode: StatusCodes.Status503ServiceUnavailable);

    private static async Task<Ok<AppointmentOptionsResponse>> GetOptionsAsync(AppointmentService service, CancellationToken cancellationToken)
    {
        var settings = service.Settings;
        var responsibles = await service.ListResponsiblesAsync(cancellationToken);
        return TypedResults.Ok(new AppointmentOptionsResponse(
            settings.TimeZoneId,
            AppointmentRules.DurationsMinutes,
            AppointmentRules.StartStepMinutes,
            [.. AppointmentReasons.All.Select(r => new AppointmentReasonResponse(r.Code, r.Label, r.ResponsibleRequired))],
            [.. responsibles.Select(r => new AppointmentResponsibleResponse(r.Id, r.DisplayName))],
            settings.ReceptionCapacity,
            settings.DisplayStartTime,
            settings.DisplayEndTime,
            settings.HiddenWeekdays));
    }

    private static async Task<Results<Ok<AppointmentListResponse>, BadRequest<ErrorResponse>>> ListAsync(
        HttpRequest request,
        AppointmentService service,
        CancellationToken cancellationToken,
        string? from = null,
        string? to = null,
        string? status = null)
    {
        var query = HasOnlyListParameters(request.Query) ? ParseListQuery(from, to, status) : null;
        if (query is null)
        {
            return TypedResults.BadRequest(new ErrorResponse("invalid_query"));
        }

        var items = await service.ListAsync(query.Value.From, query.Value.To, query.Value.IncludeCancelled, cancellationToken);
        return TypedResults.Ok(new AppointmentListResponse([.. items.Select(ToSummary)]));
    }

    private static async Task<Results<Ok<AppointmentDetailResponse>, NotFound<ErrorResponse>>> GetAsync(
        Guid id, AppointmentService service, CancellationToken cancellationToken)
    {
        var view = await service.GetAsync(id, cancellationToken);
        return view is null ? TypedResults.NotFound(NotFoundError) : TypedResults.Ok(ToDetail(view));
    }

    private static async Task<Results<Created<AppointmentDetailResponse>, BadRequest<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> CreateAsync(
        HttpRequest request, ClaimsPrincipal principal, AppointmentService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AppointmentInputRequest>(request, cancellationToken);
        if (body is null)
        {
            return TypedResults.BadRequest(InvalidBody);
        }

        var userId = UserId(principal);
        var logger = Logger(loggers);
        var result = await service.CreateAsync(body.ToInput(), userId, cancellationToken);
        switch (result.Status)
        {
            case AppointmentWriteStatus.Success:
                var view = result.Appointment!;
                LogAppointmentWrite(logger, "created", view.Appointment.Id, userId);
                return TypedResults.Created($"/api/appointments/{view.Appointment.Id}", ToDetail(view));
            case AppointmentWriteStatus.ValidationFailed:
                return TypedResults.UnprocessableEntity(new ValidationErrorResponse("validation_failed", result.Errors!));
            default:
                return ConflictResult(result.Status, logger, Guid.Empty, userId);
        }
    }

    private static async Task<Results<Ok<AppointmentDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> UpdateAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, AppointmentService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AppointmentUpdateRequest>(request, cancellationToken);
        if (body is null)
        {
            return TypedResults.BadRequest(InvalidBody);
        }

        if (body.Version is not { } version)
        {
            return TypedResults.BadRequest(new ErrorResponse("version_required"));
        }

        var userId = UserId(principal);
        var result = await service.UpdateAsync(id, body.ToInput(), version, userId, cancellationToken);
        return WriteResult(result, Logger(loggers), "updated", id, userId);
    }

    private static async Task<Results<Ok<AppointmentDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> CancelAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, AppointmentService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AppointmentCancelRequest>(request, cancellationToken);
        if (body is null)
        {
            return TypedResults.BadRequest(InvalidBody);
        }

        if (body.Version is not { } version)
        {
            return TypedResults.BadRequest(new ErrorResponse("version_required"));
        }

        var userId = UserId(principal);
        var result = await service.CancelAsync(id, version, body.Reason, userId, cancellationToken);
        return WriteResult(result, Logger(loggers), "cancelled", id, userId);
    }

    private static async Task<Results<Ok<AppointmentDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> RestoreAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, AppointmentService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AppointmentVersionRequest>(request, cancellationToken);
        if (body is null)
        {
            return TypedResults.BadRequest(InvalidBody);
        }

        if (body.Version is not { } version)
        {
            return TypedResults.BadRequest(new ErrorResponse("version_required"));
        }

        var userId = UserId(principal);
        var result = await service.RestoreAsync(id, version, userId, cancellationToken);
        return WriteResult(result, Logger(loggers), "restored", id, userId);
    }

    private static Results<Ok<AppointmentDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>> WriteResult(
        AppointmentWriteResult result, ILogger logger, string operation, Guid id, Guid userId)
    {
        switch (result.Status)
        {
            case AppointmentWriteStatus.Success:
                LogAppointmentWrite(logger, operation, id, userId);
                return TypedResults.Ok(ToDetail(result.Appointment!));
            case AppointmentWriteStatus.NotFound:
                return TypedResults.NotFound(NotFoundError);
            case AppointmentWriteStatus.ValidationFailed:
                return TypedResults.UnprocessableEntity(new ValidationErrorResponse("validation_failed", result.Errors!));
            default:
                return ConflictResult(result.Status, logger, id, userId);
        }
    }

    private static Conflict<ErrorResponse> ConflictResult(AppointmentWriteStatus status, ILogger logger, Guid id, Guid userId)
    {
        var code = status switch
        {
            AppointmentWriteStatus.AppointmentCancelled => "appointment_cancelled",
            AppointmentWriteStatus.AppointmentNotCancelled => "appointment_not_cancelled",
            AppointmentWriteStatus.AppointmentPast => "appointment_past",
            AppointmentWriteStatus.ClientArchived => "client_archived",
            AppointmentWriteStatus.SlotConflict => "appointment_conflict",
            AppointmentWriteStatus.ReceptionCapacityFull => "reception_capacity_full",
            AppointmentWriteStatus.ReceptionNotConfigured => "reception_not_configured",
            AppointmentWriteStatus.ResponsibleUnavailable => "responsible_unavailable",
            _ => "version_conflict",
        };
        LogAppointmentConflict(logger, code, id, userId);
        return TypedResults.Conflict(new ErrorResponse(code));
    }

    /// <summary>Paramètres liés comme chaînes (refus borné 400 <c>invalid_query</c>) ; le document décrit les valeurs admises.</summary>
    private static void DescribeListParameters(OpenApiOperation operation)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            parameter.Schema = parameter.Name switch
            {
                "from" or "to" => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
                "status" => new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [JsonValue.Create("planned"), JsonValue.Create("all")],
                    Default = JsonValue.Create("planned"),
                },
                _ => parameter.Schema,
            };
            if (parameter.Name is "from" or "to")
            {
                parameter.Required = true;
            }
        }
    }

    /// <summary>Aucun paramètre inconnu ni répété (liste blanche stricte).</summary>
    internal static bool HasOnlyListParameters(IQueryCollection query) =>
        query.All(p => ListParameters.Contains(p.Key) && p.Value.Count == 1);

    /// <summary>Période [from, to[ en instants UTC, <c>to &gt; from</c>, au plus 31 jours ; statut <c>planned</c> | <c>all</c>.</summary>
    internal static (DateTimeOffset From, DateTimeOffset To, bool IncludeCancelled)? ParseListQuery(string? from, string? to, string? status)
    {
        bool? includeCancelled = (status ?? "planned") switch
        {
            "planned" => false,
            "all" => true,
            _ => null,
        };
        if (includeCancelled is null
            || !AppointmentSchedule.TryParseUtcInstant(from, out var start)
            || !AppointmentSchedule.TryParseUtcInstant(to, out var end)
            || end <= start
            || end - start > TimeSpan.FromDays(AppointmentService.MaxRangeDays))
        {
            return null;
        }

        return (start, end, includeCancelled.Value);
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
    private static readonly ErrorResponse NotFoundError = new("appointment_not_found");

    private static ILogger Logger(ILoggerFactory loggers) => loggers.CreateLogger("MMV.Api.Appointments");

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Utilisateur authentifié sans identifiant."));

    internal static AppointmentSummaryResponse ToSummary(AppointmentView view)
    {
        var a = view.Appointment;
        return new AppointmentSummaryResponse(
            a.Id, a.ClientId, view.ClientReference, view.ClientFirstName, view.ClientLastName, view.ClientArchived,
            a.ResponsibleUserId, view.ResponsibleDisplayName, a.StartsAtUtc, a.EndsAtUtc, a.ReasonCode, Status(a), a.Version);
    }

    internal static AppointmentDetailResponse ToDetail(AppointmentView view)
    {
        var a = view.Appointment;
        return new AppointmentDetailResponse(
            a.Id, a.ClientId, view.ClientReference, view.ClientFirstName, view.ClientLastName, view.ClientArchived,
            a.ResponsibleUserId, view.ResponsibleDisplayName, a.StartsAtUtc, a.EndsAtUtc, a.ReasonCode, Status(a), a.Version,
            a.Notes, a.CancelledAtUtc, view.CancelledByDisplayName, a.CancellationReason, a.CreatedAtUtc, a.UpdatedAtUtc);
    }

    private static AppointmentStatusValue Status(Appointment appointment) =>
        appointment.IsCancelled ? AppointmentStatusValue.Cancelled : AppointmentStatusValue.Planned;

    // Identifiants techniques uniquement : jamais de nom de client, de note ni de motif d'annulation (D-03-17).
    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "Rendez-vous {Operation} : {AppointmentId} par {UserId}")]
    private static partial void LogAppointmentWrite(ILogger logger, string operation, Guid appointmentId, Guid userId);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Rendez-vous refusé ({Code}) : {AppointmentId} par {UserId}")]
    private static partial void LogAppointmentConflict(ILogger logger, string code, Guid appointmentId, Guid userId);
}

/// <summary>Paramètres servis à l'interface : elle n'en code aucun en dur.</summary>
internal sealed record AppointmentOptionsResponse(
    string TimeZone,
    IReadOnlyList<int> DurationsMinutes,
    int StartStepMinutes,
    IReadOnlyList<AppointmentReasonResponse> Reasons,
    IReadOnlyList<AppointmentResponsibleResponse> Responsibles,
    int? ReceptionCapacity,
    string DisplayStartTime,
    string DisplayEndTime,
    IReadOnlyList<int> HiddenWeekdays);

/// <summary>Motif (D-03-18) ; <c>responsibleRequired</c> : rendez-vous personnalisé (D-03-14).</summary>
internal sealed record AppointmentReasonResponse(string Code, string Label, bool ResponsibleRequired);

/// <summary>Collaborateur actif et éligible : identifiant et nom affiché seulement.</summary>
internal sealed record AppointmentResponsibleResponse(Guid Id, string DisplayName);

/// <summary>Ligne d'agenda : aucune coordonnée client, aucune note (minimisation, D-03-17).</summary>
internal sealed record AppointmentSummaryResponse(
    Guid Id,
    Guid ClientId,
    string ClientReference,
    string ClientFirstName,
    string ClientLastName,
    bool ClientArchived,
    Guid? ResponsibleUserId,
    string? ResponsibleDisplayName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ReasonCode,
    AppointmentStatusValue Status,
    uint Version);

/// <summary>Détail : ligne d'agenda + notes, annulation (auteur, date, motif) et audit.</summary>
internal sealed record AppointmentDetailResponse(
    Guid Id,
    Guid ClientId,
    string ClientReference,
    string ClientFirstName,
    string ClientLastName,
    bool ClientArchived,
    Guid? ResponsibleUserId,
    string? ResponsibleDisplayName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ReasonCode,
    AppointmentStatusValue Status,
    uint Version,
    string? Notes,
    DateTimeOffset? CancelledAtUtc,
    string? CancelledByDisplayName,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record AppointmentListResponse(IReadOnlyList<AppointmentSummaryResponse> Items);

/// <summary>Statut exposé : <c>planned</c> | <c>cancelled</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppointmentStatusValue>))]
internal enum AppointmentStatusValue
{
    [JsonStringEnumMemberName("planned")]
    Planned,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}

/// <summary>
/// Saisie d'un rendez-vous. Les champs sont validés par le serveur (422 par champ) plutôt que par le schéma.
/// <c>startsAtUtc</c> : instant ISO 8601 UTC ; <c>responsibleUserId</c> <c>null</c> = rendez-vous d'accueil.
/// </summary>
internal sealed record AppointmentInputRequest(
    Guid? ClientId = null,
    Guid? ResponsibleUserId = null,
    string? StartsAtUtc = null,
    int? DurationMinutes = null,
    string? ReasonCode = null,
    string? Notes = null)
{
    public AppointmentInput ToInput() => new(ClientId, ResponsibleUserId, StartsAtUtc, DurationMinutes, ReasonCode, Notes);
}

/// <summary>Modification et déplacement : saisie complète + version lue (obligatoire).</summary>
internal sealed record AppointmentUpdateRequest(
    Guid? ClientId = null,
    Guid? ResponsibleUserId = null,
    string? StartsAtUtc = null,
    int? DurationMinutes = null,
    string? ReasonCode = null,
    string? Notes = null,
    uint? Version = null)
{
    public AppointmentInput ToInput() => new(ClientId, ResponsibleUserId, StartsAtUtc, DurationMinutes, ReasonCode, Notes);
}

/// <summary>Annulation : version lue (obligatoire) et motif facultatif (≤ 200 caractères).</summary>
internal sealed record AppointmentCancelRequest(uint? Version = null, string? Reason = null);

/// <summary>Rétablissement : version lue (obligatoire).</summary>
internal sealed record AppointmentVersionRequest(uint? Version = null);
