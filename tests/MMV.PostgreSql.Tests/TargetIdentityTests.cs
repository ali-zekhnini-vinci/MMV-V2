using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Vérification d'identité des bases V2 AVANT toute écriture : lecture seule (SELECT de catalogues),
/// aucune création, aucune migration, aucune donnée écrite.
/// </summary>
public sealed class TargetIdentityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=mmv_v2_test;Username=mmv_v2_test")]
    [InlineData("Host=localhost;Port=5433;Database=postgres;Username=mmv_v2_test")]
    [InlineData("Host=localhost;Port=5433;Database=mmv_v2_test;Username=postgres")]
    [InlineData("Host=192.0.2.10;Port=5433;Database=mmv_v2_test;Username=mmv_v2_test")]
    public void Validate_RefusesForeignTargetWithoutConnecting(string connectionString)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlTarget.Test.Validate(connectionString));

        Assert.StartsWith("Cible refusée", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsExactTestTarget()
    {
        var builder = PostgreSqlTarget.Test.Validate("Host=localhost;Port=5433;Database=mmv_v2_test;Username=mmv_v2_test");

        Assert.Equal("mmv_v2_test", builder.Database);
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Dev))]
    public Task DevDatabase_IdentityMatches() => AssertIdentityAsync(PostgreSqlTarget.Dev);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public Task TestDatabase_IdentityMatches() => AssertIdentityAsync(PostgreSqlTarget.Test);

    private async Task AssertIdentityAsync(PostgreSqlTarget target)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var identity = await target.ReadIdentityAsync(timeout.Token);
        output.WriteLine(identity.ToString());

        Assert.Equal("on", identity.TransactionReadOnly);
        Assert.Equal(target.Database, identity.Database);
        Assert.Equal(target.Role, identity.Role);
        Assert.Equal(PostgreSqlTarget.Port, identity.Port);
        Assert.True(identity.ServerVersionNum >= PostgreSqlTarget.MinimumServerVersionNum, "PostgreSQL 18 attendu.");
        Assert.True(identity.OwnsDatabase, "Le rôle doit être propriétaire de sa seule base.");
        Assert.False(identity.IsSuperuser);
        Assert.False(identity.CanCreateDb);
        Assert.False(identity.CanCreateRole);
        Assert.False(identity.CanReplicate);
        Assert.False(identity.BypassesRls);
        Assert.False(identity.PublicCanConnect, "REVOKE ALL ... FROM PUBLIC attendu (O-B01).");
    }
}
