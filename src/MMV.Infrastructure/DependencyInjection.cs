using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Nom de la chaîne de connexion (variable d'environnement <c>ConnectionStrings__Mmv</c>).</summary>
    public const string ConnectionStringName = "Mmv";

    /// <summary>
    /// Enregistre la persistance PostgreSQL. La chaîne de connexion est lue à la première résolution
    /// du contexte, jamais au démarrage : l'API démarre sans base configurée en phase 01A.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MmvDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Chaîne de connexion 'ConnectionStrings:{ConnectionStringName}' absente. "
                    + "Elle doit être fournie hors Git (variable d'environnement ou user-secrets).");
            options.UseNpgsql(connectionString);
        });

        return services;
    }
}
