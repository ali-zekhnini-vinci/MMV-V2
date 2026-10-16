using System.Data.Common;
using System.Globalization;
using System.Net;
using Npgsql;

namespace MMV.Maintenance;

/// <summary>
/// Cible UNIQUE autorisée pour <c>bootstrap-admin</c> (phase 02) et <c>permissions grant|revoke</c>
/// (phase 03-A2) : base <c>mmv_v2_dev</c>, rôle <c>mmv_v2_dev</c>, port 5433, PostgreSQL 18, connexion
/// locale (loopback) uniquement.
/// Deux contrôles, tous deux avant la moindre écriture :
/// <list type="number">
///   <item><see cref="CheckConnectionString"/> : chaîne configurée, AVANT toute connexion ;</item>
///   <item><see cref="CheckServer"/> : identité réellement relue côté serveur, après connexion.</item>
/// </list>
/// Tout écart produit un refus sans modification. Aucun mot de passe n'est lu ni affiché.
/// </summary>
internal static class BootstrapTargetPolicy
{
    public const string Database = "mmv_v2_dev";
    public const string Role = "mmv_v2_dev";
    public const int Port = 5433;
    public const int ServerMajorVersion = 18;

    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>Écarts de la chaîne de connexion par rapport à la cible (liste vide = conforme).</summary>
    public static IReadOnlyList<string> CheckConnectionString(string connectionString)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            return ["chaîne de connexion illisible"];
        }

        var violations = new List<string>();
        if (builder.Host is null || !LoopbackHosts.Contains(builder.Host, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"hôte « {builder.Host} » : connexion locale attendue ({string.Join(", ", LoopbackHosts)})");
        }

        if (builder.Port != Port)
        {
            violations.Add(Invariant($"port {builder.Port} : {Port} attendu"));
        }

        if (!string.Equals(builder.Database, Database, StringComparison.Ordinal))
        {
            violations.Add($"base « {builder.Database} » : {Database} attendue");
        }

        if (!string.Equals(builder.Username, Role, StringComparison.Ordinal))
        {
            violations.Add($"rôle « {builder.Username} » : {Role} attendu");
        }

        return violations;
    }

    /// <summary>Écarts de l'identité serveur relue après connexion (liste vide = conforme).</summary>
    public static IReadOnlyList<string> CheckServer(ServerTarget server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var violations = new List<string>();
        if (!string.Equals(server.Database, Database, StringComparison.Ordinal))
        {
            violations.Add($"base serveur « {server.Database} » : {Database} attendue");
        }

        if (!string.Equals(server.CurrentUser, Role, StringComparison.Ordinal)
            || !string.Equals(server.SessionUser, Role, StringComparison.Ordinal))
        {
            violations.Add($"rôle serveur « {server.SessionUser} / {server.CurrentUser} » : {Role} attendu");
        }

        if (server.Port != Port)
        {
            violations.Add(Invariant($"port serveur {server.Port} : {Port} attendu"));
        }

        if (server.ServerVersionNum / 10000 != ServerMajorVersion)
        {
            violations.Add(Invariant($"PostgreSQL {server.ServerVersion} : version {ServerMajorVersion} attendue"));
        }

        if (!IsLoopback(server.ServerAddress) || !IsLoopback(server.ClientAddress))
        {
            violations.Add($"connexion « {server.ClientAddress?.ToString() ?? "socket"} → {server.ServerAddress?.ToString() ?? "socket"} » : boucle locale TCP attendue");
        }

        return violations;
    }

    /// <summary>Lecture seule : identité serveur de la connexion (SELECT de fonctions système uniquement).</summary>
    public static async Task<ServerTarget> ReadServerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT current_database(), current_user, session_user, inet_server_port(), "
            + "current_setting('server_version_num')::int, current_setting('server_version'), "
            + "host(inet_server_addr()), host(inet_client_addr())";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Identité du serveur illisible.");
        }

        return new ServerTarget(
            Database: reader.GetString(0),
            CurrentUser: reader.GetString(1),
            SessionUser: reader.GetString(2),
            Port: reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
            ServerVersionNum: reader.GetInt32(4),
            ServerVersion: reader.GetString(5),
            ServerAddress: ParseAddress(reader, 6),
            ClientAddress: ParseAddress(reader, 7));
    }

    private static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    private static IPAddress? ParseAddress(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && IPAddress.TryParse(reader.GetString(ordinal), out var address) ? address : null;

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identité d'une connexion PostgreSQL, relue côté serveur. Adresses <c>null</c> : socket Unix.</summary>
internal sealed record ServerTarget(
    string Database,
    string CurrentUser,
    string SessionUser,
    int? Port,
    int ServerVersionNum,
    string ServerVersion,
    IPAddress? ServerAddress,
    IPAddress? ClientAddress);
