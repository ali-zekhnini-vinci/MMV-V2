using Npgsql;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Inventaire des objets d'une base V2, relu par SELECT de catalogues dans une session forcée en lecture seule.
/// Sert de contrôle avant migration (base vide), après migration (baseline seule) et après les tests d'écriture
/// (aucun objet résiduel).
/// </summary>
public sealed record SchemaState(
    string TransactionReadOnly,
    string PublicSchemaOwner,
    IReadOnlyList<string> ExtraSchemas,
    IReadOnlyList<string> Relations,
    IReadOnlyList<string> Routines,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Decorations,
    IReadOnlyList<string> HistoryColumns,
    IReadOnlyList<string> HistoryConstraints,
    IReadOnlyList<string> HistoryRows)
{
    public const string BaselineMigrationId = "20261015125000_InitialBaseline";

    public bool HistoryExists => Relations.Any(r => r.StartsWith("public.__EFMigrationsHistory ", StringComparison.Ordinal));

    /// <summary>Base neuve : aucun objet utilisateur.</summary>
    public bool IsEmpty =>
        ExtraSchemas.Count == 0 && Relations.Count == 0 && Routines.Count == 0 && Types.Count == 0
        && Extensions.Count == 0 && Decorations.Count == 0;

    /// <summary>Exactement l'état produit par 0001_InitialBaseline.idempotent.sql, objets possédés par <paramref name="role"/>.</summary>
    public IReadOnlyList<string> BaselineDifferences(string role)
    {
        var differences = new List<string>();
        Expect(differences, "schémas", ExtraSchemas, []);
        Expect(differences, "relations", Relations,
        [
            $"public.PK___EFMigrationsHistory kind=i owner={role} acl=default",
            $"public.__EFMigrationsHistory kind=r owner={role} acl=default",
        ]);
        Expect(differences, "routines", Routines, []);
        Expect(differences, "types", Types, []);
        Expect(differences, "extensions", Extensions, []);
        Expect(differences, "décorations", Decorations, []);
        Expect(differences, "colonnes", HistoryColumns,
        [
            "MigrationId character varying(150) notnull=t",
            "ProductVersion character varying(32) notnull=t",
        ]);
        // PostgreSQL 18 catalogue aussi les NOT NULL dans pg_constraint (contype 'n'), avec des noms générés.
        Expect(differences, "contraintes", HistoryConstraints,
        [
            "PK___EFMigrationsHistory PRIMARY KEY (\"MigrationId\")",
            "__EFMigrationsHistory_MigrationId_not_null NOT NULL \"MigrationId\"",
            "__EFMigrationsHistory_ProductVersion_not_null NOT NULL \"ProductVersion\"",
        ]);
        Expect(differences, "lignes", HistoryRows, [$"{BaselineMigrationId} 10.0.4"]);
        return differences;
    }

    public override string ToString() => string.Join(
        Environment.NewLine,
        $"transaction_read_only={TransactionReadOnly} public_owner={PublicSchemaOwner}",
        Section("schémas hors public", ExtraSchemas),
        Section("relations", Relations),
        Section("routines", Routines),
        Section("types", Types),
        Section("extensions", Extensions),
        Section("triggers/règles/politiques/ACL par défaut", Decorations),
        Section("colonnes __EFMigrationsHistory", HistoryColumns),
        Section("contraintes __EFMigrationsHistory", HistoryConstraints),
        Section("lignes __EFMigrationsHistory", HistoryRows));

    private static string Section(string name, IReadOnlyList<string> items) =>
        $"{name} ({items.Count}) : " + (items.Count == 0 ? "—" : string.Join(" | ", items));

    private static void Expect(List<string> differences, string name, IReadOnlyList<string> actual, string[] expected)
    {
        var sortedExpected = expected.Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(sortedExpected, StringComparer.Ordinal))
        {
            differences.Add($"{name} : attendu [{string.Join(" | ", sortedExpected)}], obtenu [{string.Join(" | ", actual)}]");
        }
    }
}

public static class SchemaInspection
{
    private const string UserNamespaces =
        "n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname !~ '^pg_(toast|temp)'";

    public static async Task<SchemaState> ReadAsync(PostgreSqlTarget target, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(target.ReadOnlyConnectionString());
        await connection.OpenAsync(cancellationToken);

        var historyExists = (bool)(await ScalarAsync(
            connection, "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL", cancellationToken))!;

        return new SchemaState(
            TransactionReadOnly: (string)(await ScalarAsync(
                connection, "SELECT current_setting('transaction_read_only')", cancellationToken))!,
            PublicSchemaOwner: (string)(await ScalarAsync(
                connection, "SELECT pg_get_userbyid(nspowner)::text FROM pg_namespace WHERE nspname = 'public'", cancellationToken))!,
            ExtraSchemas: await ListAsync(connection, """
                SELECT nspname::text FROM pg_namespace
                WHERE nspname NOT IN ('public', 'information_schema') AND nspname !~ '^pg_'
                """, cancellationToken),
            Relations: await ListAsync(connection, $"""
                SELECT format('%s.%s kind=%s owner=%s acl=%s', n.nspname, c.relname, c.relkind,
                              pg_get_userbyid(c.relowner), coalesce(c.relacl::text, 'default'))
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE {UserNamespaces}
                """, cancellationToken),
            Routines: await ListAsync(connection, $"""
                SELECT format('%s.%s', n.nspname, p.proname)
                FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE {UserNamespaces}
                """, cancellationToken),
            Types: await ListAsync(connection, $"""
                SELECT format('%s.%s typtype=%s', n.nspname, t.typname, t.typtype)
                FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                WHERE {UserNamespaces}
                  AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.reltype = t.oid)
                  AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.reltype = t.typelem)
                """, cancellationToken),
            Extensions: await ListAsync(connection,
                "SELECT extname::text FROM pg_extension WHERE extname <> 'plpgsql'", cancellationToken),
            Decorations: await ListAsync(connection, $"""
                SELECT format('trigger %s on %s', tgname, tgrelid::regclass) FROM pg_trigger WHERE NOT tgisinternal
                UNION ALL SELECT format('policy %s on %s', polname, polrelid::regclass) FROM pg_policy
                UNION ALL SELECT format('rule %s on %s', r.rulename, r.ev_class::regclass)
                          FROM pg_rewrite r JOIN pg_class c ON c.oid = r.ev_class
                          JOIN pg_namespace n ON n.oid = c.relnamespace WHERE {UserNamespaces}
                UNION ALL SELECT format('default_acl %s', defaclacl) FROM pg_default_acl
                UNION ALL SELECT format('event_trigger %s', evtname) FROM pg_event_trigger
                """, cancellationToken),
            HistoryColumns: historyExists
                ? await ListAsync(connection, """
                    SELECT format('%s %s notnull=%s', attname, format_type(atttypid, atttypmod), attnotnull)
                    FROM pg_attribute
                    WHERE attrelid = 'public."__EFMigrationsHistory"'::regclass AND attnum > 0 AND NOT attisdropped
                    """, cancellationToken)
                : [],
            HistoryConstraints: historyExists
                ? await ListAsync(connection, """
                    SELECT format('%s %s', conname, pg_get_constraintdef(oid))
                    FROM pg_constraint WHERE conrelid = 'public."__EFMigrationsHistory"'::regclass
                    """, cancellationToken)
                : [],
            HistoryRows: historyExists
                ? await ListAsync(connection,
                    "SELECT format('%s %s', \"MigrationId\", \"ProductVersion\") FROM public.\"__EFMigrationsHistory\"",
                    cancellationToken)
                : []);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>Résultat trié en ordinal côté client : comparaison indépendante de la collation serveur.</summary>
    private static async Task<IReadOnlyList<string>> ListAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        var items = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetString(0));
        }

        items.Sort(StringComparer.Ordinal);
        return items;
    }
}
