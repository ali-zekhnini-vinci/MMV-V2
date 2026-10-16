using Npgsql;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Inventaire des objets d'une base V2, relu par SELECT de catalogues dans une session forcée en lecture seule.
/// Sert de contrôle avant migration (base vide), après la baseline 01B (baseline seule), après la migration
/// d'identité de la phase 02 (baseline + schéma <c>auth</c> exact) et après les tests d'écriture (aucun objet résiduel).
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
    IReadOnlyList<string> HistoryRows,
    IReadOnlyList<string> AuthColumns,
    IReadOnlyList<string> AuthConstraints,
    IReadOnlyList<string> AuthIndexes)
{
    public const string BaselineMigrationId = "20261015125000_InitialBaseline";
    public const string AuthIdentityMigrationId = "20261015223849_AuthIdentity";

    /// <summary>Migrations de l'assembly, dans l'ordre (phase 02).</summary>
    public static readonly string[] AllMigrationIds = [BaselineMigrationId, AuthIdentityMigrationId];

    /// <summary>Base vide, baseline seule, ou baseline + identité : aucun autre état n'est accepté.</summary>
    public IReadOnlyList<string> KnownStateDifferences(string role)
    {
        if (IsEmpty)
        {
            return [];
        }

        var baseline = BaselineDifferences(role);
        return baseline.Count == 0 ? baseline : AuthIdentityDifferences(role);
    }

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
        Expect(differences, "colonnes auth", AuthColumns, []);
        return differences;
    }

    /// <summary>
    /// Exactement l'état produit par 0001 puis 0002_AuthIdentity (docs/phase/02/sql/), objets possédés par
    /// <paramref name="role"/>. Les contraintes sont comparées par nom, type et règle de suppression ;
    /// leur texte SQL est relu dans le script approuvé.
    /// </summary>
    public IReadOnlyList<string> AuthIdentityDifferences(string role)
    {
        var differences = new List<string>();
        Expect(differences, "schémas", ExtraSchemas, ["auth"]);
        string[] authRelations =
        [
            "bootstrap_record kind=r", "pk_bootstrap_record kind=i",
            "users kind=r", "pk_users kind=i", "ux_users_normalized_user_name kind=i",
            "user_permissions kind=r", "pk_user_permissions kind=i",
            "sessions kind=r", "pk_sessions kind=i", "ix_sessions_user_id kind=i", "ux_sessions_token_hash kind=i",
        ];
        Expect(differences, "relations", Relations,
        [
            $"public.PK___EFMigrationsHistory kind=i owner={role} acl=default",
            $"public.__EFMigrationsHistory kind=r owner={role} acl=default",
            .. authRelations.Select(r => $"auth.{r} owner={role} acl=default"),
        ]);
        Expect(differences, "routines", Routines, []);
        Expect(differences, "types", Types, []);
        Expect(differences, "extensions", Extensions, []);
        Expect(differences, "décorations", Decorations, []);
        Expect(differences, "lignes", HistoryRows, [$"{BaselineMigrationId} 10.0.4", $"{AuthIdentityMigrationId} 10.0.4"]);
        Expect(differences, "colonnes auth", AuthColumns,
        [
            "bootstrap_record.administrator_id uuid notnull=t",
            "bootstrap_record.completed_at_utc timestamp with time zone notnull=t",
            "bootstrap_record.id smallint notnull=t",
            "sessions.created_at_utc timestamp with time zone notnull=t",
            "sessions.expires_at_utc timestamp with time zone notnull=t",
            "sessions.id uuid notnull=t",
            "sessions.last_seen_at_utc timestamp with time zone notnull=t",
            "sessions.revocation_reason character varying(32) notnull=f",
            "sessions.revoked_at_utc timestamp with time zone notnull=f",
            "sessions.token_hash bytea notnull=t",
            "sessions.user_id uuid notnull=t",
            "user_permissions.permission character varying(64) notnull=t",
            "user_permissions.user_id uuid notnull=t",
            "users.created_at_utc timestamp with time zone notnull=t",
            "users.display_name character varying(128) notnull=t",
            "users.failed_login_count integer notnull=t",
            "users.id uuid notnull=t",
            "users.is_active boolean notnull=t",
            "users.lockout_end_utc timestamp with time zone notnull=f",
            "users.normalized_user_name character varying(64) notnull=t",
            "users.password_hash text notnull=t",
            "users.user_name character varying(64) notnull=t",
        ]);
        string[] notNull =
        [
            "bootstrap_record.id", "bootstrap_record.administrator_id", "bootstrap_record.completed_at_utc",
            "sessions.id", "sessions.user_id", "sessions.token_hash", "sessions.created_at_utc",
            "sessions.last_seen_at_utc", "sessions.expires_at_utc",
            "user_permissions.user_id", "user_permissions.permission",
            "users.id", "users.user_name", "users.normalized_user_name", "users.display_name",
            "users.password_hash", "users.is_active", "users.failed_login_count", "users.created_at_utc",
        ];
        Expect(differences, "contraintes auth", AuthConstraints,
        [
            "bootstrap_record.ck_bootstrap_record_singleton c -",
            "bootstrap_record.pk_bootstrap_record p -",
            "sessions.ck_sessions_expiry_after_creation c -",
            "sessions.ck_sessions_token_hash_length c -",
            "sessions.fk_sessions_users f c",
            "sessions.pk_sessions p -",
            "user_permissions.ck_user_permissions_permission c -",
            "user_permissions.fk_user_permissions_users f c",
            "user_permissions.pk_user_permissions p -",
            "users.ck_users_failed_login_count c -",
            "users.pk_users p -",
            // PostgreSQL 18 : NOT NULL catalogués dans pg_constraint, noms générés <table>_<colonne>_not_null.
            .. notNull.Select(c => $"{c[..c.IndexOf('.', StringComparison.Ordinal)]}.{c.Replace('.', '_')}_not_null n -"),
        ]);
        Expect(differences, "index auth", AuthIndexes,
        [
            "ix_sessions_user_id unique=f",
            "pk_bootstrap_record unique=t",
            "pk_sessions unique=t",
            "pk_user_permissions unique=t",
            "pk_users unique=t",
            "ux_sessions_token_hash unique=t",
            "ux_users_normalized_user_name unique=t",
        ]);
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
        Section("lignes __EFMigrationsHistory", HistoryRows),
        Section("colonnes auth", AuthColumns),
        Section("contraintes auth", AuthConstraints),
        Section("index auth", AuthIndexes));

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
                : [],
            AuthColumns: await ListAsync(connection, """
                SELECT format('%s.%s %s notnull=%s', c.relname, a.attname, format_type(a.atttypid, a.atttypmod),
                              CASE WHEN a.attnotnull THEN 't' ELSE 'f' END)
                FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'auth' AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
                """, cancellationToken),
            AuthConstraints: await ListAsync(connection, """
                SELECT format('%s.%s %s %s', c.relname, k.conname, k.contype,
                              CASE WHEN k.contype = 'f' THEN k.confdeltype::text ELSE '-' END)
                FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'auth'
                """, cancellationToken),
            AuthIndexes: await ListAsync(connection, """
                SELECT format('%s unique=%s', ic.relname, CASE WHEN i.indisunique THEN 't' ELSE 'f' END)
                FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid JOIN pg_namespace n ON n.oid = ic.relnamespace
                WHERE n.nspname = 'auth'
                """, cancellationToken));
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
