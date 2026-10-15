using System.Net;

namespace MMV.Maintenance.Tests;

/// <summary>
/// Politique de cible de <c>bootstrap-admin</c> (phase 02) : seule la cible
/// mmv_v2_dev / mmv_v2_dev / 5433 / PostgreSQL 18 / boucle locale est admise ; tout écart est refusé.
/// </summary>
public sealed class BootstrapTargetPolicyTests
{
    private const string Allowed = "Host=localhost;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev";

    private static readonly ServerTarget AllowedServer = new(
        Database: "mmv_v2_dev",
        CurrentUser: "mmv_v2_dev",
        SessionUser: "mmv_v2_dev",
        Port: 5433,
        ServerVersionNum: 180006,
        ServerVersion: "18.6",
        ServerAddress: IPAddress.Loopback,
        ClientAddress: IPAddress.Loopback);

    [Theory]
    [InlineData(Allowed)]
    [InlineData("Host=127.0.0.1;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev;Password=x")]
    [InlineData("Host=::1;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev")]
    [InlineData("Host=LOCALHOST;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev")]
    public void ConnectionString_AllowedTarget_HasNoViolation(string connectionString)
    {
        Assert.Empty(BootstrapTargetPolicy.CheckConnectionString(connectionString));
    }

    [Theory]
    [InlineData("Host=192.0.2.10;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev", "hôte")]
    [InlineData("Host=serveur-mmv;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev", "hôte")]
    [InlineData("Host=localhost,192.0.2.10;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev", "hôte")]
    [InlineData("Host=/var/run/postgresql;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev", "hôte")]
    [InlineData("Host=localhost;Port=5432;Database=mmv_v2_dev;Username=mmv_v2_dev", "port")]
    [InlineData("Host=localhost;Database=mmv_v2_dev;Username=mmv_v2_dev", "port")]
    [InlineData("Host=localhost;Port=5433;Database=mmv_v2_test;Username=mmv_v2_dev", "base")]
    [InlineData("Host=localhost;Port=5433;Database=postgres;Username=mmv_v2_dev", "base")]
    [InlineData("Host=localhost;Port=5433;Database=MMV_V2_DEV;Username=mmv_v2_dev", "base")]
    [InlineData("Host=localhost;Port=5433;Username=mmv_v2_dev", "base")]
    [InlineData("Host=localhost;Port=5433;Database=mmv_v2_dev;Username=postgres", "rôle")]
    [InlineData("Host=localhost;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_test", "rôle")]
    [InlineData("Host=localhost;Port=5433;Database=mmv_v2_dev", "rôle")]
    public void ConnectionString_AnyDeviation_IsRefused(string connectionString, string expected)
    {
        var violations = BootstrapTargetPolicy.CheckConnectionString(connectionString);

        Assert.Single(violations);
        Assert.StartsWith(expected, violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionString_TestTarget_IsRefusedOnDatabaseAndRole()
    {
        var violations = BootstrapTargetPolicy.CheckConnectionString(
            "Host=localhost;Port=5433;Database=mmv_v2_test;Username=mmv_v2_test");

        Assert.Equal(2, violations.Count);
    }

    [Fact]
    public void ConnectionString_Unreadable_IsRefused()
    {
        Assert.NotEmpty(BootstrapTargetPolicy.CheckConnectionString("ceci n'est pas une chaîne"));
    }

    [Fact]
    public void ConnectionString_ViolationsNeverExposePassword()
    {
        var violations = BootstrapTargetPolicy.CheckConnectionString(
            "Host=192.0.2.10;Port=5432;Database=postgres;Username=postgres;Password=Secret-123");

        Assert.Equal(4, violations.Count);
        Assert.DoesNotContain(violations, v => v.Contains("Secret-123", StringComparison.Ordinal));
    }

    [Fact]
    public void Server_AllowedTarget_HasNoViolation()
    {
        Assert.Empty(BootstrapTargetPolicy.CheckServer(AllowedServer));
        Assert.Empty(BootstrapTargetPolicy.CheckServer(AllowedServer with
        {
            ServerAddress = IPAddress.IPv6Loopback,
            ClientAddress = IPAddress.IPv6Loopback,
        }));
        Assert.Empty(BootstrapTargetPolicy.CheckServer(AllowedServer with
        {
            ServerAddress = IPAddress.Loopback.MapToIPv6(),
            ClientAddress = IPAddress.Loopback.MapToIPv6(),
        }));
    }

    private static readonly Dictionary<string, (ServerTarget Server, string Expected)> ServerDeviations = new()
    {
        ["base"] = (AllowedServer with { Database = "mmv_v2_test" }, "base serveur"),
        ["base postgres"] = (AllowedServer with { Database = "postgres" }, "base serveur"),
        ["rôle courant (SET ROLE)"] = (AllowedServer with { CurrentUser = "postgres" }, "rôle serveur"),
        ["rôle de session"] = (AllowedServer with { SessionUser = "mmv_v2_test" }, "rôle serveur"),
        ["port"] = (AllowedServer with { Port = 5432 }, "port serveur"),
        ["port inconnu (socket)"] = (AllowedServer with { Port = null }, "port serveur"),
        ["PostgreSQL 17"] = (AllowedServer with { ServerVersionNum = 170005, ServerVersion = "17.5" }, "PostgreSQL"),
        ["PostgreSQL 19"] = (AllowedServer with { ServerVersionNum = 190000, ServerVersion = "19.0" }, "PostgreSQL"),
        ["serveur distant"] = (AllowedServer with { ServerAddress = IPAddress.Parse("192.0.2.10") }, "connexion"),
        ["client distant"] = (AllowedServer with { ClientAddress = IPAddress.Parse("192.0.2.20") }, "connexion"),
        ["socket Unix"] = (AllowedServer with { ServerAddress = null, ClientAddress = null }, "connexion"),
    };

    [Theory]
    [InlineData("base")]
    [InlineData("base postgres")]
    [InlineData("rôle courant (SET ROLE)")]
    [InlineData("rôle de session")]
    [InlineData("port")]
    [InlineData("port inconnu (socket)")]
    [InlineData("PostgreSQL 17")]
    [InlineData("PostgreSQL 19")]
    [InlineData("serveur distant")]
    [InlineData("client distant")]
    [InlineData("socket Unix")]
    public void Server_AnyDeviation_IsRefused(string label)
    {
        var (server, expected) = ServerDeviations[label];
        var violations = BootstrapTargetPolicy.CheckServer(server);

        Assert.True(violations.Count == 1, $"{label} : {string.Join(" | ", violations)}");
        Assert.StartsWith(expected, violations[0], StringComparison.Ordinal);
    }
}
