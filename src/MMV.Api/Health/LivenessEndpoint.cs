using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MMV.Api.Health;

/// <summary>
/// Vivacité du processus API uniquement. Ne vérifie PAS PostgreSQL et ne prouve pas
/// la disponibilité de la base : un futur endpoint de disponibilité (readiness) le fera.
/// </summary>
internal static class LivenessEndpoint
{
    public const string Path = "/health/live";
    public const string Tag = "live";

    public static IServiceCollection AddLivenessCheck(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("process", () => HealthCheckResult.Healthy("Processus API actif."), tags: [Tag]);
        return services;
    }

    public static IEndpointRouteBuilder MapLiveness(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Path, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(Tag),
            ResponseWriter = WriteResponseAsync,
        });
        return endpoints;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        using var process = Process.GetCurrentProcess();
        var startedAtUtc = process.StartTime.ToUniversalTime();
        var payload = new LivenessResponse(
            Status: report.Status.ToString(),
            Scope: "process",
            ProcessId: Environment.ProcessId,
            StartedAtUtc: startedAtUtc,
            UptimeSeconds: Math.Round((DateTime.UtcNow - startedAtUtc).TotalSeconds, 1),
            CheckedAtUtc: DateTime.UtcNow);

        return context.Response.WriteAsJsonAsync(payload);
    }
}

internal sealed record LivenessResponse(
    string Status,
    string Scope,
    int ProcessId,
    DateTime StartedAtUtc,
    double UptimeSeconds,
    DateTime CheckedAtUtc);
