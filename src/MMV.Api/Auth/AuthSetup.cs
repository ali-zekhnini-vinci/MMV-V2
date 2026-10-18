using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Auth;

/// <summary>Authentification, policies et limitation de débit (phase 02 ; policies Clients en phase 03-A2, Rendez-vous en 03-B2).</summary>
internal static class AuthSetup
{
    /// <summary>Policy : administration technique (permission <c>system.admin</c>).</summary>
    public const string SystemAdminPolicy = "SystemAdmin";

    /// <summary>Policies Clients : chacune exige SA permission ; <c>system.admin</c> n'en satisfait aucune.</summary>
    public const string ClientsReadPolicy = "ClientsRead";
    public const string ClientsWritePolicy = "ClientsWrite";
    public const string ClientsArchivePolicy = "ClientsArchive";

    /// <summary>Policies Rendez-vous (D-03-17) : chacune exige SA permission ; <c>system.admin</c> n'en satisfait aucune.</summary>
    public const string AppointmentsReadPolicy = "AppointmentsRead";
    public const string AppointmentsWritePolicy = "AppointmentsWrite";
    public const string AppointmentsCancelPolicy = "AppointmentsCancel";

    /// <summary>Policy Audit (03-D2) : droit dédié ; aucun droit métier ni <c>system.admin</c> ne la satisfait.</summary>
    public const string AuditReadPolicy = "AuditRead";

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
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.SystemAdmin))
            .AddPolicy(ClientsReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.ClientsRead))
            .AddPolicy(ClientsWritePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.ClientsWrite))
            .AddPolicy(ClientsArchivePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.ClientsArchive))
            .AddPolicy(AppointmentsReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.AppointmentsRead))
            .AddPolicy(AppointmentsWritePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.AppointmentsWrite))
            .AddPolicy(AppointmentsCancelPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.AppointmentsCancel))
            .AddPolicy(AuditReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.AuditRead));

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
