using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Auth;

/// <summary>Authentification, policies et limitation de débit de la phase 02.</summary>
internal static class AuthSetup
{
    /// <summary>Policy : administration technique (permission <c>system.admin</c>).</summary>
    public const string SystemAdminPolicy = "SystemAdmin";

    /// <summary>Limitation de débit de <c>POST /api/auth/login</c>, par adresse IP distante.</summary>
    public const string LoginRateLimitPolicy = "login";

    public static IServiceCollection AddMmvAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions());

        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(SystemAdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.SystemAdmin));

        var permitPerMinute = configuration.GetValue("Auth:LoginAttemptsPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
                await context.HttpContext.Response.WriteAsJsonAsync(new { code = "too_many_requests" }, cancellationToken);
            options.AddPolicy(LoginRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
