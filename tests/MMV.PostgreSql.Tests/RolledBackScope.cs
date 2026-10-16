using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Infrastructure;
using MMV.Infrastructure.Persistence;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Portée DI réelle (MMV.Infrastructure) sur mmv_v2_test, cible re-vérifiée côté serveur, transaction
/// ouverte avant toute écriture et annulée à la libération. Les contrôles d'erreur SQL utilisent des
/// points de sauvegarde pour ne pas invalider la transaction englobante.
/// </summary>
internal sealed class RolledBackScope : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly ITestOutputHelper _output;

    private RolledBackScope(ServiceProvider provider, ITestOutputHelper output)
    {
        _provider = provider;
        _scope = provider.CreateAsyncScope();
        _output = output;
        Context = _scope.ServiceProvider.GetRequiredService<MmvDbContext>();
    }

    public MmvDbContext Context { get; }

    public static async Task<RolledBackScope> OpenAsync(ITestOutputHelper output)
    {
        var connectionString = PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = connectionString })
            .Build();
        var scope = new RolledBackScope(new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider(), output);

        await scope.Context.Database.OpenConnectionAsync();
        var identity = await scope.ScalarAsync<bool>(
            "SELECT current_database() = 'mmv_v2_test' AND current_user = 'mmv_v2_test' AND inet_server_port() = 5433");
        Assert.True(identity, "Cible d'écriture refusée (mmv_v2_test/5433 attendu).");
        await scope.Context.Database.BeginTransactionAsync();
        output.WriteLine("transaction ouverte sur mmv_v2_test (ROLLBACK à la fin du test)");
        return scope;
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// Second contexte EF indépendant (autre poste simulé) sur la MÊME connexion et la MÊME transaction :
    /// ses écritures sont annulées avec le reste au ROLLBACK.
    /// </summary>
    public async Task<MmvDbContext> SecondContextAsync()
    {
        var options = new DbContextOptionsBuilder<MmvDbContext>().UseNpgsql(Context.Database.GetDbConnection()).Options;
        var second = new MmvDbContext(options);
        await second.Database.UseTransactionAsync(Context.Database.CurrentTransaction!.GetDbTransaction());
        return second;
    }

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(sql);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var command = Command(sql);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ExpectSqlStateAsync(string sqlState, string sql)
    {
        var transaction = Context.Database.CurrentTransaction!;
        await transaction.CreateSavepointAsync("expect");
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));
        Assert.Equal(sqlState, error.SqlState);
        await transaction.RollbackToSavepointAsync("expect");
    }

    public async ValueTask DisposeAsync()
    {
        if (Context.Database.CurrentTransaction is { } transaction)
        {
            await transaction.RollbackAsync();
            _output.WriteLine("ROLLBACK effectué");
        }

        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private NpgsqlCommand Command(string sql)
    {
        var connection = (NpgsqlConnection)Context.Database.GetDbConnection();
        return new NpgsqlCommand(sql, connection, (NpgsqlTransaction?)Context.Database.CurrentTransaction?.GetDbTransaction());
    }
}
