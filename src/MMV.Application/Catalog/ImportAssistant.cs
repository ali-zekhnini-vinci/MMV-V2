using System.Globalization;
using System.Text.RegularExpressions;
using MMV.Domain.CsvImport;

namespace MMV.Application.Catalog;

/// <summary>Modèles d'import fournisseur (migration 0008).</summary>
public interface IImportProfileStore
{
    Task<SupplierImportProfile?> FindAsync(Guid supplierId, string kind, CancellationToken cancellationToken);

    void Add(SupplierImportProfile profile);
}

/// <summary>Degré de confiance d'une correspondance proposée.</summary>
public static class MatchConfidence
{
    /// <summary>Reprise du modèle enregistré pour ce fournisseur.</summary>
    public const string Profile = "profile";

    /// <summary>Intitulé reconnu tel quel (synonyme exact).</summary>
    public const string Exact = "exact";

    /// <summary>Intitulé contenant un synonyme : proposé, à vérifier.</summary>
    public const string Probable = "probable";

    /// <summary>Intitulé ambigu (ex. « Prix », « Ref » sans contexte) : jamais appliqué sans confirmation.</summary>
    public const string Ambiguous = "ambiguous";
}

/// <summary>Proposition pour un champ MMV : colonne, confiance, autres champs possibles pour une colonne ambiguë.</summary>
public sealed record MappingSuggestion(string Field, string Header, string Confidence, IReadOnlyList<string> Alternatives);

/// <summary>Options d'import réservées dans la correspondance (préfixe « @ », jamais une colonne).</summary>
public static class ImportOptions
{
    public const string Prefix = "@";

    /// <summary>Séparateur décimal confirmé par l'utilisateur : <c>,</c> ou <c>.</c>.</summary>
    public const string DecimalSeparator = "@decimal";

    /// <summary>Valeurs par défaut CHOISIES par l'utilisateur pour tout le fichier (jamais inventées).</summary>
    public const string FamilyCode = "@familyCode";

    public const string StockPolicy = "@stockPolicy";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal) { DecimalSeparator, FamilyCode, StockPolicy };

    public static bool IsOption(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);
}

/// <summary>
/// Correspondance automatique colonnes → champs (COR-05-04) SANS intelligence artificielle ni service tiers : règles,
/// normalisation des intitulés (casse, accents, espaces et ponctuation ignorés), dictionnaire de synonymes français /
/// anglais, contexte (fournisseur connu ⇒ « Ref » = référence fournisseur, sinon référence fabricant) et modèle
/// fournisseur enregistré. Point d'extension futur documenté : un assistant LOCAL pourrait proposer des
/// correspondances supplémentaires, toujours soumises à la même confirmation — aucune dépendance aujourd'hui.
/// </summary>
public static class ImportMapper
{
    /// <summary>Synonymes normalisés (voir <see cref="CsvTable.NormalizeHeader"/>) par type d'import et champ.</summary>
    private static readonly Dictionary<string, Dictionary<string, string[]>> Synonyms = new(StringComparer.Ordinal)
    {
        [ImportKinds.Catalog] = new(StringComparer.Ordinal)
        {
            ["familyCode"] = ["famille", "family", "familycode", "categorie", "category", "typeproduit", "producttype"],
            ["brand"] = ["marque", "brand", "fabricantmonture", "brandname"],
            ["modelName"] = ["modele", "model", "modelname", "nommodele", "nomdumodele", "productname", "nomproduit", "designation", "nom", "name"],
            ["modelDescription"] = ["description", "descriptionmodele", "descriptif"],
            ["variantLabel"] = ["variante", "libellevariante", "variantlabel", "declinaison", "libelle", "label", "finition"],
            ["color"] = ["couleur", "coloris", "color", "colour", "teinte", "colorname"],
            ["sku"] = ["sku", "referenceinterne", "refinterne", "codeinterne"],
            ["manufacturerReference"] = ["referencefabricant", "reffabricant", "manufacturerreference", "mpn", "refconstructeur", "referenceconstructeur"],
            ["barcode"] = ["codebarres", "codebarre", "ean", "ean13", "gtin", "upc", "barcode", "codeean"],
            ["stockPolicy"] = ["politique", "politiquestock", "stockpolicy", "gestionstock"],
            ["lensManufacturer"] = ["fabricantverre", "lensmanufacturer"],
            ["lensRange"] = ["gamme", "lensrange", "gammeverre"],
            ["lensIndex"] = ["indice", "lensindex", "index"],
            ["lensTreatments"] = ["traitements", "lenstreatments", "traitement"],
            ["supplierCode"] = ["codefournisseur", "fournisseur", "suppliercode", "supplier"],
            ["supplierReference"] = ["referencefournisseur", "reffournisseur", "supplierreference", "supplierref", "refsupplier", "vendorreference"],
            ["primarySupplier"] = ["principal", "fournisseurprincipal", "primarysupplier"],
        },
        [ImportKinds.Suppliers] = new(StringComparer.Ordinal)
        {
            ["code"] = ["code", "codefournisseur", "suppliercode"],
            ["name"] = ["nom", "name", "raisonsociale", "fournisseur", "suppliername", "companyname"],
            ["countryCode"] = ["pays", "country", "countrycode", "codepays"],
            ["contactName"] = ["contact", "contactname", "interlocuteur"],
            ["email"] = ["email", "courriel", "mail", "emailaddress"],
            ["phone"] = ["telephone", "tel", "phone", "phonenumber"],
            ["addressLine"] = ["adresse", "address", "addressline", "rue"],
            ["postalCode"] = ["codepostal", "cp", "postalcode", "zipcode", "zip"],
            ["city"] = ["ville", "city", "localite"],
            ["notes"] = ["notes", "remarques", "commentaire", "comments"],
        },
        [ImportKinds.Prices] = new(StringComparer.Ordinal)
        {
            ["sku"] = ["sku", "referenceinterne", "refinterne", "codeinterne"],
            ["supplierReference"] = ["referencefournisseur", "reffournisseur", "supplierreference", "supplierref", "refsupplier", "vendorreference"],
            ["purchasePriceHt"] = ["prixachatht", "prixachat", "prixdachatht", "prixdachat", "paht", "pa", "purchasepriceht", "purchaseprice", "prixnetht", "prixnet",
                "netprice", "costprice", "cost", "tarifachat", "achatht"],
            ["coefficient"] = ["coefficient", "coef", "coeff", "multiplicateur"],
            ["salePriceHt"] = ["prixventeht", "prixvente", "prixdeventeht", "pvht", "pv", "salepriceht", "saleprice", "retailprice", "prixpublicht", "ppht"],
            ["currency"] = ["devise", "currency", "monnaie"],
        },
    };

    /// <summary>Intitulés ambigus par type : champs possibles, jamais appliqués sans confirmation.</summary>
    private static readonly Dictionary<string, Dictionary<string, string[]>> Ambiguous = new(StringComparer.Ordinal)
    {
        [ImportKinds.Catalog] = new(StringComparer.Ordinal)
        {
            ["ref"] = ["supplierReference", "manufacturerReference", "sku"],
            ["reference"] = ["supplierReference", "manufacturerReference", "sku"],
            ["productcode"] = ["supplierReference", "manufacturerReference", "sku"],
            ["codeproduit"] = ["supplierReference", "manufacturerReference", "sku"],
            ["codearticle"] = ["supplierReference", "manufacturerReference", "sku"],
            ["article"] = ["supplierReference", "manufacturerReference", "modelName"],
            ["itemcode"] = ["supplierReference", "manufacturerReference", "sku"],
            ["refarticle"] = ["supplierReference", "manufacturerReference", "sku"],
        },
        [ImportKinds.Prices] = new(StringComparer.Ordinal)
        {
            ["prix"] = ["purchasePriceHt", "salePriceHt"],
            ["price"] = ["purchasePriceHt", "salePriceHt"],
            ["tarif"] = ["purchasePriceHt", "salePriceHt"],
            ["prixht"] = ["purchasePriceHt", "salePriceHt"],
            ["ref"] = ["supplierReference", "sku"],
            ["reference"] = ["supplierReference", "sku"],
            ["productcode"] = ["supplierReference", "sku"],
            ["codearticle"] = ["supplierReference", "sku"],
        },
        [ImportKinds.Suppliers] = new(StringComparer.Ordinal),
    };

    /// <summary>
    /// Propositions pour les en-têtes du fichier. Contexte <paramref name="supplierKnown"/> : un intitulé « Ref » d'un
    /// fichier fournisseur désigne d'abord la référence du fournisseur (proposée « probable »). <paramref name="profile"/> :
    /// correspondance validée d'un modèle fournisseur (appliquée si la colonne existe encore).
    /// </summary>
    public static IReadOnlyList<MappingSuggestion> Suggest(string kind, IReadOnlyList<string> headers, bool supplierKnown, IReadOnlyDictionary<string, string>? profile)
    {
        var fields = ImportService.Fields[kind].Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var synonyms = Synonyms[kind];
        var usedHeaders = new HashSet<string>(StringComparer.Ordinal);
        var result = new Dictionary<string, MappingSuggestion>(StringComparer.Ordinal);

        void Take(string field, string header, string confidence, IReadOnlyList<string>? alternatives = null)
        {
            // Une même colonne peut servir de libellé de variante ET de coloris (cas usuel d'un fichier de montures).
            var reuse = field is "color" or "variantLabel" && result.Values.Any(r => r.Header == header && r.Field is "color" or "variantLabel");
            if (fields.Contains(field) && !result.ContainsKey(field) && (reuse || usedHeaders.Add(header)))
            {
                result[field] = new MappingSuggestion(field, header, confidence, alternatives ?? []);
            }
        }

        foreach (var (field, header) in profile ?? new Dictionary<string, string>())
        {
            if (!ImportOptions.IsOption(field) && headers.Contains(header, StringComparer.Ordinal))
            {
                Take(field, header, MatchConfidence.Profile);
            }
        }

        foreach (var header in headers)
        {
            var key = CsvTable.NormalizeHeader(header);
            foreach (var (field, aliases) in synonyms)
            {
                if (aliases.Contains(key, StringComparer.Ordinal))
                {
                    Take(field, header, MatchConfidence.Exact);
                }
            }
        }

        foreach (var header in headers.Where(h => !usedHeaders.Contains(h)))
        {
            var key = CsvTable.NormalizeHeader(header);
            if (Ambiguous[kind].TryGetValue(key, out var candidates))
            {
                var free = candidates.Where(c => !result.ContainsKey(c)).ToList();
                if (free.Count == 0)
                {
                    continue;
                }

                // Contexte : fichier d'un fournisseur connu ⇒ « Ref » = référence fournisseur (probable, à vérifier).
                if (supplierKnown && free[0] == "supplierReference")
                {
                    Take("supplierReference", header, MatchConfidence.Probable, free);
                }
                else
                {
                    Take(free[0], header, MatchConfidence.Ambiguous, free);
                }

                continue;
            }

            var probable = synonyms
                .Where(s => !result.ContainsKey(s.Key))
                .SelectMany(s => s.Value.Where(a => a.Length >= 4 && key.Contains(a, StringComparison.Ordinal)).Select(a => (Field: s.Key, Alias: a)))
                .OrderByDescending(x => x.Alias.Length)
                .FirstOrDefault();
            if (probable.Field is not null)
            {
                Take(probable.Field, header, MatchConfidence.Probable);
            }
        }

        return [.. result.Values.OrderBy(s => ImportService.Fields[kind].ToList().FindIndex(f => f.Key == s.Field))];
    }

    /// <summary>Correspondance appliquée d'office : tout sauf les propositions ambiguës.</summary>
    public static Dictionary<string, string> Applied(IReadOnlyList<MappingSuggestion> suggestions) =>
        suggestions.Where(s => s.Confidence != MatchConfidence.Ambiguous).ToDictionary(s => s.Field, s => s.Header, StringComparer.Ordinal);
}

/// <summary>
/// Lecture des montants d'un fichier fournisseur (COR-05-04) : point ou virgule décimale, séparateurs de milliers
/// (espace, espace insécable, point, virgule, apostrophe), symbole ou code de devise. Jamais de flottant : la sortie
/// est une chaîne décimale canonique (« 1234.50 »). Une valeur AMBIGUË (« 1,234 » : milliers ou décimales ?) est
/// refusée tant que le séparateur décimal n'est pas confirmé pour le fichier (<see cref="ImportOptions.DecimalSeparator"/>).
/// Devise différente du magasin : refus (aucune conversion).
/// </summary>
public static partial class NumberText
{
    public const string AmbiguousNumber = "ambiguous_number";
    public const string CurrencyMismatch = "currency_mismatch";

    private static readonly Dictionary<string, string> CurrencyTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["€"] = "EUR", ["EUR"] = "EUR", ["EURO"] = "EUR", ["EUROS"] = "EUR",
        ["MAD"] = "MAD", ["DH"] = "MAD", ["DHS"] = "MAD", ["DIRHAM"] = "MAD", ["DIRHAMS"] = "MAD",
        ["$"] = "USD", ["USD"] = "USD", ["£"] = "GBP", ["GBP"] = "GBP", ["CHF"] = "CHF",
    };

    /// <summary>Indice de séparateur décimal d'une valeur : ',', '.', null si indécidable (entier ou ambiguë).</summary>
    public static char? Evidence(string raw, out bool ambiguous)
    {
        ambiguous = false;
        var text = Strip(raw, out _);
        var lastComma = text.LastIndexOf(',');
        var lastDot = text.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            return lastComma > lastDot ? ',' : '.';
        }

        var position = Math.Max(lastComma, lastDot);
        if (position < 0)
        {
            return null;
        }

        var separator = text[position];
        if (text.Count(c => c == separator) > 1)
        {
            // « 1.234.567 » : uniquement des milliers, aucune décimale.
            return separator == ',' ? '.' : ',';
        }

        var decimals = text.Length - position - 1;
        if (decimals == 3)
        {
            ambiguous = true;
            return null;
        }

        return separator;
    }

    /// <summary>Séparateur décimal d'une colonne : unique indice cohérent, sinon null (aucun indice) ; conflit ⇒ ambigu.</summary>
    public static (char? Separator, bool Ambiguous) DetectColumn(IEnumerable<string> values)
    {
        var evidence = new HashSet<char>();
        var ambiguous = false;
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            if (Evidence(value, out var unsure) is { } separator)
            {
                evidence.Add(separator);
            }

            ambiguous |= unsure;
        }

        return evidence.Count == 1 ? (evidence.First(), false) : (null, ambiguous || evidence.Count > 1);
    }

    /// <summary>Normalisation d'un montant ; (valeur canonique, null) ou (null, code d'erreur borné).</summary>
    public static (string? Value, string? Error) Normalize(string raw, char? decimalSeparator, string? expectedCurrency)
    {
        var text = Strip(raw, out var currency);
        if (currency is not null && expectedCurrency is not null && !string.Equals(currency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return (null, CurrencyMismatch);
        }

        var ambiguous = false;
        var separator = decimalSeparator ?? Evidence(raw, out ambiguous);
        if (decimalSeparator is null && ambiguous)
        {
            return (null, AmbiguousNumber);
        }

        if (separator is { } dec)
        {
            var thousands = dec == ',' ? '.' : ',';
            text = text.Replace(thousands.ToString(), string.Empty, StringComparison.Ordinal).Replace(dec, '.');
        }
        else if (text.Contains(',', StringComparison.Ordinal) || text.Contains('.', StringComparison.Ordinal))
        {
            // Plusieurs séparateurs identiques sans décimales (« 1.234.567 ») : milliers seulement.
            text = text.Replace(",", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
        }

        return CanonicalPattern().IsMatch(text) ? (text, null) : (null, "invalid_format");
    }

    /// <summary>Retire espaces (y compris insécables), apostrophes de milliers et symbole / code de devise.</summary>
    private static string Strip(string raw, out string? currency)
    {
        currency = null;
        var text = raw.Trim();
        foreach (var (token, code) in CurrencyTokens.OrderByDescending(t => t.Key.Length))
        {
            if (text.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                currency = code;
                text = text[token.Length..];
                break;
            }

            if (text.EndsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                currency = code;
                text = text[..^token.Length];
                break;
            }
        }

        return new string([.. text.Where(c => !char.IsWhiteSpace(c) && c is not (' ' or ' ' or '\'' or '’'))]);
    }

    [GeneratedRegex("^-?[0-9]{1,12}(\\.[0-9]{1,6})?$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalPattern();

    /// <summary>Format canonique invariant d'un décimal (tests, rapports).</summary>
    public static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
