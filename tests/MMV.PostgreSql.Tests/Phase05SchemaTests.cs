using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Inventaire EXACT des cinq schémas créés par 0007_CatalogStockModule (Phase 05), relu des catalogues PostgreSQL en
/// session lecture seule : colonnes (type, nullabilité, défaut), contraintes (définition complète), index (définition),
/// séquences, fonctions (empreinte du corps), déclencheurs (définition), propriétaire et droits. Comparé à l'instantané
/// versionné <c>docs/phase/05/sql/0007_CatalogStockModule.schema-inventory.txt</c>, qui constitue aussi le diff de schéma
/// de la revue G05-SQL (0006 → 0007 n'ajoute que ces objets, plus la contrainte de permissions recréée).
/// Mise à jour volontaire : <c>MMV_UPDATE_PHASE05_SCHEMA=1</c>, puis relecture du diff.
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05SchemaTests(ITestOutputHelper output)
{
    public const string SnapshotRelativePath = "docs/phase/05/sql/0007_CatalogStockModule.schema-inventory.txt";

    private const string Schemas = "('catalog', 'imports', 'org', 'pricing', 'stock')";

    [Phase05Fact]
    public async Task Phase05Schemas_MatchTheReviewedInventory_AndPreviousStagesAreUnchanged()
    {
        var builder = new NpgsqlConnectionStringBuilder(Phase05Target.ConnectionString) { Options = "-c default_transaction_read_only=on" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        var role = builder.Username!;

        var state = await SchemaInspection.ReadAsync(connection, CancellationToken.None);
        Assert.Equal("on", state.TransactionReadOnly);
        var differences = state.CatalogStockModuleDifferences(role);
        Assert.True(differences.Count == 0, "Différences 0001–0007 : " + string.Join(" ; ", differences));

        var actual = string.Join("\n", await InventoryAsync(connection)) + "\n";
        var path = Path.Combine(FindRepositoryRoot(), SnapshotRelativePath);
        if (Environment.GetEnvironmentVariable("MMV_UPDATE_PHASE05_SCHEMA") == "1")
        {
            await File.WriteAllTextAsync(path, actual);
        }

        output.WriteLine($"{actual.Count(c => c == '\n')} ligne(s) d'inventaire sur {Phase05Target.Name}");
        Assert.True(File.Exists(path), $"Instantané absent : {SnapshotRelativePath}");
        Assert.Equal((await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n"), actual);
    }

    internal static async Task<List<string>> InventoryAsync(NpgsqlConnection connection)
    {
        var lines = new List<string>();
        async Task Section(string title, string sql)
        {
            lines.Add($"## {title}");
            var items = new List<string>();
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(reader.GetString(0));
            }

            lines.AddRange(items.Order(StringComparer.Ordinal));
        }

        await Section("relations (propriétaire = rôle applicatif)", $"""
            SELECT format('%s.%s kind=%s owner_is_db_owner=%s acl=%s', n.nspname, c.relname, c.relkind,
                          c.relowner = (SELECT datdba FROM pg_database WHERE datname = current_database()), coalesce(c.relacl::text, 'default'))
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname IN {Schemas}
            """);
        await Section("colonnes", $"""
            SELECT format('%s.%s.%s %s notnull=%s default=%s identity=%s generated=%s', n.nspname, c.relname, a.attname,
                          format_type(a.atttypid, a.atttypmod), a.attnotnull, coalesce(pg_get_expr(d.adbin, d.adrelid), '-'),
                          coalesce(nullif(a.attidentity, ''), '-'), coalesce(nullif(a.attgenerated, ''), '-'))
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE n.nspname IN {Schemas} AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
            """);
        await Section("contraintes", $"""
            SELECT format('%s.%s %s %s', n.nspname, c.relname, k.conname, pg_get_constraintdef(k.oid))
            FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN {Schemas} AND k.contype <> 'n'
            """);
        await Section("index", $"""
            SELECT pg_get_indexdef(i.indexrelid) FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid
            JOIN pg_namespace n ON n.oid = ic.relnamespace WHERE n.nspname IN {Schemas}
            """);
        await Section("séquences", $"""
            SELECT format('%s.%s type=%s start=%s increment=%s min=%s max=%s cycle=%s', n.nspname, c.relname, format_type(s.seqtypid, NULL),
                          s.seqstart, s.seqincrement, s.seqmin, s.seqmax, s.seqcycle)
            FROM pg_sequence s JOIN pg_class c ON c.oid = s.seqrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname IN {Schemas}
            """);
        await Section("fonctions (empreinte md5 du corps)", $"""
            SELECT format('%s.%s returns=%s md5=%s', n.nspname, p.proname, format_type(p.prorettype, NULL), md5(p.prosrc))
            FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname IN {Schemas}
            """);
        await Section("déclencheurs", $"""
            SELECT pg_get_triggerdef(t.oid) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN {Schemas} AND NOT t.tgisinternal
            """);
        await Section("données insérées par la migration", """
            SELECT format('catalog.families %s label=%s policies=%s sort=%s', code, label_fr, allowed_policies, sort_order) FROM catalog.families
            """);
        await Section("contrainte des permissions (recréée par 0007)", """
            SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_user_permissions_permission'
            """);
        return lines;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MMV.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Racine du dépôt (MMV.slnx) introuvable.");
    }
}
