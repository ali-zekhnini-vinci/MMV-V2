using Npgsql;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// 03-D3 (O-03-42) : nettoyage des événements d'audit FICTIFS validés (COMMIT) sur <c>mmv_v2_test</c> UNIQUEMENT. Depuis
/// 0005, le journal est en ajout seul (déclencheurs) et ses clés étrangères sont <c>RESTRICT</c> : un compte ou un client
/// fictif ne peut être supprimé qu'après ses événements. Dans UNE transaction, cible relue côté serveur, le rôle
/// propriétaire de la table désactive le déclencheur de ligne, supprime les seuls événements des comptes ou des clients
/// du test, réactive le déclencheur et contrôle son état avant de valider : hors de cette transaction, le déclencheur
/// n'est jamais vu désactivé. Illustration de la limite documentée (D-03-19) : les déclencheurs protègent contre une
/// erreur applicative, pas contre le rôle propriétaire ni un superutilisateur. Jamais exécuté sur <c>mmv_v2_dev</c>.
/// </summary>
internal static class CommittedAuditCleanup
{
    public static async Task<int> PurgeAsync(IReadOnlyCollection<Guid> users, IReadOnlyCollection<Guid> clients)
    {
        await using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var identity = new NpgsqlCommand(
            "SELECT current_database() = 'mmv_v2_test' AND current_user = 'mmv_v2_test' AND inet_server_port() = 5433", connection, transaction))
        {
            Assert.True((bool)(await identity.ExecuteScalarAsync())!, "Cible de nettoyage refusée (mmv_v2_test/5433 attendu).");
        }

        await using (var disable = new NpgsqlCommand("ALTER TABLE audit.events DISABLE TRIGGER trg_events_append_only", connection, transaction))
        {
            await disable.ExecuteNonQueryAsync();
        }

        int deleted;
        await using (var command = new NpgsqlCommand("DELETE FROM audit.events WHERE actor_user_id = ANY(@u) OR client_id = ANY(@c)", connection, transaction))
        {
            command.Parameters.AddWithValue("u", users.ToArray());
            command.Parameters.AddWithValue("c", clients.ToArray());
            deleted = await command.ExecuteNonQueryAsync();
        }

        await using (var enable = new NpgsqlCommand("ALTER TABLE audit.events ENABLE TRIGGER trg_events_append_only", connection, transaction))
        {
            await enable.ExecuteNonQueryAsync();
        }

        await using (var state = new NpgsqlCommand(
            "SELECT string_agg(tgname || '=' || tgenabled::text, ',' ORDER BY tgname) FROM pg_trigger WHERE tgrelid = 'audit.events'::regclass AND NOT tgisinternal",
            connection, transaction))
        {
            Assert.Equal("trg_events_append_only=O,trg_events_no_truncate=O", (string?)await state.ExecuteScalarAsync());
        }

        await transaction.CommitAsync();
        return deleted;
    }

    /// <summary>Événements restant pour ces comptes ou clients (résidu attendu : 0).</summary>
    public static Task<long> CountAsync(IReadOnlyCollection<Guid> users, IReadOnlyCollection<Guid> clients) =>
        CommittedTestData.CountAsync(
            "SELECT count(*) FROM audit.events WHERE actor_user_id = ANY(@u) OR client_id = ANY(@c)", ("u", users.ToArray()), ("c", clients.ToArray()));
}
