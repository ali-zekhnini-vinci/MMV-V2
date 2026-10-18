using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;
using MMV.Api.Auth;
using MMV.Application.Clients;
using MMV.Domain.Clients;

namespace MMV.Api.Clients;

/// <summary>
/// Routes Clients (phase 03-A2, contrat docs/phase/03/API_CLIENTS.md). Autorisation exclusivement serveur par
/// <c>clients.read</c>, <c>clients.write</c>, <c>clients.archive</c> ; <c>system.admin</c> n'en donne aucune.
/// Aucune suppression (pas de DELETE). Erreurs à codes bornés, sans donnée personnelle ; journaux limités aux
/// identifiants techniques (D-03-10).
/// </summary>
internal static partial class ClientsEndpoints
{
    // Corps stricts : un champ inconnu (ex. sécurité sociale) est refusé, jamais ignoré en silence.
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    private static readonly HashSet<string> ListParameters = new(StringComparer.Ordinal)
    {
        "q", "status", "sort", "dir", "page", "pageSize",
    };

    /// <summary>Borne de page : (page - 1) × pageSize reste représentable et raisonnable.</summary>
    public const int MaxPage = 100_000;

    public static RouteGroupBuilder MapClientsEndpoints(this RouteGroupBuilder api)
    {
        var clients = api.MapGroup("/clients")
            .WithTags("Clients")
            .WithMetadata(
                new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized, typeof(ErrorResponse), ["application/json"]),
                new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ErrorResponse), ["application/json"]));

        clients.MapGet("/", ListAsync).WithName("ListClients").RequireAuthorization(AuthSetup.ClientsReadPolicy)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                DescribeListParameters(operation);
                return Task.CompletedTask;
            });
        clients.MapGet("/{id:guid}", GetAsync).WithName("GetClient").RequireAuthorization(AuthSetup.ClientsReadPolicy);
        clients.MapPost("/", CreateAsync).WithName("CreateClient").RequireAuthorization(AuthSetup.ClientsWritePolicy)
            .Accepts<ClientInputRequest>("application/json");
        clients.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateClient").RequireAuthorization(AuthSetup.ClientsWritePolicy)
            .Accepts<ClientUpdateRequest>("application/json");
        clients.MapPost("/{id:guid}/archive", ArchiveAsync).WithName("ArchiveClient").RequireAuthorization(AuthSetup.ClientsArchivePolicy)
            .Accepts<ClientVersionRequest>("application/json");
        clients.MapPost("/{id:guid}/reactivate", ReactivateAsync).WithName("ReactivateClient").RequireAuthorization(AuthSetup.ClientsArchivePolicy)
            .Accepts<ClientVersionRequest>("application/json");

        return clients;
    }

    private static async Task<Results<Ok<ClientPageResponse>, BadRequest<ErrorResponse>>> ListAsync(
        HttpRequest request,
        ClientService service,
        CancellationToken cancellationToken,
        string? q = null,
        string? status = null,
        string? sort = null,
        string? dir = null,
        string? page = null,
        string? pageSize = null)
    {
        var query = HasOnlyListParameters(request.Query) ? ParseListQuery(q, status, sort, dir, page, pageSize) : null;
        if (query is null)
        {
            return TypedResults.BadRequest(new ErrorResponse("invalid_query"));
        }

        var result = await service.ListAsync(query, cancellationToken);
        return TypedResults.Ok(new ClientPageResponse(
            [.. result.Items.Select(ToSummary)], result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<Results<Ok<ClientDetailResponse>, NotFound<ErrorResponse>>> GetAsync(
        Guid id, ClientService service, CancellationToken cancellationToken)
    {
        var client = await service.GetAsync(id, cancellationToken);
        return client is null ? TypedResults.NotFound(NotFoundError) : TypedResults.Ok(ToDetail(client));
    }

    private static async Task<Results<Created<ClientDetailResponse>, BadRequest<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> CreateAsync(
        HttpRequest request, ClaimsPrincipal principal, ClientService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ClientInputRequest>(request, cancellationToken);
        if (body is null)
        {
            return TypedResults.BadRequest(InvalidBody);
        }

        var userId = UserId(principal);
        var result = await service.CreateAsync(body.ToInput(), userId, cancellationToken);
        if (result.Status == ClientWriteStatus.ValidationFailed)
        {
            return TypedResults.UnprocessableEntity(new ValidationErrorResponse("validation_failed", result.Errors!));
        }

        var client = result.Client!;
        var logger = Logger(loggers);
        LogClientWrite(logger, "created", client.Id, userId);
        return TypedResults.Created($"/api/clients/{client.Id}", ToDetail(client));
    }

    private static async Task<Results<Ok<ClientDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnprocessableEntity<ValidationErrorResponse>>> UpdateAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, ClientService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ClientUpdateRequest>(request, cancellationToken);
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
        var logger = Logger(loggers);
        return result.Status switch
        {
            ClientWriteStatus.Success => Logged(TypedResults.Ok(ToDetail(result.Client!)), logger, "updated", id, userId),
            ClientWriteStatus.NotFound => TypedResults.NotFound(NotFoundError),
            ClientWriteStatus.ValidationFailed => TypedResults.UnprocessableEntity(new ValidationErrorResponse("validation_failed", result.Errors!)),
            _ => ConflictResult(result.Status, logger, id, userId),
        };
    }

    private static Task<Results<Ok<ClientDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>>> ArchiveAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, ClientService service, ILoggerFactory loggers, CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(id, request, principal, archive: true, service, Logger(loggers), cancellationToken);

    private static Task<Results<Ok<ClientDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>>> ReactivateAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, ClientService service, ILoggerFactory loggers, CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(id, request, principal, archive: false, service, Logger(loggers), cancellationToken);

    private static async Task<Results<Ok<ClientDetailResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>>> ChangeLifecycleAsync(
        Guid id, HttpRequest request, ClaimsPrincipal principal, bool archive, ClientService service, ILogger logger, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<ClientVersionRequest>(request, cancellationToken);
        if (body is null)
        {
            return TypedResults.BadRequest(InvalidBody);
        }

        if (body.Version is not { } version)
        {
            return TypedResults.BadRequest(new ErrorResponse("version_required"));
        }

        var userId = UserId(principal);
        var result = archive
            ? await service.ArchiveAsync(id, version, userId, cancellationToken)
            : await service.ReactivateAsync(id, version, userId, cancellationToken);
        return result.Status switch
        {
            ClientWriteStatus.Success => Logged(TypedResults.Ok(ToDetail(result.Client!)), logger, archive ? "archived" : "reactivated", id, userId),
            ClientWriteStatus.NotFound => TypedResults.NotFound(NotFoundError),
            _ => ConflictResult(result.Status, logger, id, userId),
        };
    }

    /// <summary>
    /// Paramètres liés comme chaînes (le refus hors liste blanche reste un 400 <c>invalid_query</c> borné) ;
    /// le document OpenAPI décrit leurs valeurs réellement admises.
    /// </summary>
    private static void DescribeListParameters(OpenApiOperation operation)
    {
        static OpenApiSchema Enumeration(string fallback, params string[] values) => new()
        {
            Type = JsonSchemaType.String,
            Enum = [.. values.Select(v => (JsonNode)JsonValue.Create(v))],
            Default = JsonValue.Create(fallback),
        };

        static OpenApiSchema Integer(int minimum, int maximum, int fallback) => new()
        {
            Type = JsonSchemaType.Integer,
            Format = "int32",
            Minimum = minimum.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Maximum = maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Default = JsonValue.Create(fallback),
        };

        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            parameter.Schema = parameter.Name switch
            {
                "q" => new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    MinLength = ClientListQuery.SearchMinLength,
                    MaxLength = ClientListQuery.SearchMaxLength,
                },
                "status" => Enumeration("active", "active", "archived", "all"),
                "sort" => Enumeration("name", "name", "createdAt", "city"),
                "dir" => Enumeration("asc", "asc", "desc"),
                "page" => Integer(1, MaxPage, 1),
                "pageSize" => Integer(1, ClientListQuery.MaxPageSize, ClientListQuery.DefaultPageSize),
                _ => parameter.Schema,
            };
        }
    }

    /// <summary>Aucun paramètre inconnu ni répété (liste blanche stricte).</summary>
    internal static bool HasOnlyListParameters(IQueryCollection query) =>
        query.All(p => ListParameters.Contains(p.Key) && p.Value.Count == 1);

    /// <summary>Valeurs de liste blanche et bornes ; toute autre valeur → <c>null</c> (400 invalid_query).</summary>
    internal static ClientListQuery? ParseListQuery(
        string? q, string? status, string? sort, string? dir, string? page, string? pageSize)
    {
        var search = q?.Trim();
        if (search is not null && search.Length is < ClientListQuery.SearchMinLength or > ClientListQuery.SearchMaxLength)
        {
            return null;
        }

        ClientStatusFilter? statusFilter = (status ?? "active") switch
        {
            "active" => ClientStatusFilter.Active,
            "archived" => ClientStatusFilter.Archived,
            "all" => ClientStatusFilter.All,
            _ => null,
        };
        ClientSortField? sortField = (sort ?? "name") switch
        {
            "name" => ClientSortField.Name,
            "createdAt" => ClientSortField.CreatedAt,
            "city" => ClientSortField.City,
            _ => null,
        };
        bool? descending = (dir ?? "asc") switch
        {
            "asc" => false,
            "desc" => true,
            _ => null,
        };
        var pageNumber = BoundedInteger(page ?? "1", 1, MaxPage);
        var size = BoundedInteger(pageSize ?? "20", 1, ClientListQuery.MaxPageSize);

        return statusFilter is null || sortField is null || descending is null || pageNumber is null || size is null
            ? null
            : new ClientListQuery(search, statusFilter.Value, sortField.Value, descending.Value, pageNumber.Value, size.Value);
    }

    // Chiffres ASCII seulement (ni signe, ni espace, ni séparateur), dans les bornes.
    internal static int? BoundedInteger(string value, int min, int max)
    {
        if (value.Length is 0 or > 9 || !value.All(char.IsAsciiDigit))
        {
            return null;
        }

        var number = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        return number >= min && number <= max ? number : null;
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

    private static Conflict<ErrorResponse> ConflictResult(ClientWriteStatus status, ILogger logger, Guid id, Guid userId)
    {
        var code = status switch
        {
            ClientWriteStatus.ClientArchived => "client_archived",
            ClientWriteStatus.AlreadyArchived => "already_archived",
            ClientWriteStatus.AlreadyActive => "already_active",
            _ => "version_conflict",
        };
        LogClientConflict(logger, code, id, userId);
        return TypedResults.Conflict(new ErrorResponse(code));
    }

    private static T Logged<T>(T result, ILogger logger, string operation, Guid clientId, Guid userId)
    {
        LogClientWrite(logger, operation, clientId, userId);
        return result;
    }

    private static readonly ErrorResponse InvalidBody = new("invalid_body");
    private static readonly ErrorResponse NotFoundError = new("client_not_found");

    private static ILogger Logger(ILoggerFactory loggers) => loggers.CreateLogger("MMV.Api.Clients");

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Utilisateur authentifié sans identifiant."));

    internal static ClientSummaryResponse ToSummary(Client client) => new(
        client.Id, client.Reference, client.FirstName, client.LastName, client.Phone, client.Email, client.City,
        Status(client), client.CreatedAtUtc, client.Version);

    internal static ClientDetailResponse ToDetail(Client client) => new(
        client.Id, client.Reference, client.FirstName, client.LastName, client.Phone, client.Email, client.City,
        Status(client), client.CreatedAtUtc, client.Version,
        client.BirthDate, client.AddressLine, client.PostalCode, client.Notes, client.UpdatedAtUtc, client.ArchivedAtUtc);

    private static ClientStatusValue Status(Client client) => client.IsArchived ? ClientStatusValue.Archived : ClientStatusValue.Active;

    // Identifiants techniques uniquement : jamais de nom, téléphone, email, adresse ni note (D-03-10).
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Client {Operation} : {ClientId} par {UserId}")]
    private static partial void LogClientWrite(ILogger logger, string operation, Guid clientId, Guid userId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Client en conflit ({Code}) : {ClientId} par {UserId}")]
    private static partial void LogClientConflict(ILogger logger, string code, Guid clientId, Guid userId);
}

/// <summary>Ligne de liste (colonnes affichées seulement : ni notes, ni adresse, ni date de naissance).</summary>
internal sealed record ClientSummaryResponse(
    Guid Id,
    string Reference,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? City,
    ClientStatusValue Status,
    DateTimeOffset CreatedAtUtc,
    uint Version);

/// <summary>Fiche complète.</summary>
internal sealed record ClientDetailResponse(
    Guid Id,
    string Reference,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? City,
    ClientStatusValue Status,
    DateTimeOffset CreatedAtUtc,
    uint Version,
    DateOnly? BirthDate,
    string? AddressLine,
    string? PostalCode,
    string? Notes,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

internal sealed record ClientPageResponse(IReadOnlyList<ClientSummaryResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>Cycle de vie exposé : <c>active</c> | <c>archived</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClientStatusValue>))]
internal enum ClientStatusValue
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("archived")]
    Archived,
}

/// <summary>
/// Saisie d'un client (création). Facultatifs : <c>null</c> ou absents. Prénom et nom sont validés par le
/// serveur (422 <c>required</c>) plutôt que par le schéma, pour un retour par champ.
/// </summary>
internal sealed record ClientInputRequest(
    string? FirstName = null,
    string? LastName = null,
    string? BirthDate = null,
    string? Phone = null,
    string? Email = null,
    string? AddressLine = null,
    string? City = null,
    string? PostalCode = null,
    string? Notes = null)
{
    public ClientInput ToInput() => new(FirstName, LastName, BirthDate, Phone, Email, AddressLine, City, PostalCode, Notes);
}

/// <summary>Modification : saisie complète + version lue (obligatoire).</summary>
internal sealed record ClientUpdateRequest(
    string? FirstName = null,
    string? LastName = null,
    string? BirthDate = null,
    string? Phone = null,
    string? Email = null,
    string? AddressLine = null,
    string? City = null,
    string? PostalCode = null,
    string? Notes = null,
    uint? Version = null)
{
    public ClientInput ToInput() => new(FirstName, LastName, BirthDate, Phone, Email, AddressLine, City, PostalCode, Notes);
}

/// <summary>Archivage / réactivation : version lue (obligatoire).</summary>
internal sealed record ClientVersionRequest(uint? Version = null);

/// <summary>Erreur à code borné.</summary>
internal sealed record ErrorResponse(string Code);

/// <summary>Erreur de validation : codes par champ (<c>required</c>, <c>too_long</c>, <c>invalid_format</c>, <c>out_of_range</c>).</summary>
internal sealed record ValidationErrorResponse(string Code, IReadOnlyDictionary<string, string[]> Errors);
