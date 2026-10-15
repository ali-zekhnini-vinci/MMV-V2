using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MMV.Infrastructure.Persistence;

namespace MMV.Api.Health;

/// <summary>
/// Disponibilité de la base PostgreSQL : ouverture réelle d'une connexion Npgsql, bornée dans le temps.
/// Prouve uniquement que la base est joignable, pas l'état des migrations. La réponse ne contient
/// ni message d'erreur, ni nom de base, ni hôte, ni chaîne de connexion.
/// </summary>
internal static class ReadinessEndpoint
{
    public const string Path = "/health/ready";
    public const string Tag = "ready";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>Délai de connexion Npgsql de la sonde, inférieur au timeout du check.</summary>
    public const int ConnectTimeoutSeconds = 2;

    public static IServiceCollection AddReadinessCheck(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessCheck>("database", HealthStatus.Unhealthy, [Tag], Timeout);
        return services;
    }

    public static IEndpointConventionBuilder MapReadiness(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHealthChecks(Path, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(Tag),
            ResponseWriter = WriteResponseAsync,
        });

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        var payload = new ReadinessResponse(
            Status: report.Status.ToString(),
            Scope: "database",
            CheckedAtUtc: DateTime.UtcNow,
            DurationMs: Math.Round(report.TotalDuration.TotalMilliseconds));

        return context.Response.WriteAsJsonAsync(payload);
    }
}

/// <summary>
/// Le contexte est résolu dans le check (et non injecté) : une chaîne absente devient un résultat
/// Unhealthy (503) au lieu d'une erreur 500 à la construction du check.
/// </summary>
internal sealed class DatabaseReadinessCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        MmvDbContext database;
        try
        {
            database = services.GetRequiredService<MmvDbContext>();
        }
        catch (InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("Chaîne de connexion PostgreSQL non configurée.");
        }

        return await database.Database.CanConnectWithinAsync(ReadinessEndpoint.ConnectTimeoutSeconds, cancellationToken)
            ? HealthCheckResult.Healthy("PostgreSQL joignable.")
            : HealthCheckResult.Unhealthy("PostgreSQL injoignable.");
    }
}

internal sealed record ReadinessResponse(
    string Status,
    string Scope,
    DateTime CheckedAtUtc,
    double DurationMs);
