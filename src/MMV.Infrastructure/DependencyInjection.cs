using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMV.Application.Identity;
using MMV.Infrastructure.Identity;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Nom de la chaîne de connexion (variable d'environnement <c>ConnectionStrings__Mmv</c>).</summary>
    public const string ConnectionStringName = "Mmv";

    /// <summary>
    /// Enregistre la persistance PostgreSQL et les services d'identité. La chaîne de connexion est lue
    /// à la première résolution du contexte, jamais au démarrage : l'API démarre sans base configurée.
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

        // Identité (phase 02). Les options peuvent être enregistrées avant cet appel (MMV.Api les lit
        // dans la section « Auth ») ; à défaut, valeurs par défaut de AuthOptions.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new AuthOptions());
        services.TryAddSingleton<IPasswordHashing, IdentityPasswordHashing>();
        services.TryAddScoped<IIdentityStore, IdentityStore>();
        services.TryAddScoped<AuthService>();
        services.TryAddScoped<BootstrapService>();

        return services;
    }
}
