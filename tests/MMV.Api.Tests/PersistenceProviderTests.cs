using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Infrastructure;
using MMV.Infrastructure.Persistence;

namespace MMV.Api.Tests;

/// <summary>
/// Vérifie la configuration du fournisseur sans ouvrir de connexion : aucune base n'est touchée.
/// La persistance réelle reste NOT_TESTED tant qu'une base PostgreSQL V2 isolée n'est pas approuvée.
/// </summary>
public sealed class PersistenceProviderTests
{
    [Fact]
    public void DbContextUsesNpgsqlProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Hôte non résolvable volontairement : aucune connexion n'est tentée par ce test.
                ["ConnectionStrings:Mmv"] = "Host=mmv-test.invalid;Database=unused",
            })
            .Build();

        using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }

    [Fact]
    public void MissingConnectionStringFailsExplicitlyOnResolution()
    {
        var configuration = new ConfigurationBuilder().Build();

        using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<MmvDbContext>().Database.ProviderName);
        Assert.Contains("ConnectionStrings:Mmv", error.Message, StringComparison.Ordinal);
    }
}
