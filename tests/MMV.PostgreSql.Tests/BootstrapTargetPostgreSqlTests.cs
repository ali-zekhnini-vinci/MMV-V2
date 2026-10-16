extern alias maintenance;

using maintenance::MMV.Maintenance;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Politique de cible de <c>bootstrap-admin</c> confrontée aux vraies bases V2, en LECTURE SEULE
/// (session <c>default_transaction_read_only=on</c>) : mmv_v2_dev est admise, mmv_v2_test est refusée
/// par l'identité relue côté serveur. Aucun bootstrap, aucune écriture.
/// </summary>
public sealed class BootstrapTargetPostgreSqlTests(ITestOutputHelper output)
{
    [PostgreSqlFact(nameof(PostgreSqlTarget.Dev))]
    public async Task DevDatabase_IsTheAllowedBootstrapTarget()
    {
        Assert.Empty(BootstrapTargetPolicy.CheckConnectionString(PostgreSqlTarget.Dev.ConnectionString!));

        var server = await ReadServerAsync(PostgreSqlTarget.Dev);

        Assert.Empty(BootstrapTargetPolicy.CheckServer(server));
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task TestDatabase_IsRefusedAsBootstrapTarget()
    {
        Assert.Equal(2, BootstrapTargetPolicy.CheckConnectionString(PostgreSqlTarget.Test.ConnectionString!).Count);

        var violations = BootstrapTargetPolicy.CheckServer(await ReadServerAsync(PostgreSqlTarget.Test));

        Assert.Equal(2, violations.Count);
        Assert.StartsWith("base serveur", violations[0], StringComparison.Ordinal);
        Assert.StartsWith("rôle serveur", violations[1], StringComparison.Ordinal);
    }

    private async Task<ServerTarget> ReadServerAsync(PostgreSqlTarget target)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(target.ReadOnlyConnectionString());
        await connection.OpenAsync(timeout.Token);
        var server = await BootstrapTargetPolicy.ReadServerAsync(connection, timeout.Token);
        output.WriteLine($"db={server.Database} role={server.SessionUser}/{server.CurrentUser} port={server.Port} "
            + $"version={server.ServerVersion} {server.ClientAddress} -> {server.ServerAddress}");
        return server;
    }
}
