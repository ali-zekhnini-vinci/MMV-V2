using Microsoft.EntityFrameworkCore;
using MMV.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Contrôles de schéma en lecture seule (session <c>default_transaction_read_only=on</c>).
/// Sans trait : valables avant ET après migration (base vide ou baseline seule, rien d'autre).
/// <c>Stage=PostMigration</c> : exigent la baseline appliquée (O-B07), à lancer après l'exécution du script.
/// </summary>
public sealed class SchemaStateTests(ITestOutputHelper output)
{
    [PostgreSqlFact(nameof(PostgreSqlTarget.Dev))]
    public Task DevDatabase_ContainsNothingButBaseline() => AssertEmptyOrBaselineAsync(PostgreSqlTarget.Dev);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public Task TestDatabase_ContainsNothingButBaseline() => AssertEmptyOrBaselineAsync(PostgreSqlTarget.Test);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Dev))]
    [Trait("Stage", "PostMigration")]
    public Task DevDatabase_BaselineApplied() => AssertBaselineAppliedAsync(PostgreSqlTarget.Dev);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    [Trait("Stage", "PostMigration")]
    public Task TestDatabase_BaselineApplied() => AssertBaselineAppliedAsync(PostgreSqlTarget.Test);

    private async Task AssertEmptyOrBaselineAsync(PostgreSqlTarget target)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var state = await SchemaInspection.ReadAsync(target, timeout.Token);
        output.WriteLine($"{target.Database} : history_exists={state.HistoryExists}");
        output.WriteLine(state.ToString());

        Assert.Equal("on", state.TransactionReadOnly);
        Assert.Equal("pg_database_owner", state.PublicSchemaOwner);
        Assert.True(
            state.IsEmpty || state.BaselineDifferences(target.Role).Count == 0,
            "Objet inattendu : " + string.Join(" ; ", state.BaselineDifferences(target.Role)));
    }

    private async Task AssertBaselineAppliedAsync(PostgreSqlTarget target)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var state = await SchemaInspection.ReadAsync(target, timeout.Token);
        output.WriteLine(state.ToString());
        Assert.Empty(state.BaselineDifferences(target.Role));

        // Vue EF Core (lecture seule) : la migration de l'assembly est la seule appliquée, rien n'est en attente.
        var options = new DbContextOptionsBuilder<MmvDbContext>().UseNpgsql(target.ReadOnlyConnectionString()).Options;
        await using var context = new MmvDbContext(options);
        var applied = (await context.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray();
        var pending = (await context.Database.GetPendingMigrationsAsync(timeout.Token)).ToArray();
        output.WriteLine($"EF applied=[{string.Join(", ", applied)}] pending=[{string.Join(", ", pending)}]");

        Assert.Equal([SchemaState.BaselineMigrationId], applied);
        Assert.Equal([SchemaState.BaselineMigrationId], context.Database.GetMigrations());
        Assert.Empty(pending);
    }
}
