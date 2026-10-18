using System.Globalization;
using Microsoft.Extensions.Configuration;
using Npgsql;

// Exécution séquentielle (03-B3) : les rôles de test sont limités à 10 connexions (durcissement Phase 01b) et les
// tests Rendez-vous VALIDÉS (COMMIT) seraient visibles des tests annulés d'autres classes exécutées en parallèle
// (constaté : 53300 « trop de connexions » et capacité d'accueil déjà occupée).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Cible PostgreSQL V2 autorisée, lue hors dépôt (user-secrets ou variable d'environnement).
/// Avant toute connexion, la chaîne est contrôlée (hôte loopback, port 5433, base et rôle attendus) ;
/// après connexion, l'identité réelle est relue côté serveur. Toute autre cible est refusée.
/// </summary>
public sealed record PostgreSqlTarget(string Key, string Database, string Role)
{
    public const int Port = 5433;
    public const int MinimumServerVersionNum = 180000;

    /// <summary>Base de développement de l'API (secret du projet MMV.Api) : lecture d'identité seulement.</summary>
    public static readonly PostgreSqlTarget Dev = new("ConnectionStrings:Mmv", "mmv_v2_dev", "mmv_v2_dev");

    /// <summary>Base de tests : seule cible autorisée pour les écritures éphémères avec ROLLBACK (GO O-B02).</summary>
    public static readonly PostgreSqlTarget Test = new("ConnectionStrings:MmvTest", "mmv_v2_test", "mmv_v2_test");

    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    private static readonly Lazy<IConfiguration> Configuration = new(() => new ConfigurationBuilder()
        .AddUserSecrets("mmv-v2-api")
        .AddUserSecrets("mmv-v2-postgresql-tests")
        .AddEnvironmentVariables()
        .Build());

    public string? ConnectionString => Configuration.Value[Key];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    /// <summary>Refuse, sans se connecter, toute chaîne qui ne vise pas exactement cette cible.</summary>
    public NpgsqlConnectionStringBuilder ValidatedBuilder() => Validate(
        IsConfigured
            ? ConnectionString!
            : throw new InvalidOperationException($"'{Key}' absente (user-secrets ou variable d'environnement)."));

    public NpgsqlConnectionStringBuilder Validate(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Require(LoopbackHosts.Contains(builder.Host, StringComparer.OrdinalIgnoreCase), "hôte loopback");
        Require(builder.Port == Port, $"port {Port}");
        Require(builder.Database == Database, $"base {Database}");
        Require(builder.Username == Role, $"rôle {Role}");
        return builder;
    }

    /// <summary>
    /// Chaîne validée dont la session est forcée en lecture seule côté serveur
    /// (<c>default_transaction_read_only=on</c>) : toute écriture accidentelle échoue.
    /// </summary>
    public string ReadOnlyConnectionString()
    {
        var builder = ValidatedBuilder();
        builder.Options = "-c default_transaction_read_only=on";
        return builder.ConnectionString;
    }

    /// <summary>Lecture seule : identité serveur et attributs du rôle connecté.</summary>
    public async Task<ServerIdentity> ReadIdentityAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ReadOnlyConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT current_database(), current_user, inet_server_port(),
                   current_setting('server_version_num')::int, current_setting('transaction_read_only'),
                   r.rolsuper, r.rolcreatedb, r.rolcreaterole, r.rolreplication, r.rolbypassrls,
                   d.datdba = r.oid AS owns_database,
                   has_database_privilege('public', current_database(), 'CONNECT') AS public_can_connect
            FROM pg_roles r
            JOIN pg_database d ON d.datname = current_database()
            WHERE r.rolname = current_user
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Identité du rôle courant introuvable.");
        }

        return new ServerIdentity(
            Database: reader.GetString(0),
            Role: reader.GetString(1),
            Port: reader.GetInt32(2),
            ServerVersionNum: reader.GetInt32(3),
            TransactionReadOnly: reader.GetString(4),
            IsSuperuser: reader.GetBoolean(5),
            CanCreateDb: reader.GetBoolean(6),
            CanCreateRole: reader.GetBoolean(7),
            CanReplicate: reader.GetBoolean(8),
            BypassesRls: reader.GetBoolean(9),
            OwnsDatabase: reader.GetBoolean(10),
            PublicCanConnect: reader.GetBoolean(11));
    }

    private void Require(bool condition, string expectation)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                $"Cible refusée pour '{Key}' : {expectation} attendu. Aucune connexion n'a été ouverte.");
        }
    }
}

public sealed record ServerIdentity(
    string Database,
    string Role,
    int Port,
    int ServerVersionNum,
    string TransactionReadOnly,
    bool IsSuperuser,
    bool CanCreateDb,
    bool CanCreateRole,
    bool CanReplicate,
    bool BypassesRls,
    bool OwnsDatabase,
    bool PublicCanConnect)
{
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"db={Database} role={Role} port={Port} version_num={ServerVersionNum} owner={OwnsDatabase} "
        + $"super={IsSuperuser} createdb={CanCreateDb} createrole={CanCreateRole} replication={CanReplicate} "
        + $"bypassrls={BypassesRls} public_connect={PublicCanConnect}");
}

/// <summary>Variante paramétrée de <see cref="PostgreSqlFactAttribute"/> (03-B3) : « Skipped » si la cible est absente.</summary>
public sealed class PostgreSqlTheoryAttribute : TheoryAttribute
{
    public PostgreSqlTheoryAttribute(string target)
        : this(target, requiresAuditJournal: false)
    {
    }

    /// <summary>Voir <see cref="PostgreSqlFactAttribute(string, bool)"/>.</summary>
    public PostgreSqlTheoryAttribute(string target, bool requiresAuditJournal)
    {
        var resolved = target == nameof(PostgreSqlTarget.Dev) ? PostgreSqlTarget.Dev : PostgreSqlTarget.Test;
        if (!resolved.IsConfigured)
        {
            Skip = $"NOT_TESTED : '{resolved.Key}' non configurée (voir docs/phase/01b/IMPLEMENTATION_SPEC.md).";
        }
        else if (requiresAuditJournal && !PostgreSqlFactAttribute.AuditJournalPresent(resolved))
        {
            Skip = $"NOT_TESTED : exige la migration 0005_AuditJournal appliquée à {resolved.Database} (revue du propriétaire requise).";
        }
    }
}

/// <summary>
/// Test PostgreSQL réel : « Skipped » (NOT_TESTED), jamais « Passed », si la chaîne de la cible est absente.
/// </summary>
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute(string target)
        : this(target, requiresAuditJournal: false)
    {
    }

    /// <summary>
    /// <paramref name="requiresAuditJournal"/> (03-D2) : test à données VALIDÉES ou à plusieurs connexions, qui ne peut
    /// pas simuler 0005 dans une transaction annulée — « Skipped » (NOT_TESTED) tant que <c>audit.events</c> n'existe
    /// pas dans la base cible (migration soumise à la revue du propriétaire), jamais « Passed ».
    /// </summary>
    public PostgreSqlFactAttribute(string target, bool requiresAuditJournal)
    {
        var resolved = target == nameof(PostgreSqlTarget.Dev) ? PostgreSqlTarget.Dev : PostgreSqlTarget.Test;
        if (!resolved.IsConfigured)
        {
            Skip = $"NOT_TESTED : '{resolved.Key}' non configurée (voir docs/phase/01b/IMPLEMENTATION_SPEC.md).";
        }
        else if (requiresAuditJournal && !AuditJournalPresent(resolved))
        {
            Skip = $"NOT_TESTED : exige la migration 0005_AuditJournal appliquée à {resolved.Database} (revue du propriétaire requise).";
        }
    }

    internal static bool AuditJournalPresent(PostgreSqlTarget target)
    {
        try
        {
            using var connection = new NpgsqlConnection(target.ReadOnlyConnectionString());
            connection.Open();
            using var command = new NpgsqlCommand("SELECT to_regclass('audit.events') IS NOT NULL", connection);
            return (bool)command.ExecuteScalar()!;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }
}
