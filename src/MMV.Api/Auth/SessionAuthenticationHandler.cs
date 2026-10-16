using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MMV.Application.Identity;

namespace MMV.Api.Auth;

/// <summary>
/// Schéma « MmvSession » : jeton opaque <c>Authorization: Bearer</c> validé à chaque requête contre
/// la session serveur (révocation, échéance, inactivité, compte actif). Défis 401/403 en JSON neutre.
/// </summary>
internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MmvSession";
    public const string PermissionClaim = "mmv:perm";
    public const string SessionIdClaim = "mmv:sid";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();
        var auth = Context.RequestServices.GetRequiredService<AuthService>();
        var session = await auth.AuthenticateAsync(token, Context.RequestAborted);
        if (session is null)
        {
            // Aucun détail (absent, expiré, révoqué, désactivé) : même refus.
            return AuthenticateResult.Fail("Session invalide.");
        }

        Context.Items[typeof(AuthenticatedSession)] = session;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, session.User.Id.ToString()),
            new(ClaimTypes.Name, session.User.UserName),
            new(SessionIdClaim, session.SessionId.ToString()),
            .. session.User.Permissions.Select(p => new Claim(PermissionClaim, p)),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        await Response.WriteAsJsonAsync(new { code = "unauthenticated" });
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new { code = "forbidden" });
    }
}
