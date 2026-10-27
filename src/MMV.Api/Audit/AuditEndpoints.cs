using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;
using MMV.Api.Auth;
using MMV.Api.Clients;
using MMV.Application.Appointments;
using MMV.Application.Audit;
using MMV.Domain.Appointments;
using MMV.Domain.Audit;
using MMV.Domain.Identity;

namespace MMV.Api.Audit;

/// <summary>
/// Contexte technique d'audit de la requête HTTP : identifiant de SESSION (revendication <c>mmv:sid</c>, jamais le
/// jeton) et identifiant de requête (corrélation avec les journaux techniques).
/// </summary>
internal sealed class HttpAuditContext(IHttpContextAccessor accessor) : IAuditContext
{
    public Guid? SessionId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(SessionAuthenticationHandler.SessionIdClaim), out var id) ? id : null;

    public string? TraceId => accessor.HttpContext?.TraceIdentifier;
}

/// <summary>
/// Historique des actions sur un dossier client (phase 03-D2) : lecture seule, droit DÉDIÉ <c>audit.read</c> —
/// ni <c>clients.read</c>, ni <c>appointments.read</c>, ni <c>system.admin</c> n'y donnent accès. Aucune route
/// d'écriture : le journal n'est alimenté que par les services métier, dans leurs transactions. Valeurs : seules
/// celles que la politique de conservation garde (créneau, durée, motif, responsable) sont servies, avec leur
/// libellé calculé par le serveur (heure murale du magasin, nom affiché actuel du collaborateur, libellé du motif).
/// 04-B : les événements du module <c>optical</c> ne sont servis qu'à une session détenant AUSSI <c>optical.read</c>
/// (sinon ils sont exclus par le serveur, et le filtre <c>module=optical</c> est refusé par 403) ; ils ne contiennent
/// que des données de gestion (numéros de révision, motifs codifiés), jamais une valeur optique.
/// </summary>
internal static class AuditEndpoints
{
    private static readonly HashSet<string> ListParameters = new(StringComparer.Ordinal) { "module", "from", "to", "page", "pageSize" };

    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder api)
    {
        var audit = api.MapGroup("/audit")
            .WithTags("Audit")
            .WithMetadata(
                new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized, typeof(ErrorResponse), ["application/json"]),
                new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ErrorResponse), ["application/json"]));

        audit.MapGet("/clients/{clientId:guid}", ListForClientAsync).WithName("ListClientAuditEvents").RequireAuthorization(AuthSetup.AuditReadPolicy)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                DescribeParameters(operation);
                return Task.CompletedTask;
            });
        return audit;
    }

    private static async Task<Results<Ok<AuditEventPageResponse>, BadRequest<ErrorResponse>, ForbidHttpResult>> ListForClientAsync(
        Guid clientId,
        HttpRequest request,
        ClaimsPrincipal principal,
        AuditService service,
        AppointmentSettings settings,
        CancellationToken cancellationToken,
        string? module = null,
        string? from = null,
        string? to = null,
        string? page = null,
        string? pageSize = null)
    {
        var query = request.Query.All(p => ListParameters.Contains(p.Key) && p.Value.Count == 1) ? ParseQuery(clientId, module, from, to, page, pageSize) : null;
        if (query is null)
        {
            return TypedResults.BadRequest(new ErrorResponse("invalid_query"));
        }

        var optical = principal.HasClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.OpticalRead)
            && principal.HasClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.ClientsRead);
        if (query.Module == AuditActions.OpticalModule && !optical)
        {
            return TypedResults.Forbid();
        }

        query = query with { ExcludedModules = optical ? [] : [AuditActions.OpticalModule] };

        var result = await service.ListForClientAsync(query, cancellationToken);
        var parsed = result.Items.Select(item => (item, Changes: AuditChanges.Deserialize(item.Event.Changes))).ToList();
        var responsibleIds = parsed.SelectMany(p => p.Changes)
            .Where(c => c.Field == "responsibleUserId")
            .SelectMany(c => new[] { c.Old, c.New })
            .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var names = await service.DisplayNamesAsync(responsibleIds, cancellationToken);
        return TypedResults.Ok(new AuditEventPageResponse(
            [.. parsed.Select(p => ToResponse(p.item, p.Changes, names, settings))],
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    /// <summary>Module en liste blanche, période [from, to[ en instants UTC, page et taille bornées ; sinon <c>null</c>.</summary>
    internal static AuditQuery? ParseQuery(Guid clientId, string? module, string? from, string? to, string? page, string? pageSize)
    {
        if (module is not (null or AuditActions.ClientsModule or AuditActions.AppointmentsModule or AuditActions.OpticalModule))
        {
            return null;
        }

        DateTimeOffset? start = null;
        DateTimeOffset? end = null;
        if (from is not null)
        {
            if (!AppointmentSchedule.TryParseUtcInstant(from, out var value))
            {
                return null;
            }

            start = value;
        }

        if (to is not null)
        {
            if (!AppointmentSchedule.TryParseUtcInstant(to, out var value))
            {
                return null;
            }

            end = value;
        }

        if (start is not null && end is not null && end <= start)
        {
            return null;
        }

        var pageNumber = ClientsEndpoints.BoundedInteger(page ?? "1", 1, AuditQuery.MaxPage);
        var size = ClientsEndpoints.BoundedInteger(pageSize ?? "20", 1, AuditQuery.MaxPageSize);
        return pageNumber is null || size is null ? null : new AuditQuery(clientId, module, start, end, pageNumber.Value, size.Value);
    }

    private static AuditEventResponse ToResponse(AuditEntryView view, IReadOnlyList<AuditChange> changes, IReadOnlyDictionary<Guid, string> names, AppointmentSettings settings)
    {
        var e = view.Event;
        // 03-D3 : une création n'a pas de valeur précédente — aucun libellé « avant » (pas de « Non attribué → … »).
        var created = e.Action is AuditActions.ClientCreated or AuditActions.AppointmentCreated || e.Action.EndsWith(".created", StringComparison.Ordinal);
        return new AuditEventResponse(
            e.Id, e.OccurredAtUtc, e.ActorUserId, view.ActorDisplayName, e.Module, e.Action, e.EntityType, e.EntityId,
            [.. changes.Select(c => new AuditChangeResponse(
                c.Field, c.Masked, c.Masked ? null : c.Old, c.Masked ? null : c.New,
                c.Masked || created ? null : Display(c.Field, c.Old, names, settings), c.Masked ? null : Display(c.Field, c.New, names, settings)))]);
    }

    /// <summary>Libellé d'une valeur conservée ; <c>null</c> pour une valeur absente (création, aucun responsable → « Non attribué »).</summary>
    internal static string? Display(string field, string? value, IReadOnlyDictionary<Guid, string> names, AppointmentSettings settings)
    {
        switch (field)
        {
            case "responsibleUserId":
                return value is null ? "Non attribué" : Guid.TryParse(value, out var id) && names.TryGetValue(id, out var name) ? name : "Compte inconnu";
            case "reasonCode":
                return value is null ? null : AppointmentReasons.Find(value)?.Label ?? value;
            case "durationMinutes":
                return value is null ? null : $"{value} min";
            case "startsAtUtc":
                if (value is null || !AppointmentSchedule.TryParseUtcInstant(value, out var instant))
                {
                    return null;
                }

                // Heure murale du magasin si les paramètres Rendez-vous sont configurés, sinon UTC explicite.
                return settings.TimeZone is { } zone
                    ? TimeZoneInfo.ConvertTime(instant, zone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                    : instant.UtcDateTime.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
            default:
                return value;
        }
    }

    private static void DescribeParameters(OpenApiOperation operation)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            parameter.Schema = parameter.Name switch
            {
                "module" => new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [JsonValue.Create(AuditActions.ClientsModule), JsonValue.Create(AuditActions.AppointmentsModule), JsonValue.Create(AuditActions.OpticalModule)],
                },
                "from" or "to" => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
                "page" => new OpenApiSchema
                {
                    Type = JsonSchemaType.Integer, Format = "int32", Minimum = "1",
                    Maximum = AuditQuery.MaxPage.ToString(CultureInfo.InvariantCulture), Default = JsonValue.Create(1),
                },
                "pageSize" => new OpenApiSchema
                {
                    Type = JsonSchemaType.Integer, Format = "int32", Minimum = "1",
                    Maximum = AuditQuery.MaxPageSize.ToString(CultureInfo.InvariantCulture), Default = JsonValue.Create(AuditQuery.DefaultPageSize),
                },
                _ => parameter.Schema,
            };
        }
    }
}

/// <summary>Page de l'historique d'un dossier, du plus récent au plus ancien.</summary>
internal sealed record AuditEventPageResponse(IReadOnlyList<AuditEventResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// Événement : instant UTC, auteur (compte qui a exécuté l'action, nom affiché actuel), module, action, entité.
/// Identifiants de session et de requête conservés en base pour les enquêtes, non servis.
/// </summary>
internal sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    Guid ActorUserId,
    string ActorDisplayName,
    string Module,
    string Action,
    string EntityType,
    Guid EntityId,
    IReadOnlyList<AuditChangeResponse> Changes);

/// <summary>Champ modifié ; <c>masked</c> : valeur non conservée (donnée personnelle ou texte libre).</summary>
internal sealed record AuditChangeResponse(string Field, bool Masked, string? Old, string? New, string? OldDisplay, string? NewDisplay);
