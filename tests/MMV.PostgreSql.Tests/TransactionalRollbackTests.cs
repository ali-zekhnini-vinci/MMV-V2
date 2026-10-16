using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Écriture éphémère autorisée par O-B02 : <c>mmv_v2_test</c> uniquement, après migration baseline.
/// Table temporaire créée dans une transaction puis <c>ROLLBACK</c> ; contrôle d'absence d'objet résiduel.
/// Toute anomalie de cible arrête le test AVANT la première écriture. Jamais <c>EnsureCreated()</c>.
/// </summary>
public sealed class TransactionalRollbackTests(ITestOutputHelper output)
{
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    [Trait("Stage", "Write")]
    public async Task TestDatabase_TemporaryWriteIsRolledBackWithoutResidue()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var target = PostgreSqlTarget.Test;

        // 1. Garde de cible (lecture seule) : identité, version, absence de privilège administrateur, schéma connu.
        var identity = await target.ReadIdentityAsync(timeout.Token);
        output.WriteLine(identity.ToString());
        Assert.Equal("mmv_v2_test", identity.Database);
        Assert.Equal("mmv_v2_test", identity.Role);
        Assert.Equal(PostgreSqlTarget.Port, identity.Port);
        Assert.True(identity.ServerVersionNum >= PostgreSqlTarget.MinimumServerVersionNum);
        Assert.False(identity.IsSuperuser);
        Assert.False(identity.BypassesRls);

        var before = await SchemaInspection.ReadAsync(target, timeout.Token);
        // Baseline 01B seule, ou baseline + identité (phase 02) : tout autre état est refusé avant écriture.
        Assert.Empty(before.KnownStateDifferences(target.Role));

        var table = $"mmv_ob02_probe_{Guid.NewGuid():N}";
        output.WriteLine($"table temporaire : {table}");

        await using (var connection = new NpgsqlConnection(target.ValidatedBuilder().ConnectionString))
        {
            await connection.OpenAsync(timeout.Token);

            // 2. Re-vérification sur la connexion qui va écrire, avant la transaction.
            await using (var check = new NpgsqlCommand(
                "SELECT current_database() = 'mmv_v2_test' AND current_user = 'mmv_v2_test' AND inet_server_port() = 5433",
                connection))
            {
                Assert.True((bool)(await check.ExecuteScalarAsync(timeout.Token))!, "Cible de la connexion d'écriture refusée.");
            }

            // 3. Écriture éphémère dans une transaction annulée.
            await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
            await ExecuteAsync(connection, $"CREATE TEMP TABLE {table} (id integer PRIMARY KEY, label text NOT NULL) ON COMMIT DROP", timeout.Token);
            await ExecuteAsync(connection, $"INSERT INTO {table} (id, label) VALUES (1, 'o-b02-rollback-probe')", timeout.Token);
            Assert.Equal("o-b02-rollback-probe", await ScalarAsync(connection, $"SELECT label FROM {table} WHERE id = 1", timeout.Token));
            Assert.Equal(true, await ScalarAsync(connection, $"SELECT to_regclass('pg_temp.{table}') IS NOT NULL", timeout.Token));

            await transaction.RollbackAsync(timeout.Token);

            Assert.Equal(true, await ScalarAsync(connection, $"SELECT to_regclass('pg_temp.{table}') IS NULL", timeout.Token));
        }

        // 4. Absence d'objet résiduel, relue sur une nouvelle session en lecture seule.
        await using (var connection = new NpgsqlConnection(target.ReadOnlyConnectionString()))
        {
            await connection.OpenAsync(timeout.Token);
            Assert.Equal(0L, await ScalarAsync(connection, $"SELECT count(*) FROM pg_class WHERE relname = '{table}'", timeout.Token));
        }

        var after = await SchemaInspection.ReadAsync(target, timeout.Token);
        output.WriteLine(after.ToString());
        Assert.Equal(before.ToString(), after.ToString());
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
