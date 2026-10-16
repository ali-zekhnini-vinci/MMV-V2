using Microsoft.EntityFrameworkCore;
using MMV.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Contrôles de schéma en lecture seule (session <c>default_transaction_read_only=on</c>).
/// Sans trait : valables à tout stade connu (base vide, baseline 01B seule, baseline + identité phase 02).
/// <c>Stage=PostMigration</c> : exigent TOUTES les migrations de l'assembly appliquées (phase 02 : O-02-01),
/// à lancer après l'exécution des scripts approuvés.
/// Historique : en 01B, ces tests exigeaient la baseline seule (voir docs/phase/01b/QA_MATRIX.md).
/// </summary>
public sealed class SchemaStateTests(ITestOutputHelper output)
{
    [Fact]
    public void Assembly_DeclaresExactlyTheReviewedMigrations()
    {
        var options = new DbContextOptionsBuilder<MmvDbContext>().UseNpgsql().Options;
        using var context = new MmvDbContext(options);

        Assert.Equal(SchemaState.AllMigrationIds, context.Database.GetMigrations());
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Dev))]
    public Task DevDatabase_ContainsOnlyAKnownState() => AssertKnownStateAsync(PostgreSqlTarget.Dev);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public Task TestDatabase_ContainsOnlyAKnownState() => AssertKnownStateAsync(PostgreSqlTarget.Test);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Dev))]
    [Trait("Stage", "PostMigration")]
    public Task DevDatabase_AllMigrationsApplied() => AssertAllMigrationsAppliedAsync(PostgreSqlTarget.Dev);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    [Trait("Stage", "PostMigration")]
    public Task TestDatabase_AllMigrationsApplied() => AssertAllMigrationsAppliedAsync(PostgreSqlTarget.Test);

    private async Task AssertKnownStateAsync(PostgreSqlTarget target)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var state = await SchemaInspection.ReadAsync(target, timeout.Token);
        output.WriteLine($"{target.Database} : history_exists={state.HistoryExists}");
        output.WriteLine(state.ToString());

        Assert.Equal("on", state.TransactionReadOnly);
        Assert.Equal("pg_database_owner", state.PublicSchemaOwner);
        var differences = state.KnownStateDifferences(target.Role);
        Assert.True(differences.Count == 0, "Objet inattendu : " + string.Join(" ; ", differences));
    }

    private async Task AssertAllMigrationsAppliedAsync(PostgreSqlTarget target)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var state = await SchemaInspection.ReadAsync(target, timeout.Token);
        output.WriteLine(state.ToString());
        Assert.Empty(state.AuthIdentityDifferences(target.Role));

        // Vue EF Core (lecture seule) : toutes les migrations de l'assembly appliquées, rien en attente.
        var options = new DbContextOptionsBuilder<MmvDbContext>().UseNpgsql(target.ReadOnlyConnectionString()).Options;
        await using var context = new MmvDbContext(options);
        var applied = (await context.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray();
        var pending = (await context.Database.GetPendingMigrationsAsync(timeout.Token)).ToArray();
        output.WriteLine($"EF applied=[{string.Join(", ", applied)}] pending=[{string.Join(", ", pending)}]");

        Assert.Equal(SchemaState.AllMigrationIds, applied);
        Assert.Empty(pending);
    }
}
