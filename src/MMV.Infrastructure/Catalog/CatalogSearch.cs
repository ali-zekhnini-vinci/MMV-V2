using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MMV.Infrastructure.Persistence;
using Npgsql;

namespace MMV.Infrastructure.Catalog;

/// <summary>
/// Recherche serveur harmonisée des listes de la Phase 05 (COR-05-07), même principe que la recherche Clients :
/// insensible à la casse et aux accents, espaces superflus ignorés, CHAQUE mot doit apparaître (ET), sur l'ensemble des
/// données autorisées (jamais la seule page chargée). Un mot correspond aussi dans la forme « compacte » du texte
/// (espaces, tirets, points, barres obliques, soulignés et apostrophes retirés) : « rayban » trouve « Ray-Ban »,
/// « RX 5228 » trouve « RX5228 ». Aucune colonne ni extension nouvelle : <c>translate</c> et <c>lower</c> natifs,
/// mots transmis en paramètres (jamais concaténés), motifs LIKE échappés.
/// </summary>
internal static class CatalogSearch
{
    public const int MaxWords = 6;

    // Lettres accentuées → lettre de base (majuscules incluses : lower() d'une base en locale C ne traite que l'ASCII).
    private const string Accented = "ÀÂÄÁÃÅàâäáãåÇçÉÈÊËéèêëÍÌÎÏíìîïÑñÓÒÔÖÕóòôöõÚÙÛÜúùûüÝýÿŒÆ";
    private const string Plain = "aaaaaaaaaaaacceeeeeeeeiiiiiiiinnoooooooooouuuuuuuuyyyœæ";
    private const string CompactChars = " -./_'";

    /// <summary>Mots normalisés de la saisie (mêmes règles que <see cref="FoldSql"/>), au plus <see cref="MaxWords"/>.</summary>
    public static IReadOnlyList<string> Words(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : [.. Fold(search).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Take(MaxWords)];

    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static string Compact(string word) => new([.. word.Where(c => !CompactChars.Contains(c, StringComparison.Ordinal))]);

    private static string FoldSql(string expression) => $"lower(translate({expression}, '{Accented}', '{Plain}'))";

    private static string Like(string value) => "%" + value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal) + "%";

    /// <summary>
    /// Identifiants des variantes dont le document de recherche contient chaque mot : SKU, libellé, modèle, marque,
    /// code-barres, référence fabricant, valeurs des caractéristiques (modèle et variante, ex. coloris) et références
    /// fournisseur des liens actifs.
    /// </summary>
    public static IQueryable<Guid> VariantIds(MmvDbContext context, IReadOnlyList<string> words)
    {
        const string document = """
            concat_ws(' ', v.sku, v.label, m.name, m.brand, v.barcode, v.manufacturer_reference,
                (SELECT string_agg(a.value, ' ') FROM jsonb_each_text(v.attributes) a),
                (SELECT string_agg(a.value, ' ') FROM jsonb_each_text(m.attributes) a),
                (SELECT string_agg(l.supplier_reference, ' ') FROM catalog.variant_suppliers l WHERE l.variant_id = v.id AND l.removed_at_utc IS NULL))
            """;
        return Query(context, words, $"""
            SELECT v.id AS "Value"
            FROM catalog.product_variants v
            JOIN catalog.product_models m ON m.id = v.model_id
            CROSS JOIN LATERAL (SELECT {FoldSql(document)} AS d) s
            CROSS JOIN LATERAL (SELECT translate(s.d, '{CompactChars.Replace("'", "''", StringComparison.Ordinal)}', '') AS c) k
            """);
    }

    /// <summary>Fournisseurs : code, nom, ville, contact et références fournisseur de leurs liens actifs.</summary>
    public static IQueryable<Guid> SupplierIds(MmvDbContext context, IReadOnlyList<string> words)
    {
        const string document = """
            concat_ws(' ', f.code, f.name, f.city, f.contact_name,
                (SELECT string_agg(l.supplier_reference, ' ') FROM catalog.variant_suppliers l WHERE l.supplier_id = f.id AND l.removed_at_utc IS NULL))
            """;
        return Query(context, words, $"""
            SELECT f.id AS "Value"
            FROM catalog.suppliers f
            CROSS JOIN LATERAL (SELECT {FoldSql(document)} AS d) s
            CROSS JOIN LATERAL (SELECT translate(s.d, '{CompactChars.Replace("'", "''", StringComparison.Ordinal)}', '') AS c) k
            """);
    }

    private static IQueryable<Guid> Query(MmvDbContext context, IReadOnlyList<string> words, string from)
    {
        var conditions = new List<string>();
        var parameters = new List<object>();
        for (var i = 0; i < words.Count; i++)
        {
            var compact = Compact(words[i]);
            parameters.Add(new NpgsqlParameter($"w{i}", Like(words[i])));
            if (compact.Length > 0 && compact != words[i])
            {
                parameters.Add(new NpgsqlParameter($"c{i}", Like(compact)));
                conditions.Add($@"(s.d LIKE @w{i} ESCAPE '\' OR k.c LIKE @c{i} ESCAPE '\')");
            }
            else
            {
                conditions.Add($@"(s.d LIKE @w{i} ESCAPE '\' OR k.c LIKE @w{i} ESCAPE '\')");
            }
        }

        var sql = from + (conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions));
        return context.Database.SqlQueryRaw<Guid>(sql, [.. parameters]);
    }
}
