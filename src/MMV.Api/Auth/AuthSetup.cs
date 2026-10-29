using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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

    /// <summary>
    /// Policies du dossier optique (04-B, D-04-05) : chacune exige <c>clients.read</c> ET <c>optical.read</c>, plus le
    /// droit de l'opération ; aucun droit n'en implique un autre et <c>system.admin</c> n'en satisfait aucune. Contrôle
    /// AVANT toute lecture : un identifiant inconnu et un identifiant non autorisé donnent le même 403.
    /// </summary>
    public const string OpticalReadPolicy = "OpticalRead";
    public const string OpticalPrescriptionsWritePolicy = "OpticalPrescriptionsWrite";
    public const string OpticalMeasurementsWritePolicy = "OpticalMeasurementsWrite";
    public const string OpticalInvalidatePolicy = "OpticalInvalidate";

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
                .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.AuditRead))
            .AddPolicy(OpticalReadPolicy, policy => RequireOptical(policy))
            .AddPolicy(OpticalPrescriptionsWritePolicy, policy => RequireOptical(policy, Permissions.OpticalPrescriptionsWrite))
            .AddPolicy(OpticalMeasurementsWritePolicy, policy => RequireOptical(policy, Permissions.OpticalMeasurementsWrite))
            .AddPolicy(OpticalInvalidatePolicy, policy => RequireOptical(policy, Permissions.OpticalInvalidate));

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

    /// <summary>Chaque exigence est cumulative (toutes les revendications sont exigées).</summary>
    private static AuthorizationPolicyBuilder RequireOptical(AuthorizationPolicyBuilder policy, params string[] operations)
    {
        policy.RequireAuthenticatedUser()
            .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.ClientsRead)
            .RequireClaim(SessionAuthenticationHandler.PermissionClaim, Permissions.OpticalRead);
        foreach (var operation in operations)
        {
            policy.RequireClaim(SessionAuthenticationHandler.PermissionClaim, operation);
        }

        return policy;
    }
}
