using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MMV.Infrastructure.Persistence;

/// <summary>
/// Contexte utilisé uniquement par l'outil <c>dotnet ef</c> pour générer migrations et scripts SQL.
/// Aucune chaîne de connexion : la génération ne lit aucun secret et n'ouvre jamais de connexion.
/// L'application d'une migration passe par le script SQL revu, jamais par cette fabrique.
/// </summary>
internal sealed class MmvDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MmvDbContext>
{
    public MmvDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MmvDbContext>()
            .UseNpgsql()
            .Options;

        return new MmvDbContext(options);
    }
}
