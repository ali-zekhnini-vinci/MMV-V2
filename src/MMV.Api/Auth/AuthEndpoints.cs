using System.Security.Claims;
using MMV.Api.Appointments;
using MMV.Api.Clients;
using MMV.Api.Cors;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Auth;

/// <summary>
/// Routes d'identité (phase 02). Toute route <c>/api</c> exige HTTPS. Aucune inscription publique,
/// aucun endpoint de bootstrap : le premier administrateur est créé par <c>MMV.Maintenance</c>.
/// </summary>
internal static class AuthEndpoints
{
    public sealed record LoginRequest(string? UserName, string? Password);

    public sealed record SessionUserDto(Guid Id, string UserName, string DisplayName, IReadOnlyList<string> Permissions);

    public sealed record SessionDto(DateTimeOffset ExpiresAtUtc, int IdleTimeoutSeconds, SessionUserDto User);

    public sealed record LoginResponse(
        string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc, int IdleTimeoutSeconds, SessionUserDto User);

    public sealed record AccountDto(
        Guid Id, string UserName, string DisplayName, bool IsActive, IReadOnlyList<string> Permissions, DateTimeOffset CreatedAtUtc);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api")
            .RequireCors(DesktopCors.ApiPolicyName)
            .AddEndpointFilter(RequireHttps);

        var auth = api.MapGroup("/auth");
        auth.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(AuthSetup.LoginRateLimitPolicy);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapGet("/session", GetSession).RequireAuthorization();

        api.MapGroup("/admin")
            .RequireAuthorization(AuthSetup.SystemAdminPolicy)
            .MapGet("/accounts", ListAccountsAsync);

        // Phase 03-A2 : même groupe /api (HTTPS obligatoire, CORS desktop-api).
        api.MapClientsEndpoints();

        // Phase 03-B2 : même groupe /api.
        api.MapAppointmentsEndpoints();

        return app;
    }

    private static async ValueTask<object?> RequireHttps(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.IsHttps
            ? await next(context)
            : Results.Json(new { code = "https_required" }, statusCode: StatusCodes.Status403Forbidden);

    private static async Task<IResult> LoginAsync(LoginRequest? request, AuthService auth, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request?.UserName))
        {
            errors["userName"] = "Veuillez saisir votre identifiant.";
        }

        if (string.IsNullOrEmpty(request?.Password))
        {
            errors["password"] = "Veuillez saisir votre mot de passe.";
        }

        // Bornes de taille : un corps démesuré n'atteint jamais le hachage.
        if (request?.UserName?.Length > UserAccount.UserNameMaxLength || request?.Password?.Length > BootstrapService.PasswordMaxLength)
        {
            return InvalidCredentials();
        }

        if (errors.Count > 0)
        {
            return Results.Json(new { code = "validation_failed", errors }, statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await auth.LoginAsync(request!.UserName!, request.Password!, cancellationToken);
        if (result is null)
        {
            return InvalidCredentials();
        }

        var session = result.Session;
        return Results.Ok(new LoginResponse(
            result.AccessToken, "Bearer", session.ExpiresAtUtc, IdleSeconds(auth), ToDto(session.User)));
    }

    private static async Task<IResult> LogoutAsync(ClaimsPrincipal principal, AuthService auth, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(principal.FindFirstValue(SessionAuthenticationHandler.SessionIdClaim), out var sessionId))
        {
            await auth.LogoutAsync(sessionId, cancellationToken);
        }

        return Results.NoContent();
    }

    private static IResult GetSession(HttpContext context, AuthService auth)
    {
        var session = (AuthenticatedSession)context.Items[typeof(AuthenticatedSession)]!;
        return Results.Ok(new SessionDto(session.ExpiresAtUtc, IdleSeconds(auth), ToDto(session.User)));
    }

    private static async Task<IResult> ListAccountsAsync(IIdentityStore store, CancellationToken cancellationToken)
    {
        var users = await store.ListUsersAsync(cancellationToken);
        return Results.Ok(users.Select(u => new AccountDto(
            u.Id,
            u.UserName,
            u.DisplayName,
            u.IsActive,
            [.. u.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal)],
            u.CreatedAtUtc)));
    }

    private static IResult InvalidCredentials() =>
        Results.Json(new { code = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized);

    private static int IdleSeconds(AuthService auth) => (int)auth.Options.IdleTimeout.TotalSeconds;

    private static SessionUserDto ToDto(AuthenticatedUser user) => new(user.Id, user.UserName, user.DisplayName, user.Permissions);
}
