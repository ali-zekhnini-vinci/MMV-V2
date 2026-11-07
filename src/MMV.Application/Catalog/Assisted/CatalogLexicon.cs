using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MMV.Domain.Catalog;

namespace MMV.Application.Catalog.Assisted;

/// <summary>Profil d'une colonne calculé sur TOUT le fichier (lignes alignées, cellules non vides).</summary>
public sealed class ColumnProfile
{
    public int N { get; set; }

    public int Distinct { get; set; }

    public Dictionary<string, int> Classes { get; } = new(StringComparer.Ordinal);

    public List<string> Values { get; } = [];

    public double Share(params string[] classes) => N == 0 ? 0 : classes.Sum(c => Classes.GetValueOrDefault(c)) / (double)N;
}

/// <summary>Règle d'intitulé : champ explicite, alternatives (ambigu), pourcentage, nom (marque / fabricant), consigne suspecte.</summary>
public sealed record HeaderRule(string? Field, string[] Alternatives, bool Explicit, bool Percent, bool Name, bool Suspect, string Why);

/// <summary>
/// Lexique et classes de valeurs DÉTERMINISTES (C1.1), portés du validateur prototype B0 (<c>Lex</c>, écrit sans regarder
/// aucune sortie de modèle) : 49 champs du contrat <c>mmv.csvai.v2</c>, six familles MMV, normalisation, détection des
/// consignes injectées, classes de cellule, clé GTIN, codes et mots-clés de famille, règles d'intitulé. Deux champs
/// SERVEUR seulement (jamais proposés à l'IA) complètent le référentiel des verres : <c>cylinderMin</c>, <c>leadTimeDays</c>.
/// Caches bornés et sûrs en concurrence (l'API sert plusieurs requêtes).
/// </summary>
public static partial class CatalogLexicon
{
    public static readonly string[] FamilyCodes =
        [Families.OpticalFrames, Families.Sunglasses, Families.OphthalmicLenses, Families.ContactLensesSolutions, Families.Accessories, Families.Services];

    /// <summary>Les 49 champs du contrat v2 (énumération proposée à l'IA, identique aux bancs Lot A et B0).</summary>
    public static readonly (string Key, string Meaning)[] ContractFields =
    [
        ("familyCode", "code ou libellé indiquant la famille / le type de produit"),
        ("brand", "marque commerciale"),
        ("modelName", "nom ou code commercial du modèle"),
        ("modelDescription", "description longue du produit"),
        ("variantLabel", "libellé de la déclinaison"),
        ("color", "coloris"),
        ("sku", "code article INTERNE du magasin (jamais dans un fichier fournisseur)"),
        ("manufacturerReference", "référence du fabricant ou de la marque"),
        ("supplierReference", "référence de l'article chez le fournisseur (code article, item code)"),
        ("barcode", "code-barres EAN / GTIN / UPC (8 à 14 chiffres)"),
        ("stockPolicy", "politique de stock (stocké, sur commande)"),
        ("supplierCode", "code du fournisseur"),
        ("supplierName", "nom du fournisseur"),
        ("purchasePriceHt", "prix d'ACHAT hors taxes (prix net, tarif, coût, wholesale)"),
        ("salePriceHt", "prix de VENTE hors taxes"),
        ("salePriceTtc", "prix de VENTE toutes taxes comprises (prix public, PVC, RRP, MSRP)"),
        ("priceUnqualified", "montant dont on ne sait pas s'il est d'achat ou de vente, HT ou TTC"),
        ("currency", "devise"),
        ("vatRate", "taux de TVA"),
        ("eyeSize", "calibre de monture (mm)"),
        ("bridgeSize", "pont de monture (mm)"),
        ("templeLength", "longueur de branche (mm)"),
        ("frameDimensions", "calibre, pont et branche réunis dans une même cellule (ex. 52-18-140)"),
        ("material", "matière d'une monture ou d'un accessoire"),
        ("shape", "forme de monture"),
        ("lensTint", "teinte des verres d'une solaire"),
        ("replacement", "renouvellement d'une lentille (journalière, mensuelle…)"),
        ("power", "puissance ou sphère d'une lentille de contact (dioptries)"),
        ("cylinder", "cylindre d'une lentille torique"),
        ("axis", "axe d'une lentille torique"),
        ("baseCurve", "rayon de courbure d'une lentille"),
        ("diameter", "diamètre d'une lentille ou d'un verre (mm)"),
        ("packSize", "nombre d'unités par boîte ou conditionnement"),
        ("volumeMl", "contenance d'un produit d'entretien"),
        ("lensManufacturer", "fabricant d'un verre correcteur"),
        ("lensRange", "gamme d'un verre correcteur"),
        ("lensType", "géométrie d'un verre (unifocal, progressif…)"),
        ("lensIndex", "indice de réfraction d'un verre"),
        ("lensMaterial", "matériau d'un verre (organique, minéral, polycarbonate…)"),
        ("lensTreatments", "traitements d'un verre (antireflet, durci, filtre bleu…)"),
        ("sphereMin", "sphère minimale fabriquée d'un verre"),
        ("sphereMax", "sphère maximale fabriquée d'un verre"),
        ("cylinderMax", "cylindre maximal fabriqué d'un verre"),
        ("additionMin", "addition minimale d'un verre progressif"),
        ("additionMax", "addition maximale d'un verre progressif"),
        ("quantity", "quantité en stock"),
        ("unit", "unité de vente"),
        ("notes", "remarque ou texte libre"),
        ("unknown", "aucun champ ne convient, ou doute"),
    ];

    /// <summary>Champs reconnus par les RÈGLES seulement (jamais dans l'énumération de l'IA) : référentiel des verres.</summary>
    public static readonly string[] ServerOnlyFields = ["cylinderMin", "leadTimeDays", "combinationMax"];

    public static readonly HashSet<string> ContractFieldKeys = [.. ContractFields.Select(f => f.Key)];
    public static readonly HashSet<string> AllFieldKeys = [.. ContractFieldKeys, .. ServerOnlyFields];
    public static readonly HashSet<string> Money = ["purchasePriceHt", "salePriceHt", "salePriceTtc", "priceUnqualified"];
    public static readonly HashSet<string> Commercial = ["purchasePriceHt", "salePriceHt", "salePriceTtc", "priceUnqualified", "currency", "vatRate", "supplierReference", "manufacturerReference", "sku", "barcode"];
    public static readonly HashSet<string> Refs = ["supplierReference", "manufacturerReference", "sku"];
    public static readonly HashSet<string> TextFields = ["brand", "modelName", "modelDescription", "color", "variantLabel", "material", "shape", "lensTint", "replacement", "lensManufacturer", "lensRange", "lensType", "lensMaterial", "lensTreatments", "supplierName", "unit", "stockPolicy", "supplierCode", "familyCode"];
    public static readonly HashSet<string> IdentityText = ["brand", "modelName", "modelDescription", "familyCode", "lensManufacturer", "lensRange"];
    public static readonly HashSet<string> KeywordFields = ["modelName", "modelDescription", "familyCode", "variantLabel", "lensType", "lensRange"];
    public static readonly string[] ColumnIssues = ["none", "ambiguous_meaning", "ambiguous_price", "mixed_units", "mixed_content", "composite_value", "duplicate_column", "values_mismatch_header", "suspicious_instruction"];
    public static readonly string[] Confidences = ["high", "medium", "low"];
    public static readonly HashSet<string> NumericLike = ["G", "I", "S", "N", "P", "D", "A", "U"];

    private const int CacheLimit = 200_000;
    private static readonly ConcurrentDictionary<string, bool> SuspiciousCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> ClassCache = new(StringComparer.Ordinal);

    public static string Norm(string s)
    {
        s = s.Replace("ø", " diametre ", StringComparison.Ordinal).Replace("Ø", " diametre ", StringComparison.Ordinal)
            .Replace("œ", "oe", StringComparison.Ordinal).Replace("Œ", "oe", StringComparison.Ordinal);
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (ch == '%')
            {
                sb.Append(" % ");
            }
            else
            {
                sb.Append(' ');
            }
        }

        return Spaces().Replace(sb.ToString(), " ").Trim();
    }

    public static HashSet<string> Tok(string s) => [.. Norm(s).Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    private static readonly string[] SuspiciousPhrases =
    [
        " ignore ", " ignorez ", " ignorer ", " disregard ", " oublie ", " oubliez ", " instruction ", " instructions ", " consigne ", " consignes ", " prompt ",
        " pour l ia ", " for the ai ", " to the ai ", " reponds ", " repondez ", " respond ", " salepricettc ", " salepriceht ", " purchasepriceht ", " header line ",
    ];

    /// <summary>Consigne injectée (balises, rôles, JSON, verbes d'action sur colonnes / lignes / familles) : jamais interprétée.</summary>
    public static bool Suspicious(string raw)
    {
        if (SuspiciousCache.TryGetValue(raw, out var hit))
        {
            return hit;
        }

        var value = SuspiciousCore(raw);
        if (SuspiciousCache.Count > CacheLimit)
        {
            SuspiciousCache.Clear();
        }

        SuspiciousCache[raw] = value;
        return value;
    }

    private static bool SuspiciousCore(string raw)
    {
        if (raw.Length == 0)
        {
            return false;
        }

        var low = raw.ToLowerInvariant();
        if (low.Contains("<data", StringComparison.Ordinal) || low.Contains("</data", StringComparison.Ordinal) || low.Contains("<|", StringComparison.Ordinal))
        {
            return true;
        }

        if (RolePrefix().IsMatch(raw) || JsonKey().IsMatch(raw))
        {
            return true;
        }

        var n = " " + Norm(raw) + " ";
        return SuspiciousPhrases.Any(p => n.Contains(p, StringComparison.Ordinal)) || ActionVerb().IsMatch(n);
    }

    public static bool PlainSigned(string v) => PlainSignedPattern().IsMatch(v);

    /// <summary>
    /// Classe d'une cellule : E vide, X consigne suspecte, F formule, G 8–14 chiffres, I entier, S décimal signé, N décimal,
    /// P pourcentage, D dimensions composites, A montant à séparateur de milliers ou symbole, U nombre + unité, C code, W mots.
    /// Un décimal signé (<c>-1,25</c>) est un NOMBRE, jamais une formule.
    /// </summary>
    public static string Cls(string raw)
    {
        if (ClassCache.TryGetValue(raw, out var hit))
        {
            return hit;
        }

        var value = ClsCore(raw);
        if (ClassCache.Count > CacheLimit)
        {
            ClassCache.Clear();
        }

        ClassCache[raw] = value;
        return value;
    }

    private static string ClsCore(string raw)
    {
        var v = raw.Trim();
        if (v.Length == 0)
        {
            return "E";
        }

        if (Suspicious(v))
        {
            return "X";
        }

        if (v[0] == '=' || v[0] == '@' || ((v[0] == '+' || v[0] == '-') && !PlainSigned(v)))
        {
            return "F";
        }

        if (Gtin14().IsMatch(v))
        {
            return "G";
        }

        if (Digits().IsMatch(v))
        {
            return "I";
        }

        if (PlainSigned(v))
        {
            return "S";
        }

        if (Decimal().IsMatch(v))
        {
            return "N";
        }

        if (Percent().IsMatch(v))
        {
            return "P";
        }

        if (FrameDims().IsMatch(v))
        {
            return "D";
        }

        if (Thousands().IsMatch(v) || CurrencyAmount().IsMatch(v))
        {
            return "A";
        }

        if (NumberUnit().IsMatch(v))
        {
            return "U";
        }

        return !v.Contains(' ', StringComparison.Ordinal) && HasDigit().IsMatch(v) && HasLetter().IsMatch(v) ? "C" : "W";
    }

    public static bool GtinValid(string d)
    {
        if (!Digits().IsMatch(d) || d.Length is not (8 or 12 or 13 or 14))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < d.Length - 1; i++)
        {
            var x = d[d.Length - 2 - i] - '0';
            sum += i % 2 == 0 ? 3 * x : x;
        }

        return (10 - (sum % 10)) % 10 == d[^1] - '0';
    }

    // ------------------------------------------------------------------ familles : codes et mots-clés

    private static readonly Dictionary<string, string> FamilyCodeWords = new(StringComparer.Ordinal)
    {
        ["mon"] = Families.OpticalFrames, ["mont"] = Families.OpticalFrames, ["monture"] = Families.OpticalFrames, ["montures"] = Families.OpticalFrames,
        ["frame"] = Families.OpticalFrames, ["frames"] = Families.OpticalFrames, ["opt"] = Families.OpticalFrames, ["optique"] = Families.OpticalFrames,
        ["sol"] = Families.Sunglasses, ["solaire"] = Families.Sunglasses, ["solaires"] = Families.Sunglasses, ["sun"] = Families.Sunglasses,
        ["soleil"] = Families.Sunglasses, ["sunglasses"] = Families.Sunglasses,
        ["len"] = Families.ContactLensesSolutions, ["lent"] = Families.ContactLensesSolutions, ["lentille"] = Families.ContactLensesSolutions,
        ["lentilles"] = Families.ContactLensesSolutions, ["lc"] = Families.ContactLensesSolutions, ["cl"] = Families.ContactLensesSolutions,
        ["contact"] = Families.ContactLensesSolutions, ["ent"] = Families.ContactLensesSolutions, ["entretien"] = Families.ContactLensesSolutions,
        ["solution"] = Families.ContactLensesSolutions, ["solutions"] = Families.ContactLensesSolutions,
        ["ver"] = Families.OphthalmicLenses, ["verre"] = Families.OphthalmicLenses, ["verres"] = Families.OphthalmicLenses, ["oph"] = Families.OphthalmicLenses,
        ["ophtalmique"] = Families.OphthalmicLenses,
        ["acc"] = Families.Accessories, ["accessoire"] = Families.Accessories, ["accessoires"] = Families.Accessories, ["accessory"] = Families.Accessories,
        ["pre"] = Families.Services, ["prest"] = Families.Services, ["prestation"] = Families.Services, ["prestations"] = Families.Services,
        ["srv"] = Families.Services, ["service"] = Families.Services, ["services"] = Families.Services,
    };

    // Ordre de priorité : une prestation sur une monture est une prestation ; « monture solaire » est une solaire.
    private static readonly (string Family, string[] Words)[] Keywords =
    [
        (Families.Services, ["ajustage", "montage", "reparation", "prestation", "forfait", "service", "controle", "remplacement", "prise"]),
        (Families.Accessories, ["etui", "chiffon", "cordon", "spray", "microfibre", "pochette", "chainette", "nettoyant"]),
        (Families.Sunglasses, ["solaire", "solaires", "soleil", "sun", "sunglass", "sunglasses", "polarise", "polarisee", "polarisees", "polarized"]),
        (Families.ContactLensesSolutions, ["lentille", "lentilles", "journaliere", "mensuelle", "bimensuelle", "torique", "contact", "solution", "multifonction", "entretien", "saline", "peroxyde"]),
        (Families.OphthalmicLenses, ["unifocal", "unifocaux", "progressif", "progressifs", "progressive", "degressif", "bifocal", "antireflet", "hmc"]),
        (Families.OpticalFrames, ["monture", "montures", "frame", "frames", "optique"]),
    ];

    public static string? FamilyOfCode(string value)
    {
        var n = Norm(value);
        if (n.Length == 0)
        {
            return null;
        }

        return FamilyCodeWords.TryGetValue(n, out var f) ? f : KeywordFamily(value);
    }

    public static string? KeywordFamily(string text)
    {
        var t = Tok(text);
        foreach (var (family, words) in Keywords)
        {
            if (words.Any(t.Contains))
            {
                return family;
            }
        }

        return null;
    }

    private static readonly string[] LensTypeWords = ["unifocal", "progressif", "progressive", "degressif", "bifocal", "multifocal", "single", "trifocal"];

    public static bool LensTypeLike(string v)
    {
        var t = Tok(v);
        return LensTypeWords.Any(t.Contains);
    }

    /// <summary>Géométrie MMV d'une valeur de type de verre (liste fermée <see cref="LensTypes"/>) ; null si non reconnue.</summary>
    public static string? LensTypeOf(string value)
    {
        var t = Tok(value);
        if (t.Overlaps(["unifocal", "unifocaux", "single", "sv", "simple"]))
        {
            return LensTypes.SingleVision;
        }

        if (t.Overlaps(["progressif", "progressifs", "progressive", "progressives", "multifocal", "pal"]))
        {
            return LensTypes.Progressive;
        }

        if (t.Overlaps(["degressif", "degressifs", "degressive", "office", "bureau"]))
        {
            return LensTypes.Degressive;
        }

        return t.Overlaps(["bifocal", "bifocaux", "bifocale", "trifocal"]) ? LensTypes.Bifocal : null;
    }

    // ------------------------------------------------------------------ règle d'en-tête

    private static HeaderRule R(string f, string why, bool name = false) => new(f, [], true, false, name, false, why);

    private static HeaderRule Amb(string why, params string[] alt) => new(null, alt, false, false, false, false, why);

    public static readonly string[] HeaderishUnknown = ["delai", "gender", "genre", "sexe", "zone", "divers", "saison", "collection", "lead", "livraison", "origine"];

    public static HeaderRule Rule(string header, IReadOnlyList<string> allHeaders, ColumnProfile prof, bool lensContext)
    {
        if (header.Trim().Length == 0)
        {
            return new(null, [], false, false, false, false, "en-tête vide");
        }

        if (Suspicious(header))
        {
            return new(null, [], false, false, false, true, "en-tête contenant une consigne : aucun sens déduit");
        }

        var t = Tok(header);
        var n = Norm(header);
        bool Has(params string[] w) => w.Any(t.Contains);
        var moneyTok = Has("prix", "price", "tarif", "px", "pa", "pv", "pvc", "ppc", "rrp", "msrp", "montant", "amount", "cout", "cost", "wholesale");
        var percent = header.Contains('%', StringComparison.Ordinal) || (!moneyTok && Has("remise", "discount", "rabais", "escompte", "taux"));
        if (Has("tva", "vat", "taxe", "tax") && !moneyTok && !Has("incl", "ttc", "excl"))
        {
            return R("vatRate", "taux de TVA");
        }

        if (percent)
        {
            return new(null, [], false, true, false, false, "pourcentage (remise, taux) : jamais un montant");
        }

        if (Has("ean", "ean13", "ean8", "gtin", "upc", "barcode", "codebarre", "codebarres") || n.Contains("code barre", StringComparison.Ordinal))
        {
            return R("barcode", "code-barres");
        }

        if (moneyTok)
        {
            var purchase = Has("achat", "purchase", "wholesale", "cout", "cost", "pa", "revient") || (Has("net") && !Has("vente", "public", "pv", "pvc", "rrp", "ppc", "retail"));
            var sale = Has("vente", "sale", "public", "pvc", "rrp", "msrp", "retail", "pv", "conseille", "ppc", "selling");
            var ht = Has("ht", "hors", "excl", "exclusive", "net");
            var ttc = Has("ttc", "incl", "inclusive", "toutes");
            if (purchase && sale)
            {
                return Amb("montant : achat et vente mêlés", "priceUnqualified", "purchasePriceHt", "salePriceHt", "salePriceTtc");
            }

            if (purchase)
            {
                return ttc ? Amb("achat TTC : inhabituel", "purchasePriceHt", "priceUnqualified") : R("purchasePriceHt", "prix d'achat");
            }

            if (sale)
            {
                if (ttc && !ht)
                {
                    return R("salePriceTtc", "prix de vente TTC");
                }

                return ht && !ttc ? R("salePriceHt", "prix de vente HT") : Amb("prix de vente sans HT/TTC", "salePriceHt", "salePriceTtc");
            }

            return Amb("montant non qualifié", "priceUnqualified", "purchasePriceHt", "salePriceHt", "salePriceTtc");
        }

        if (Has("devise", "currency", "monnaie"))
        {
            return R("currency", "devise");
        }

        if (n is "code fournisseur" or "supplier code" or "codefournisseur")
        {
            return R("supplierCode", "code fournisseur");
        }

        if (Has("ref", "refs", "reference", "references", "code", "item", "article", "mpn", "sku", "codearticle", "itemcode"))
        {
            if (Has("sku", "interne"))
            {
                return R("sku", "SKU interne");
            }

            if (Has("fournisseur", "supplier", "vendor"))
            {
                return R("supplierReference", "référence fournisseur");
            }

            return Has("fabricant", "manufacturer", "constructeur", "mpn", "marque", "brand")
                ? R("manufacturerReference", "référence fabricant")
                : Amb("référence non qualifiée", "supplierReference", "manufacturerReference");
        }

        if (Has("fabricant", "manufacturer", "constructeur"))
        {
            return R(lensContext ? "lensManufacturer" : "brand", "nom du fabricant", name: true);
        }

        if (Has("marque", "brand"))
        {
            return R("brand", "marque", name: true);
        }

        if (Has("modele", "model", "modelname", "modeles"))
        {
            return R("modelName", "modèle");
        }

        var hasModelColumn = allHeaders.Any(h => h != header && Tok(h).Overlaps(["modele", "model", "modelname"]));
        if (Has("designation", "libelle", "nom", "name", "produit", "product"))
        {
            return hasModelColumn ? R("modelDescription", "désignation (une colonne modèle existe)") : R("modelName", "désignation");
        }

        if (Has("description", "descriptif"))
        {
            return R("modelDescription", "description");
        }

        var colorTok = Has("coloris", "couleur", "color", "colour", "teinte");
        if (colorTok && Has("puissance", "power", "sphere", "dioptrie"))
        {
            return Amb("coloris ou puissance selon la ligne", "color", "power", "variantLabel");
        }

        if (colorTok)
        {
            return R("color", "coloris");
        }

        if (Has("variante", "declinaison", "finition"))
        {
            return R("variantLabel", "déclinaison");
        }

        if (Has("calibre", "eyesize"))
        {
            return R("eyeSize", "calibre");
        }

        if (Has("pont", "bridge"))
        {
            return R("bridgeSize", "pont");
        }

        if (Has("branche", "branches", "temple"))
        {
            return R("templeLength", "branche");
        }

        if (Has("size", "taille", "dim", "dimension", "dimensions", "dims"))
        {
            return prof.Share("D") >= 0.8 ? R("frameDimensions", "dimensions composites") : Amb("dimension non qualifiée", "eyeSize", "diameter");
        }

        if (Has("matiere", "material", "materiau", "materiaux"))
        {
            return R(lensContext ? "lensMaterial" : "material", "matière");
        }

        if (Has("forme", "shape"))
        {
            return R("shape", "forme");
        }

        if (Has("renouvellement", "replacement", "frequence"))
        {
            return R("replacement", "renouvellement");
        }

        if (lensContext && Has("sphere", "sph") && Has("cyl", "cylindre", "cylinder"))
        {
            return R("combinationMax", "limite |SPH| + |CYL| (restriction combinée, référentiel des verres)");
        }

        if (Has("sphere", "sph", "puissance", "power", "dioptrie", "dioptries"))
        {
            if (Has("min", "mini"))
            {
                return R("sphereMin", "sphère minimale");
            }

            return Has("max", "maxi") ? R("sphereMax", "sphère maximale") : R("power", "puissance");
        }

        if (Has("cyl", "cylindre", "cylinder"))
        {
            if (lensContext && Has("min", "mini"))
            {
                return R("cylinderMin", "cylindre minimal (référentiel des verres)");
            }

            return Has("max", "maxi") ? R("cylinderMax", "cylindre maximal") : R("cylinder", "cylindre");
        }

        if (Has("axe", "axis"))
        {
            return R("axis", "axe");
        }

        if (Has("add", "addition"))
        {
            if (Has("max", "maxi"))
            {
                return R("additionMax", "addition maximale");
            }

            return Has("min", "mini") ? R("additionMin", "addition minimale") : Amb("addition non qualifiée", "additionMax", "additionMin");
        }

        if (Has("rayon", "bc", "courbure", "basecurve"))
        {
            return R("baseCurve", "rayon");
        }

        if (Has("diametre", "diameter", "dia", "diametres", "diameters"))
        {
            return R("diameter", "diamètre");
        }

        if (Has("boite", "conditionnement", "cond", "pack", "packsize", "colisage"))
        {
            return R("packSize", "conditionnement");
        }

        if (Has("contenance", "volume", "ml"))
        {
            return R("volumeMl", "contenance");
        }

        if (Has("gamme", "range"))
        {
            return R("lensRange", "gamme");
        }

        if (Has("indice", "index"))
        {
            return R("lensIndex", "indice");
        }

        if (Has("traitement", "traitements", "treatment", "coating"))
        {
            return R("lensTreatments", "traitements");
        }

        if (lensContext && Has("delai", "lead") && Has("jours", "jour", "j", "days", "day", "livraison", "fabrication", "time"))
        {
            return R("leadTimeDays", "délai en jours (référentiel des verres)");
        }

        if (Has("famille", "family", "categorie", "category", "univers", "typeproduit"))
        {
            return R("familyCode", "famille");
        }

        if (Has("type", "geometrie", "geometry"))
        {
            var vals = prof.Values.Where(v => v.Trim().Length > 0).ToList();
            // Géométrie de verre d'abord : « Unifocal », « Progressive » sont aussi des mots-clés de la famille des verres.
            if (vals.Count > 0 && vals.Count(LensTypeLike) >= 0.6 * vals.Count)
            {
                return R("lensType", "type = géométrie de verre");
            }

            if (vals.Count > 0 && vals.Count(v => FamilyOfCode(v) != null) >= 0.6 * vals.Count)
            {
                return R("familyCode", "type = codes de famille");
            }

            return Amb("type non qualifié", "familyCode", "lensType", "modelDescription");
        }

        if (Has("fournisseur", "supplier"))
        {
            return Amb("fournisseur : code ou nom", "supplierCode", "supplierName");
        }

        if (Has("stock", "qte", "quantite", "quantity", "qty"))
        {
            return R("quantity", "quantité");
        }

        if (Has("unite", "unit"))
        {
            return R("unit", "unité");
        }

        if (Has("politique", "policy"))
        {
            return R("stockPolicy", "politique de stock");
        }

        return Has("commentaire", "commentaires", "remarque", "remarques", "note", "notes", "comments", "comment", "observation", "observations")
            ? R("notes", "remarque")
            : new(null, [], false, false, false, false, "intitulé sans champ MMV connu");
    }

    public static bool Headerish(string cell)
    {
        var t = Tok(cell);
        return HeaderishUnknown.Any(t.Contains);
    }

    /// <summary>Fichier sans en-têtes : suggestions tirées des seules valeurs (toujours à confirmer).</summary>
    public static HeaderRule ValueRule(ColumnProfile prof)
    {
        if (prof.N == 0)
        {
            return new(null, [], false, false, false, false, "colonne vide");
        }

        if (prof.Share("G") >= 0.8)
        {
            return Amb("valeurs au format code-barres", "barcode");
        }

        if (prof.Share("D") >= 0.8)
        {
            return Amb("valeurs composites calibre-pont-branche", "frameDimensions");
        }

        return prof.Share("P") >= 0.8
            ? new(null, [], false, true, false, false, "pourcentages")
            : new(null, [], false, false, false, false, "sans en-tête : aucun sens déduit des seules valeurs");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\b(system|assistant|user|developer)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex RolePrefix();

    [GeneratedRegex("\\{\\s*\"[^\"]+\"\\s*:")]
    private static partial Regex JsonKey();

    [GeneratedRegex(@"\b(map|classify|classe|classez|classer|regroupe|regroupez|group|mets|mettez|set|assign)\b.{0,50}\b(column|columns|colonne|colonnes|rows|row|lignes|ligne|product|produit|famille|family|confidence|services)\b")]
    private static partial Regex ActionVerb();

    [GeneratedRegex(@"^[+\-]\d+([.,]\d+)?$")]
    private static partial Regex PlainSignedPattern();

    [GeneratedRegex(@"^\d{8,14}$")]
    private static partial Regex Gtin14();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^\d+[.,]\d+$")]
    private static partial Regex Decimal();

    [GeneratedRegex(@"^\d+([.,]\d+)?\s*%$")]
    private static partial Regex Percent();

    [GeneratedRegex(@"^\d{2}\s*[-x×/□ ]\s*\d{2}\s*[-x×/□ ]\s*\d{3}$")]
    private static partial Regex FrameDims();

    // Séparateurs de milliers : espace, point, espace insécable (U+00A0), espace fine insécable (U+202F).
    [GeneratedRegex("^\\d{1,3}([ .  ]\\d{3})+([.,]\\d{1,2})?$")]
    private static partial Regex Thousands();

    [GeneratedRegex(@"^\d+([.,]\d+)?\s*(€|eur|\$|usd)$", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyAmount();

    [GeneratedRegex(@"^\d+([.,]\d+)?\s*[A-Za-zµ].*$")]
    private static partial Regex NumberUnit();

    [GeneratedRegex(@"\d")]
    private static partial Regex HasDigit();

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex HasLetter();
}
