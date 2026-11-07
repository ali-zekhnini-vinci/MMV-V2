using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;

namespace MMV.Application.Catalog.Assisted;

/// <summary>Statuts de validation (conception § 4) : seuls AMBIGU et INVALIDE bloquent.</summary>
public static class CatalogStatuses
{
    public const string Valid = "VALIDE";
    public const string Warning = "AVERTISSEMENT";
    public const string Ambiguous = "AMBIGU";
    public const string Invalid = "INVALIDE";
    public const string Unrecognized = "NON_RECONNU";

    public static int Severity(string s) => s switch { Valid => 0, Warning => 1, Unrecognized => 1, Ambiguous => 2, Invalid => 3, _ => 0 };

    public static bool Blocking(string s) => s is Ambiguous or Invalid;

    public static string Max(string a, string b) => Severity(b) > Severity(a) ? b : a;
}

/// <summary>Origine d'une interprétation (affichée, jamais déduite du texte).</summary>
public static class InterpretationOrigins
{
    public const string None = "none";
    public const string Rule = "rule";
    public const string RuleAndAi = "rule+ai";
    public const string Ai = "ai";
    public const string AiSuggestion = "ai-suggestion";
    public const string User = "user";
    public const string Target = "target";
    public const string Continuation = "continuation";
}

public sealed class ColumnDecision
{
    public int C { get; init; }

    public string Header { get; init; } = string.Empty;

    public string Field { get; set; } = "unknown";

    public string Status { get; set; } = CatalogStatuses.Unrecognized;

    public string Origin { get; set; } = InterpretationOrigins.None;

    public string? RuleField { get; set; }

    public string[] RuleAlternatives { get; set; } = [];

    public bool RuleExplicit { get; set; }

    public bool RuleName { get; set; }

    public string? AiField { get; set; }

    public string? AiConfidence { get; set; }

    public string? AiIssue { get; set; }

    public string? Suggestion { get; set; }

    public List<string> Reasons { get; } = [];

    public bool AiRejected { get; set; }

    public bool AiConflict { get; set; }

    public bool AiFlag { get; set; }

    public bool RuleOk { get; set; }

    public int? UserRevision { get; set; }

    public ColumnProfile Profile { get; init; } = new();

    public string RuleView => RuleField ?? (RuleAlternatives.Length > 0 ? RuleAlternatives[0] : Field);

    public bool Importable => !CatalogStatuses.Blocking(Status) && Status != CatalogStatuses.Unrecognized && Field != "unknown";
}

public sealed class CellDecision
{
    public int C { get; init; }

    public string Field { get; init; } = string.Empty;

    /// <summary>Valeur brute du FICHIER (jamais réécrite).</summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>Valeur contrôlée : brute, ou corrigée par l'utilisateur (<see cref="CorrectedRevision"/>).</summary>
    public string Input { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string Status { get; set; } = CatalogStatuses.Valid;

    public string? Reason { get; set; }

    public int SourceLine { get; set; }

    public int? CorrectedRevision { get; set; }
}

public sealed class RowDecision
{
    public int Line { get; init; }

    /// <summary><c>header</c>, <c>data</c>, <c>empty</c>, <c>comment</c>, <c>skipped</c> (ignorée volontairement, tracée).</summary>
    public string Kind { get; set; } = "data";

    public string Status { get; set; } = CatalogStatuses.Valid;

    public string? Family { get; set; }

    public string FamilyStatus { get; set; } = "-";

    public string FamilyOrigin { get; set; } = InterpretationOrigins.None;

    /// <summary>Classe de famille stable (preuves des RÈGLES seulement) : cible des confirmations groupées.</summary>
    public string? FamilyClass { get; set; }

    public string? FamilySuggestion { get; set; }

    public List<string> Reasons { get; } = [];

    public int Group { get; set; } = -1;

    public int? ContinuationOf { get; set; }

    public bool Structural { get; set; }

    public int? SkippedRevision { get; set; }

    public List<CellDecision> Cells { get; } = [];

    public bool FamilyAuto => Kind == "data" && FamilyStatus == CatalogStatuses.Valid && !Structural;

    public CellDecision? Cell(string field) => Cells.FirstOrDefault(c => c.Field == field);
}

public sealed class GroupDecision
{
    public int Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public List<int> Lines { get; } = [];

    public string Status { get; set; } = CatalogStatuses.Valid;

    public List<string> Reasons { get; } = [];
}

/// <summary>Résultat de la validation déterministe d'un document (une interprétation par révision).</summary>
public sealed class CatalogInterpretation
{
    public required CsvDocument Document { get; init; }

    public required string Target { get; init; }

    public string HeaderStatus { get; set; } = CatalogStatuses.Valid;

    public bool HeaderPresent { get; set; }

    public string HeaderRule { get; set; } = string.Empty;

    public string? HeaderAi { get; set; }

    public int? HeaderUserRevision { get; set; }

    public List<ColumnDecision> Columns { get; } = [];

    public List<RowDecision> Rows { get; } = [];

    public List<GroupDecision> Groups { get; } = [];

    public bool Injection { get; set; }

    public bool AiUsed { get; set; }

    public bool Applicable { get; set; }

    public int Confirmations { get; set; }

    public Dictionary<string, int> Blockers { get; } = new(StringComparer.Ordinal);

    public IEnumerable<RowDecision> DataRows => Rows.Where(r => r.Kind == "data");

    public static bool RowImportable(RowDecision r) => r.Kind == "data" && !CatalogStatuses.Blocking(r.Status);
}

/// <summary>
/// Validation métier DÉTERMINISTE (C1.3), portée du validateur prototype B0 (monotonie vérifiée sur 21 080 mutations) et
/// complétée de la couche des décisions de l'utilisateur. Trois couches recalculées à CHAQUE revalidation : décisions de
/// l'utilisateur (révision courante) &gt; règles &gt; proposition IA (dernière tentative terminée). Une proposition de l'IA
/// peut AJOUTER un doute (AMBIGU), jamais rendre importable ce que les règles seules (et l'utilisateur) ne rendent pas
/// importable (P1) ; <c>VALIDE</c> ne vient que d'une règle ou de l'utilisateur (P2) ; un désaccord règle / IA bloque
/// (P3) ; toute valeur est dérivée de la seule valeur brute de sa cellule, ou de la correction de l'utilisateur (P4).
/// </summary>
public static partial class CatalogInterpretationValidator
{
    private static readonly HashSet<string> FrameDims = ["eyeSize", "bridgeSize", "templeLength", "frameDimensions"];
    private static readonly HashSet<string> ContactFields = ["power", "baseCurve", "replacement", "cylinder", "axis"];
    private static readonly HashSet<string> OphthalmicFields =
        ["lensIndex", "sphereMin", "sphereMax", "cylinderMin", "cylinderMax", "additionMin", "additionMax", "lensRange", "lensManufacturer", "lensType", "lensMaterial", "lensTreatments", "leadTimeDays", "combinationMax"];

    public static HashSet<string>? FieldFamilies(string f) =>
        FrameDims.Contains(f) ? [Families.OpticalFrames, Families.Sunglasses] :
        f == "lensTint" ? [Families.Sunglasses] :
        ContactFields.Contains(f) ? [Families.ContactLensesSolutions] :
        OphthalmicFields.Contains(f) ? [Families.OphthalmicLenses] :
        f == "diameter" ? [Families.ContactLensesSolutions, Families.OphthalmicLenses] : null;

    private sealed record Hard(bool Violation, bool Soft, string Why);

    /// <summary>Contrôle d'un champ contre les valeurs de TOUTE la colonne : violation (refus) ou réserve (AMBIGU).</summary>
    public static (bool Violation, bool Soft, string Why) CheckColumn(string field, ColumnProfile profile, HeaderRule rule)
    {
        var h = Check(field, profile, rule);
        return (h.Violation, h.Soft, h.Why);
    }

    private static Hard Check(string f, ColumnProfile p, HeaderRule hr)
    {
        Hard V(string w) => new(true, false, w);
        Hard S(string w) => new(false, true, w);
        var ok = new Hard(false, false, string.Empty);
        if (p.N == 0)
        {
            return ok;
        }

        if (CatalogLexicon.Money.Contains(f))
        {
            if (hr.Percent || p.Share("P") >= 0.3)
            {
                return V("pourcentage, pas un montant");
            }

            if (p.Share("S") > 0)
            {
                return V("montants négatifs");
            }

            return p.Share("I", "N", "A") < 0.8 ? V("valeurs non monétaires") : ok;
        }

        switch (f)
        {
            case "barcode":
                return p.Share("G") < 0.8 ? V("valeurs hors format code-barres (8 à 14 chiffres)") : ok;
            case "supplierReference" or "manufacturerReference" or "sku":
                if (hr.Name)
                {
                    return V("un nom de marque ou de fabricant n'est pas une référence");
                }

                if (p.Share("G") >= 0.8)
                {
                    return S("valeurs au format code-barres");
                }

                if (p.N >= 4 && p.Distinct <= 2)
                {
                    return S("valeur répétée : nom plutôt que référence");
                }

                return p.Values.Count(v => v.Trim().Contains(' ', StringComparison.Ordinal) && CatalogLexicon.Cls(v) == "W") >= 0.6 * p.N ? S("libellés plutôt que références") : ok;
            case "eyeSize" or "bridgeSize" or "templeLength":
            {
                var (lo, hi) = f == "eyeSize" ? (20, 80) : f == "bridgeSize" ? (5, 40) : (80, 180);
                var inRange = p.Values.Count(v => int.TryParse(v.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var x) && x >= lo && x <= hi);
                if (p.Share("D") >= 0.1)
                {
                    return S("valeurs composites dans une colonne de mesure");
                }

                return inRange < 0.8 * p.N ? V($"valeurs hors plage {lo}–{hi}") : ok;
            }

            case "frameDimensions":
                return p.Share("D") < 0.8 ? V("pas de motif calibre-pont-branche") : ok;
            case "power" or "sphereMin" or "sphereMax" or "cylinder" or "cylinderMin" or "cylinderMax" or "additionMin" or "additionMax" or "axis":
                return p.Share("S", "N", "I") < 0.8 ? V("valeurs non numériques") : ok;
            case "baseCurve" or "lensIndex":
                if (p.Share("S") > 0.1)
                {
                    return V("valeurs signées");
                }

                return p.Share("N", "I", "U") < 0.8 ? V("valeurs non numériques") : ok;
            case "diameter":
                if (p.Share("S") > 0.1)
                {
                    return V("valeurs signées");
                }

                // Référentiel des verres : liste de diamètres (« 65/70/75 ») admise.
                return p.Share("N", "I", "U") + (p.Values.Count(v => DiameterList().IsMatch(v.Trim())) / (double)p.N) < 0.8 ? V("valeurs non numériques") : ok;
            case "packSize" or "volumeMl" or "quantity" or "leadTimeDays":
                return p.Share("I", "U") < 0.8 ? V("valeurs non entières") : ok;
            case "combinationMax":
                return p.Share("I", "N") < 0.8 ? V("valeurs non numériques ou signées") : ok;
            case "vatRate":
                return p.Share("P", "I", "N") < 0.8 ? V("valeurs non numériques") : ok;
            case "currency":
                return p.Values.Count(v => CurrencyCode().IsMatch(v.Trim())) < 0.8 * p.N ? V("pas un code de devise") : ok;
            case "familyCode":
                return p.Distinct > 30 ? V("trop de valeurs distinctes pour une famille") : ok;
            case "lensType":
                return p.Values.Count(CatalogLexicon.LensTypeLike) < 0.5 * p.N ? S("valeurs sans géométrie de verre reconnue") : ok;
            case "notes":
                return ok;
        }

        if (CatalogLexicon.TextFields.Contains(f))
        {
            var num = p.Share("I", "N", "S", "G", "P", "A", "D");
            if (num >= 0.8)
            {
                return V("valeurs numériques");
            }

            if (num >= 0.15 && num <= 0.85 && f is not ("modelName" or "modelDescription" or "variantLabel"))
            {
                return S("contenu mixte (nombres et mots)");
            }
        }

        return ok;
    }

    private static string Num(string v) => v.Trim().Replace(',', '.').TrimStart('+');

    /// <summary>
    /// Contrôle d'une cellule : valeur dérivée UNIQUEMENT de la valeur contrôlée (brute ou corrigée). Séparateur décimal
    /// ambigu : jamais deviné, sauf choix explicite de l'utilisateur pour le fichier (<paramref name="decimalSeparator"/>).
    /// </summary>
    public static (string? Value, string Status, string? Reason) Cell(string field, string raw, char? decimalSeparator = null)
    {
        var v = raw.Trim();
        var k = CatalogLexicon.Cls(raw);
        if (k == "E")
        {
            return (null, CatalogStatuses.Valid, null);
        }

        if (k == "F")
        {
            return (null, CatalogStatuses.Invalid, "formule de tableur refusée");
        }

        if (CatalogLexicon.Money.Contains(field))
        {
            if (k is "I")
            {
                return (v, CatalogStatuses.Valid, null);
            }

            if (k is "N")
            {
                var dec = v[(v.IndexOfAny([',', '.']) + 1)..];
                if (dec.Length != 3)
                {
                    return (Num(v), CatalogStatuses.Valid, null);
                }

                return decimalSeparator is { } sep && Amount(v, sep) is { } chosen
                    ? (chosen, CatalogStatuses.Warning, $"séparateur décimal « {sep} » choisi pour le fichier")
                    : (null, CatalogStatuses.Ambiguous, "séparateur de milliers ou décimal ?");
            }

            if (k is "A")
            {
                var m = EuroAmount().Match(v);
                if (m.Success)
                {
                    return (Num(m.Groups[1].Value), CatalogStatuses.Warning, "symbole monétaire retiré");
                }

                return decimalSeparator is { } sep && Amount(v, sep) is { } chosen
                    ? (chosen, CatalogStatuses.Warning, $"séparateur décimal « {sep} » choisi pour le fichier")
                    : (null, CatalogStatuses.Ambiguous, "séparateur de milliers : montant non deviné");
            }

            return k is "S" ? (null, CatalogStatuses.Invalid, "montant négatif") : (null, CatalogStatuses.Invalid, "montant illisible");
        }

        switch (field)
        {
            case "barcode":
                if (k != "G")
                {
                    return (null, CatalogStatuses.Invalid, "code-barres : 8 à 14 chiffres attendus");
                }

                if (v.Length is not (8 or 12 or 13 or 14))
                {
                    return (null, CatalogStatuses.Invalid, $"code-barres de {v.Length} chiffres");
                }

                // Le catalogue refuse une clé GTIN fausse (CatalogInputValidator) : bloquant ici aussi, jamais corrigé en silence.
                return CatalogLexicon.GtinValid(v) ? (v, CatalogStatuses.Valid, null) : (null, CatalogStatuses.Invalid, "clé de contrôle GTIN incorrecte");
            case "supplierReference" or "manufacturerReference" or "sku":
                if (k == "X")
                {
                    return (null, CatalogStatuses.Invalid, "référence contenant une consigne");
                }

                return v.Length > 40 ? (null, CatalogStatuses.Invalid, "référence trop longue") : (v, CatalogStatuses.Valid, null);
            case "power" or "sphereMin" or "sphereMax" or "cylinder" or "cylinderMin" or "cylinderMax" or "additionMin" or "additionMax":
            {
                if (k is not ("S" or "N" or "I"))
                {
                    return (null, CatalogStatuses.Invalid, "nombre décimal signé attendu");
                }

                var d = decimal.Parse(Num(v), CultureInfo.InvariantCulture);
                if (Math.Abs(d) > 30)
                {
                    return (null, CatalogStatuses.Invalid, "hors plage");
                }

                var value = (v.StartsWith('-') ? "-" : string.Empty) + Num(v).TrimStart('-');
                return field == "power" && d * 4 != Math.Truncate(d * 4) ? (value, CatalogStatuses.Warning, "pas de 0,25 non respecté") : (value, CatalogStatuses.Valid, null);
            }

            case "axis":
                return k == "I" ? (v, CatalogStatuses.Valid, null) : (null, CatalogStatuses.Invalid, "axe entier attendu");
            case "baseCurve" or "diameter" or "lensIndex":
                if (k is "N" or "I")
                {
                    return (Num(v), CatalogStatuses.Valid, null);
                }

                if (field == "diameter" && DiameterList().IsMatch(v))
                {
                    return (DiameterSeparators().Replace(v, "/"), CatalogStatuses.Valid, null);
                }

                if (k == "U")
                {
                    var m = Millimetres().Match(v);
                    return m.Success && field != "lensIndex"
                        ? (Num(m.Groups[1].Value), CatalogStatuses.Warning, "unité mm retirée")
                        : (null, CatalogStatuses.Ambiguous, "unité inattendue : aucune conversion");
                }

                return (null, CatalogStatuses.Invalid, "nombre attendu");
            case "eyeSize" or "bridgeSize" or "templeLength":
            {
                if (k == "D")
                {
                    return (null, CatalogStatuses.Ambiguous, "valeur composite dans une colonne de mesure");
                }

                var (lo, hi) = field == "eyeSize" ? (20, 80) : field == "bridgeSize" ? (5, 40) : (80, 180);
                return k == "I" && int.Parse(v, CultureInfo.InvariantCulture) is var x && x >= lo && x <= hi
                    ? (v, CatalogStatuses.Valid, null)
                    : (null, CatalogStatuses.Invalid, $"entier {lo}–{hi} attendu");
            }

            case "frameDimensions":
            {
                var m = FrameDimensions().Match(v);
                return m.Success
                    ? ($"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value}", CatalogStatuses.Valid, null)
                    : (null, CatalogStatuses.Ambiguous, "motif calibre-pont-branche non reconnu");
            }

            case "packSize":
            {
                if (k == "I")
                {
                    return (v, CatalogStatuses.Valid, null);
                }

                var m = PackUnit().Match(v);
                if (m.Success)
                {
                    return (m.Groups[1].Value, CatalogStatuses.Warning, "libellé d'unité retiré");
                }

                return k == "U" ? (null, CatalogStatuses.Ambiguous, "unité de contenance dans un conditionnement") : (null, CatalogStatuses.Invalid, "entier attendu");
            }

            case "volumeMl":
            {
                if (k == "I")
                {
                    return (v, CatalogStatuses.Valid, null);
                }

                var m = Millilitres().Match(v);
                if (m.Success)
                {
                    return (m.Groups[1].Value, CatalogStatuses.Warning, "unité ml retirée");
                }

                return k == "U" ? (null, CatalogStatuses.Ambiguous, "unité différente de ml : aucune conversion") : (null, CatalogStatuses.Invalid, "contenance attendue");
            }

            case "leadTimeDays":
            {
                if (k == "I")
                {
                    return int.Parse(v, CultureInfo.InvariantCulture) <= 365 ? (v, CatalogStatuses.Valid, null) : (null, CatalogStatuses.Invalid, "délai de 0 à 365 jours attendu");
                }

                var m = Days().Match(v);
                return m.Success && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) <= 365
                    ? (m.Groups[1].Value, CatalogStatuses.Warning, "unité de jours retirée")
                    : (null, CatalogStatuses.Invalid, "délai en jours attendu");
            }

            case "quantity":
                return k == "I" ? (v, CatalogStatuses.Valid, null) : (null, CatalogStatuses.Invalid, "entier attendu");
            case "combinationMax":
                return k is "I" or "N" && decimal.Parse(Num(v), CultureInfo.InvariantCulture) is > 0 and <= 45
                    ? (Num(v), CatalogStatuses.Valid, null)
                    : (null, CatalogStatuses.Invalid, "somme |SPH| + |CYL| de 0 à 45 attendue");
            case "vatRate":
                if (k == "P")
                {
                    return (Num(v.TrimEnd('%', ' ')), CatalogStatuses.Valid, null);
                }

                return k is "I" or "N" ? (Num(v), CatalogStatuses.Warning, "taux sans %") : (null, CatalogStatuses.Invalid, "taux attendu");
            case "currency":
                return CurrencyCode().IsMatch(v) ? (v, CatalogStatuses.Valid, null) : (null, CatalogStatuses.Invalid, "code ISO à 3 lettres attendu");
            case "notes":
                return k == "X" ? (v, CatalogStatuses.Warning, "consigne suspecte conservée comme simple texte") : (v, CatalogStatuses.Valid, null);
        }

        if (k == "X")
        {
            return CatalogLexicon.IdentityText.Contains(field) ? (null, CatalogStatuses.Invalid, "consigne dans une valeur d'identité") : (v, CatalogStatuses.Warning, "consigne suspecte conservée comme texte");
        }

        return (v, CatalogStatuses.Valid, null);
    }

    /// <summary>Montant lu avec le séparateur décimal CHOISI : l'autre signe (et les espaces) est un séparateur de milliers.</summary>
    private static string? Amount(string raw, char separator)
    {
        var text = AmountSymbol().Replace(raw.Trim(), string.Empty).Trim();
        var thousands = separator == ',' ? '.' : ',';
        text = text.Replace(thousands.ToString(), string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (separator == ',')
        {
            text = text.Replace(',', '.');
        }

        return PlainAmount().IsMatch(text) ? text : null;
    }

    public static ColumnProfile Profile(CsvDocument document, int c, int skipFirst, IReadOnlyDictionary<(int Line, int Column), (string Value, int Revision)>? corrections = null)
    {
        var p = new ColumnProfile();
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in document.Records.Skip(skipFirst))
        {
            if (r.Empty || r.Cells.Length != document.ModalColumns || c >= r.Cells.Length)
            {
                continue;
            }

            var v = corrections is not null && corrections.TryGetValue((r.Line, c), out var fix) ? fix.Value : r.Cells[c];
            if (v.Trim().Length == 0)
            {
                continue;
            }

            p.N++;
            p.Values.Add(v);
            set.Add(v.Trim());
            var k = CatalogLexicon.Cls(v);
            p.Classes[k] = p.Classes.GetValueOrDefault(k) + 1;
        }

        p.Distinct = set.Count;
        return p;
    }

    private static string RulesHeader(CsvDocument document)
    {
        var first = document.Records.FirstOrDefault(r => !r.Empty);
        if (first == null)
        {
            return "absent";
        }

        var cells = first.Cells.Where(c => c.Trim().Length > 0).ToList();
        var numeric = cells.Count(c => CatalogLexicon.Cls(c) is "G" or "I" or "S" or "N" or "P" or "D" or "A");
        var empty = new ColumnProfile();
        var lex = cells.Count(c => CatalogLexicon.Rule(c, first.Cells, empty, false) is var r && (r.Field != null || r.Alternatives.Length > 0 || r.Percent || r.Suspect) || CatalogLexicon.Headerish(c));
        if (numeric == 0 && lex >= 1)
        {
            return "present";
        }

        return numeric >= Math.Max(1, cells.Count / 4) ? "absent" : "uncertain";
    }

    /// <summary>Validation complète. <paramref name="ai"/> : dernière proposition exploitable (null : règles seules).</summary>
    public static CatalogInterpretation Validate(CsvDocument document, CatalogProposal? ai, AssistantUserLayer? user = null, string target = AssistantTargets.Products)
    {
        user ??= AssistantUserLayer.Empty;
        var lensTarget = target == AssistantTargets.Lenses;
        var d = new CatalogInterpretation { Document = document, Target = target, AiUsed = ai != null };
        var recByLine = document.Records.ToDictionary(x => x.Line);
        char? decimalSeparator = user.DecimalSeparator?.Separator;

        // ---------------------------------------------------------------- 1. en-tête
        var hr = RulesHeader(document);
        d.HeaderRule = hr;
        d.HeaderAi = ai?.Header;
        if (user.HeaderPresent is { } chosenHeader)
        {
            d.HeaderStatus = CatalogStatuses.Valid;
            d.HeaderPresent = chosenHeader.Present;
            d.HeaderUserRevision = chosenHeader.Revision;
        }
        else if (hr == "uncertain")
        {
            d.HeaderStatus = CatalogStatuses.Ambiguous;
            d.HeaderPresent = (ai?.Header ?? "present") == "present";
        }
        else if (ai == null || ai.Header == hr)
        {
            d.HeaderStatus = CatalogStatuses.Valid;
            d.HeaderPresent = hr == "present";
        }
        else
        {
            d.HeaderStatus = CatalogStatuses.Ambiguous;
            d.HeaderPresent = hr == "present";
        }

        var headerBlocked = d.HeaderStatus == CatalogStatuses.Ambiguous;
        var skip = d.HeaderPresent ? 1 : 0;
        var headerCells = d.HeaderPresent && document.Records.Count > 0 ? document.Records[0].Cells : [];
        var headers = Enumerable.Range(0, document.Columns).Select(c => c < headerCells.Length ? headerCells[c] : string.Empty).ToList();
        var profiles = Enumerable.Range(0, document.Columns).Select(c => Profile(document, c, skip, user.CellValues)).ToList();
        var lensContext = lensTarget || headers.Any(h => CatalogLexicon.Tok(h).Overlaps(["indice", "sph", "addition", "add", "gamme", "traitement", "traitements", "cyl"]));
        var normHeaders = headers.Select(CatalogLexicon.Norm).ToList();
        d.Injection = headers.Any(CatalogLexicon.Suspicious) || document.Records.Any(r => r.Cells.Any(CatalogLexicon.Suspicious));

        // ---------------------------------------------------------------- 2. colonnes
        for (var c = 0; c < document.Columns; c++)
        {
            var prof = profiles[c];
            var cd = new ColumnDecision { C = c, Header = headers[c], Profile = prof };
            var rule = d.HeaderPresent ? CatalogLexicon.Rule(headers[c], headers, prof, lensContext) : CatalogLexicon.ValueRule(prof);
            cd.RuleField = rule.Field;
            cd.RuleAlternatives = rule.Alternatives;
            cd.RuleExplicit = rule.Explicit;
            cd.RuleName = rule.Name;
            cd.Reasons.Add("règle : " + rule.Why);
            var aic = ai != null && ai.Columns.TryGetValue(c, out var x) ? x : null;
            var a = aic != null && aic.Field != "unknown" ? aic.Field : null;
            cd.AiField = aic?.Field;
            cd.AiConfidence = aic?.Confidence;
            cd.AiIssue = aic?.Issue;
            string? softA = null;
            if (a != null)
            {
                var h = Check(a, prof, rule);
                if (h.Violation)
                {
                    cd.AiRejected = true;
                    cd.Reasons.Add($"proposition IA « {a} » refusée : {h.Why}");
                    a = null;
                }
                else if (h.Soft)
                {
                    softA = h.Why;
                }
            }

            var r = rule.Field;
            var alt = rule.Alternatives.ToList();
            string? softR = null;
            if (r != null)
            {
                var h = Check(r, prof, rule);
                if (h.Violation)
                {
                    cd.Reasons.Add($"intitulé « {r} » incompatible avec les valeurs : {h.Why}");
                    alt.Insert(0, r);
                    r = null;
                }
                else if (h.Soft)
                {
                    softR = h.Why;
                }
            }

            var dup = normHeaders[c].Length > 0 && normHeaders.Count(h => h == normHeaders[c]) > 1;
            if (!d.HeaderPresent || headerBlocked)
            {
                var suggestion = a ?? (alt.Count > 0 ? alt[0] : r);
                if (suggestion == null)
                {
                    cd.Status = CatalogStatuses.Unrecognized;
                }
                else
                {
                    cd.Field = suggestion;
                    cd.Status = CatalogStatuses.Ambiguous;
                    cd.Origin = a != null ? InterpretationOrigins.Ai : InterpretationOrigins.Rule;
                    cd.Reasons.Add(headerBlocked ? "présence d'en-têtes à confirmer : colonne à confirmer" : "fichier sans en-têtes : chaque colonne est confirmée par l'utilisateur");
                }
            }
            else if (dup)
            {
                cd.Field = a ?? r ?? (alt.Count > 0 ? alt[0] : "unknown");
                cd.Status = cd.Field == "unknown" ? CatalogStatuses.Unrecognized : CatalogStatuses.Ambiguous;
                cd.Origin = a != null ? InterpretationOrigins.Ai : InterpretationOrigins.Rule;
                cd.Reasons.Add("en-tête en double");
            }
            else if (r != null)
            {
                cd.Field = r;
                if (a == null || a == r)
                {
                    cd.Status = CatalogStatuses.Valid;
                    cd.Origin = a == r ? InterpretationOrigins.RuleAndAi : InterpretationOrigins.Rule;
                }
                else
                {
                    cd.Status = CatalogStatuses.Ambiguous;
                    cd.Origin = InterpretationOrigins.Rule;
                    cd.AiConflict = true;
                    cd.Suggestion = a;
                    cd.Reasons.Add($"l'IA propose « {a} » : désaccord à trancher");
                }
            }
            else if (alt.Count > 0)
            {
                cd.Field = a != null && alt.Contains(a) ? a : alt[0];
                cd.Origin = a != null && alt.Contains(a) ? InterpretationOrigins.Ai : InterpretationOrigins.Rule;
                if (a != null && !alt.Contains(a))
                {
                    cd.AiConflict = true;
                    cd.Suggestion = a;
                }

                cd.Status = CatalogStatuses.Ambiguous;
                cd.Reasons.Add("intitulé ambigu : choix de l'utilisateur requis");
            }
            else if (a != null && a != "notes")
            {
                cd.Status = CatalogStatuses.Unrecognized;
                cd.Suggestion = a;
                cd.Origin = InterpretationOrigins.AiSuggestion;
                cd.Reasons.Add($"suggestion de l'IA « {a} » non appliquée : aucune règle ne la confirme");
            }
            else
            {
                cd.Status = CatalogStatuses.Unrecognized;
                cd.Suggestion = a;
            }

            if (cd.Status == CatalogStatuses.Valid)
            {
                if (cd.Field == "priceUnqualified")
                {
                    cd.Status = CatalogStatuses.Ambiguous;
                    cd.Reasons.Add("montant non qualifié");
                }
                else if (CatalogLexicon.Money.Contains(cd.Field) && !rule.Explicit)
                {
                    cd.Status = CatalogStatuses.Ambiguous;
                    cd.Reasons.Add("confirmation commerciale requise");
                }
                else if (softR != null)
                {
                    cd.Status = CatalogStatuses.Ambiguous;
                    cd.Reasons.Add(softR);
                }
                else if (aic != null && aic.Issue != "none" && !(aic.Issue == "suspicious_instruction" && cd.Field == "notes"))
                {
                    cd.Status = CatalogStatuses.Ambiguous;
                    cd.AiFlag = true;
                    cd.Reasons.Add($"l'IA signale « {aic.Issue} »");
                }
            }

            if (cd.Status == CatalogStatuses.Ambiguous && softA != null)
            {
                cd.Reasons.Add("réserve sur la suggestion : " + softA);
            }

            cd.RuleOk = d.HeaderPresent && !headerBlocked && !dup && r != null && softR == null && r != "priceUnqualified" && (!CatalogLexicon.Money.Contains(r) || rule.Explicit);
            if (cd.RuleOk)
            {
                cd.RuleField = r;
            }

            // Décision de l'utilisateur : PRIORITAIRE (le désaccord de l'IA reste affiché pour information, sans rouvrir la colonne).
            if (user.ColumnFields.TryGetValue(c, out var chosen))
            {
                cd.UserRevision = chosen.Revision;
                cd.Origin = InterpretationOrigins.User;
                if (chosen.Field == "ignore")
                {
                    cd.Field = "unknown";
                    cd.Status = CatalogStatuses.Unrecognized;
                    cd.Reasons.Add($"colonne non importée (choix de l'utilisateur, révision {chosen.Revision})");
                }
                else
                {
                    cd.Field = chosen.Field;
                    var h = Check(chosen.Field, prof, rule with { Name = false, Percent = rule.Percent });
                    if (h.Violation)
                    {
                        cd.Status = CatalogStatuses.Ambiguous;
                        cd.Reasons.Add($"choix de l'utilisateur contredit par les valeurs : {h.Why}");
                    }
                    else
                    {
                        cd.Status = CatalogStatuses.Valid;
                        cd.Reasons.Add($"champ confirmé par l'utilisateur (révision {chosen.Revision})");
                    }
                }
            }

            d.Columns.Add(cd);
        }

        // Un champ ne sert qu'à une colonne (sauf libellés de déclinaison et remarques).
        foreach (var g in d.Columns.Where(c => c.Field is not ("unknown" or "notes" or "color" or "variantLabel") && c.Status != CatalogStatuses.Unrecognized)
            .GroupBy(c => c.Field).Where(g => g.Count() > 1))
        {
            foreach (var c in g.Where(c => c.Status != CatalogStatuses.Ambiguous))
            {
                c.Status = CatalogStatuses.Ambiguous;
                c.Reasons.Add("même champ sur plusieurs colonnes");
            }
        }

        foreach (var g in d.Columns.Where(c => c.RuleOk && c.RuleField is not ("notes" or "color" or "variantLabel")).GroupBy(c => c.RuleField).Where(g => g.Count() > 1))
        {
            foreach (var c in g)
            {
                c.RuleOk = false;
            }
        }

        // ---------------------------------------------------------------- 3. lignes et cellules
        var mapped = d.Columns.Where(c => c.Field != "unknown" && c.Status != CatalogStatuses.Unrecognized).ToList();
        for (var i = 0; i < document.Records.Count; i++)
        {
            var rec = document.Records[i];
            var rd = new RowDecision { Line = rec.Line };
            if (i == 0 && d.HeaderPresent && !headerBlocked)
            {
                rd.Kind = "header";
                d.Rows.Add(rd);
                continue;
            }

            if (rec.Empty)
            {
                rd.Kind = "empty";
                d.Rows.Add(rd);
                continue;
            }

            if (user.SkippedLines.TryGetValue(rec.Line, out var skippedAt))
            {
                rd.Kind = "skipped";
                rd.SkippedRevision = skippedAt;
                rd.Reasons.Add($"ligne ignorée volontairement (révision {skippedAt})");
                d.Rows.Add(rd);
                continue;
            }

            if (i == 0 && headerBlocked)
            {
                rd.Status = CatalogStatuses.Ambiguous;
                rd.Reasons.Add("en-tête ou article : à confirmer");
            }

            if (rec.Cells.Length == 1 && document.ModalColumns >= 3)
            {
                rd.Kind = "comment";
                rd.Status = CatalogStatuses.Warning;
                rd.Reasons.Add("ligne de commentaire ou de section : affichée, non importée");
                d.Rows.Add(rd);
                continue;
            }

            if (rec.Anomalies.Contains("fewer_cells") || rec.Anomalies.Contains("more_cells"))
            {
                rd.Structural = true;
                rd.Status = CatalogStatuses.Invalid;
                rd.Reasons.Add("ligne mal alignée (" + string.Join(", ", rec.Anomalies) + ") : aucune cellule décalée");
            }

            foreach (var col in mapped)
            {
                var raw = rec.Cell(col.C);
                var corrected = user.CellValues.TryGetValue((rec.Line, col.C), out var fix) ? fix : ((string Value, int Revision)?)null;
                var input = corrected?.Value ?? raw;
                var (value, status, reason) = Cell(col.Field, input, decimalSeparator);
                rd.Cells.Add(new CellDecision
                {
                    C = col.C, Field = col.Field, Raw = raw, Input = input, Value = value, Status = status, Reason = reason, SourceLine = rec.Line,
                    CorrectedRevision = corrected?.Revision,
                });
            }

            foreach (var c in Enumerable.Range(0, rec.Cells.Length).Where(c => !mapped.Any(m => m.C == c)))
            {
                if (CatalogLexicon.Cls(rec.Cells[c]) is "X")
                {
                    rd.Reasons.Add($"c{c} : consigne suspecte (non importée)");
                }
            }

            d.Rows.Add(rd);
        }

        var dataRows = d.DataRows.ToList();
        var rowByLine = d.Rows.ToDictionary(x => x.Line);
        foreach (var f in new[] { "barcode", "supplierReference", "manufacturerReference", "sku" })
        {
            foreach (var g in dataRows.SelectMany(r => r.Cells.Where(c => c.Field == f && c.Value != null && c.Status != CatalogStatuses.Invalid))
                .GroupBy(c => f == "barcode" ? c.Value : CatalogKeys.Normalize(c.Value!)).Where(g => g.Count() > 1))
            {
                foreach (var c in g)
                {
                    c.Status = CatalogStatuses.Invalid;
                    c.Reason = f == "barcode" ? "EAN en double dans le fichier" : "référence en double dans le fichier";
                }
            }
        }

        // ---------------------------------------------------------------- 4. familles
        var familyCodeColumn = d.Columns.FirstOrDefault(c => (c.RuleField == "familyCode" || (c.UserRevision != null && c.Field == "familyCode")) && c.Status != CatalogStatuses.Unrecognized && d.HeaderPresent && !headerBlocked);
        var keywordColumns = d.HeaderPresent && !headerBlocked
            ? d.Columns.Where(c => (c.RuleField != null && CatalogLexicon.KeywordFields.Contains(c.RuleField)) || c.RuleAlternatives.Any(CatalogLexicon.KeywordFields.Contains)).Select(c => c.C).ToList()
            : [];
        var evidenceColumns = d.Columns.Where(c => (c.RuleOk || (c.UserRevision != null && c.Importable)) && FieldFamilies(c.UserRevision != null ? c.Field : c.RuleField!) != null).ToList();
        string? AiFamily(RowDecision r)
        {
            if (ai == null)
            {
                return null;
            }

            if (ai.FamilyMode == "single")
            {
                return ai.FamilySingle == "unknown" ? null : ai.FamilySingle;
            }

            if (ai.FamilyMode == "by_column" && ai.FamilyColumn >= 0 && ai.FamilyValues.TryGetValue(ai.FamilyColumn, out var map))
            {
                var v = recByLine[r.Line].Cell(ai.FamilyColumn).Trim();
                return map.TryGetValue(v, out var f) && f != "unknown" ? f : null;
            }

            return null;
        }

        foreach (var r in dataRows)
        {
            if (lensTarget)
            {
                // Destination « référentiel des verres » choisie par l'utilisateur au dépôt : famille imposée par ce choix.
                r.Family = Families.OphthalmicLenses;
                r.FamilyStatus = CatalogStatuses.Valid;
                r.FamilyOrigin = InterpretationOrigins.Target;
                continue;
            }

            var rec = recByLine[r.Line];
            var s = new HashSet<string>(CatalogLexicon.FamilyCodes);
            var evidence = false;
            var conflict = false;
            string? codeValue = null;
            var why = new List<string>();
            if (familyCodeColumn != null)
            {
                var v = (user.CellValues.TryGetValue((r.Line, familyCodeColumn.C), out var fix) ? fix.Value : rec.Cell(familyCodeColumn.C)).Trim();
                if (v.Length > 0)
                {
                    evidence = true;
                    codeValue = v;
                    var f = CatalogLexicon.FamilyOfCode(v);
                    if (f == null)
                    {
                        conflict = true;
                        why.Add($"code de famille « {v} » inconnu");
                    }
                    else
                    {
                        s.IntersectWith([f]);
                        why.Add($"code « {v} »");
                    }
                }
            }

            foreach (var c in keywordColumns)
            {
                var v = rec.Cell(c);
                if (CatalogLexicon.Cls(v) is "X" or "F")
                {
                    continue;
                }

                var f = CatalogLexicon.KeywordFamily(v);
                if (f == null)
                {
                    continue;
                }

                evidence = true;
                why.Add($"mot-clé ({f})");
                s.IntersectWith([f]);
            }

            foreach (var c in evidenceColumns)
            {
                var cell = r.Cells.FirstOrDefault(x => x.C == c.C);
                if (cell?.Value == null || CatalogStatuses.Blocking(cell.Status))
                {
                    continue;
                }

                evidence = true;
                s.IntersectWith(FieldFamilies(c.UserRevision != null ? c.Field : c.RuleField!)!);
            }

            if (evidence && s.Count == 0)
            {
                conflict = true;
                why.Add("indices de famille contradictoires");
            }

            // Classe stable, tirée des seules preuves des règles (jamais de l'IA) : cible de la confirmation groupée.
            r.FamilyClass = codeValue != null ? "code:" + CatalogLexicon.Norm(codeValue)
                : conflict ? "conflict"
                : !evidence ? "none"
                : "set:" + string.Join("/", s.OrderBy(x => Array.IndexOf(CatalogLexicon.FamilyCodes, x)));
            var p = AiFamily(r);
            r.FamilySuggestion = p;
            if (conflict)
            {
                r.FamilyStatus = CatalogStatuses.Ambiguous;
                r.Family = p;
                r.Reasons.Add("famille : " + string.Join(", ", why));
            }
            else if (!evidence)
            {
                r.FamilyStatus = CatalogStatuses.Ambiguous;
                r.Family = p;
                r.Reasons.Add("famille sans preuve déterministe : à confirmer");
            }
            else if (s.Count == 1)
            {
                var f = s.First();
                r.Family = f;
                if (p == null || p == f)
                {
                    r.FamilyStatus = CatalogStatuses.Valid;
                    r.FamilyOrigin = InterpretationOrigins.Rule;
                }
                else
                {
                    r.FamilyStatus = CatalogStatuses.Ambiguous;
                    r.Reasons.Add($"famille : règle « {f} », IA « {p} »");
                }
            }
            else
            {
                r.FamilyStatus = CatalogStatuses.Ambiguous;
                r.Family = p != null && s.Contains(p) ? p : s.OrderBy(x => Array.IndexOf(CatalogLexicon.FamilyCodes, x)).First();
                r.Reasons.Add("famille non tranchée par les règles (" + string.Join("/", s) + ")");
            }

            if (user.FamilyClasses.TryGetValue(r.FamilyClass, out var familyChoice))
            {
                r.Family = familyChoice.Family;
                r.FamilyStatus = CatalogStatuses.Valid;
                r.FamilyOrigin = InterpretationOrigins.User;
                r.Reasons.RemoveAll(x => x.StartsWith("famille", StringComparison.Ordinal));
                r.Reasons.Add($"famille confirmée par l'utilisateur (révision {familyChoice.Revision})");
            }
        }

        // ---------------------------------------------------------------- 5. identité, continuation, regroupement (serveur)
        List<ColumnDecision> IdentityColumns(string? family)
        {
            var byField = d.Columns.Where(c => c.Field != "unknown" && c.Status != CatalogStatuses.Unrecognized).ToList();
            string[] fields = family == Families.OphthalmicLenses
                ? ["lensManufacturer", "brand", "lensRange", "modelName", "lensType", "lensIndex", "lensMaterial", "lensTreatments"]
                : byField.Any(c => c.Field == "modelName") ? ["brand", "modelName"]
                : byField.Any(c => c.Field == "modelDescription") ? ["brand", "modelDescription"] : ["brand", "lensRange"];
            return byField.Where(c => fields.Contains(c.Field)).ToList();
        }

        var allIdentityColumns = d.Columns.Where(c => c.Status != CatalogStatuses.Unrecognized && (CatalogLexicon.IdentityText.Contains(c.Field) || c.Field is "lensType" or "lensIndex" or "lensMaterial" or "lensTreatments"))
            .Select(c => c.C).ToHashSet();
        var nameColumns = d.Columns.Where(c => c.Status != CatalogStatuses.Unrecognized && c.Field is "modelName" or "modelDescription" or "lensRange").ToList();
        var groups = new Dictionary<string, GroupDecision>(StringComparer.Ordinal);
        RowDecision? lastArticle = null;
        foreach (var r in d.Rows)
        {
            if (r.Kind != "data")
            {
                if (r.Kind is "empty" or "comment")
                {
                    lastArticle = null;
                }

                continue;
            }

            var structurallyBad = r.Status == CatalogStatuses.Invalid;
            var identityEmpty = allIdentityColumns.Count > 0 && allIdentityColumns.All(c => (r.Cells.FirstOrDefault(x => x.C == c)?.Input ?? string.Empty).Trim().Length == 0);
            if (structurallyBad)
            {
                lastArticle = null;
                continue;
            }

            if (identityEmpty)
            {
                if (lastArticle != null && lastArticle.Group >= 0 && !user.ContinuationRefused.ContainsKey(r.Line) && user.Continuation is not { Accept: false })
                {
                    r.ContinuationOf = lastArticle.ContinuationOf ?? lastArticle.Line;
                    r.Family = lastArticle.Family;
                    r.FamilyStatus = lastArticle.FamilyStatus;
                    r.FamilyOrigin = InterpretationOrigins.Continuation;
                    r.FamilyClass = lastArticle.FamilyClass;
                    r.Reasons.RemoveAll(x => x.StartsWith("famille", StringComparison.Ordinal));
                    var g0 = d.Groups[lastArticle.Group];
                    g0.Lines.Add(r.Line);
                    r.Group = g0.Id;
                    var accepted = user.Continuation is { Accept: true };
                    if (!accepted)
                    {
                        if (!g0.Reasons.Contains("lignes de continuation : reprise de l'identité à confirmer"))
                        {
                            g0.Reasons.Add("lignes de continuation : reprise de l'identité à confirmer");
                        }

                        g0.Status = CatalogStatuses.Ambiguous;
                    }

                    // Identité reprise, avec provenance (aucune autre valeur reprise : un prix vide reste vide).
                    var parent = rowByLine[r.ContinuationOf.Value];
                    foreach (var pc in parent.Cells.Where(pc => allIdentityColumns.Contains(pc.C) && pc.Value != null))
                    {
                        var mine = r.Cells.FirstOrDefault(x => x.C == pc.C);
                        if (mine != null)
                        {
                            mine.Value = pc.Value;
                            mine.Raw = pc.Raw;
                            mine.Input = pc.Input;
                            mine.SourceLine = pc.SourceLine;
                            mine.Status = accepted ? CatalogStatuses.Max(mine.Status, pc.Status) : CatalogStatuses.Max(mine.Status, CatalogStatuses.Ambiguous);
                            mine.Reason = accepted ? $"reprise de L{pc.SourceLine} (continuation confirmée, révision {user.Continuation!.Value.Revision})" : $"reprise de L{pc.SourceLine} (à confirmer)";
                        }
                    }

                    continue;
                }

                r.Status = CatalogStatuses.Max(r.Status, CatalogStatuses.Invalid);
                r.Reasons.Add(user.ContinuationRefused.ContainsKey(r.Line) || user.Continuation is { Accept: false }
                    ? "identité absente (continuation refusée par l'utilisateur)"
                    : "identité absente sans article précédent");
                continue;
            }

            var idc = IdentityColumns(r.Family);
            var idCells = idc.Select(c => r.Cells.FirstOrDefault(x => x.C == c.C)).ToList();
            if (idCells.Any(x => x != null && x.Status == CatalogStatuses.Invalid) || nameColumns.Count == 0 || nameColumns.All(nc => r.Cells.FirstOrDefault(x => x.C == nc.C)?.Value == null))
            {
                r.Status = CatalogStatuses.Max(r.Status, nameColumns.Count == 0 ? CatalogStatuses.Ambiguous : CatalogStatuses.Invalid);
                r.Reasons.Add(nameColumns.Count == 0 ? "aucune colonne d'identité validée : regroupement impossible" : "identité du produit invalide ou absente");
                lastArticle = null;
                continue;
            }

            var key = (r.Family ?? "?") + "|" + string.Join("|", idCells.Select(x => CatalogLexicon.Norm(x?.Value ?? string.Empty)));
            if (!groups.TryGetValue(key, out var g))
            {
                g = new GroupDecision { Id = d.Groups.Count, Key = key };
                groups[key] = g;
                d.Groups.Add(g);
            }

            g.Lines.Add(r.Line);
            r.Group = g.Id;
            if (idc.Any(c => c.Status != CatalogStatuses.Valid) && !g.Reasons.Contains("colonnes d'identité à confirmer"))
            {
                g.Reasons.Add("colonnes d'identité à confirmer");
                g.Status = CatalogStatuses.Ambiguous;
            }

            lastArticle = r;
        }

        static bool IdentityType(string? f) => f != null && (CatalogLexicon.IdentityText.Contains(f) || f is "lensType" or "lensIndex" or "lensMaterial" or "lensTreatments");
        var variantColumns = d.Columns.Where(c => (c.RuleField != null || c.RuleAlternatives.Length > 0 || c.UserRevision != null) && c.Status != CatalogStatuses.Unrecognized
            && !IdentityType(c.UserRevision != null ? c.Field : c.RuleView) && !CatalogLexicon.Commercial.Contains(c.UserRevision != null ? c.Field : c.RuleView)
            && (c.UserRevision != null ? c.Field : c.RuleView) is not ("notes" or "quantity" or "unknown")).Select(c => c.C).ToList();
        var identityPending = d.Columns.Any(c => CatalogStatuses.Blocking(c.Status) && (IdentityType(c.Field) || c.RuleAlternatives.Any(IdentityType) || IdentityType(c.Suggestion)));
        foreach (var g in d.Groups)
        {
            var rows = g.Lines.Select(l => rowByLine[l]).ToList();
            if (rows.Any(x => x.FamilyStatus != CatalogStatuses.Valid))
            {
                g.Status = CatalogStatuses.Ambiguous;
                g.Reasons.Add("famille à confirmer");
            }

            if (rows.Count > 1 && !lensTarget)
            {
                if (variantColumns.Count == 0)
                {
                    g.Status = CatalogStatuses.Ambiguous;
                    g.Reasons.Add("déclinaisons non distinguées");
                }
                else
                {
                    var signatures = rows.Select(x => string.Join("|", variantColumns.Select(c => x.Cells.FirstOrDefault(cell => cell.C == c)?.Input.Trim() ?? recByLine[x.Line].Cell(c).Trim()))).ToList();
                    if (signatures.Distinct().Count() < signatures.Count)
                    {
                        g.Status = CatalogStatuses.Ambiguous;
                        g.Reasons.Add("doublon potentiel : mêmes déclinaisons");
                    }
                }
            }

            if (headerBlocked)
            {
                g.Status = CatalogStatuses.Ambiguous;
            }

            if (identityPending && g.Status == CatalogStatuses.Valid)
            {
                g.Status = CatalogStatuses.Ambiguous;
                g.Reasons.Add("colonne d'identité en attente");
            }
        }

        // Compatibilité champ / famille (C8).
        var families = dataRows.Where(r => r.Family != null).Select(r => r.Family!).ToHashSet();
        foreach (var c in d.Columns.Where(x => x.Status == CatalogStatuses.Valid && x.UserRevision == null && FieldFamilies(x.Field) != null))
        {
            if (families.Count > 0 && !families.Overlaps(FieldFamilies(c.Field)!))
            {
                c.Status = CatalogStatuses.Ambiguous;
                c.Reasons.Add("champ incompatible avec les familles du fichier");
            }
        }

        // ---------------------------------------------------------------- 6. statut des lignes, applicabilité
        foreach (var r in dataRows)
        {
            foreach (var cell in r.Cells)
            {
                var col = d.Columns[cell.C];
                // Colonne en attente (AMBIGU) : seuls les défauts indépendants de l'interprétation (consigne, formule) remontent à la ligne.
                if (col.Importable)
                {
                    r.Status = CatalogStatuses.Max(r.Status, cell.Status);
                    if (cell.Reason != null && cell.Status != CatalogStatuses.Valid)
                    {
                        r.Reasons.Add($"c{cell.C} {cell.Field} : {cell.Reason}");
                    }
                }
                else if (CatalogStatuses.Blocking(col.Status) && cell.Input.Trim().Length > 0)
                {
                    var intrinsic = CatalogLexicon.Cls(cell.Input) is "X" or "F";
                    r.Status = CatalogStatuses.Max(r.Status, intrinsic ? CatalogStatuses.Max(cell.Status, CatalogStatuses.Ambiguous) : CatalogStatuses.Ambiguous);
                    if (intrinsic && cell.Reason != null)
                    {
                        r.Reasons.Add($"c{cell.C} : {cell.Reason}");
                    }
                }
            }

            if (r.FamilyStatus == CatalogStatuses.Ambiguous)
            {
                r.Status = CatalogStatuses.Max(r.Status, CatalogStatuses.Ambiguous);
            }

            if (r.Group >= 0 && d.Groups[r.Group].Status == CatalogStatuses.Ambiguous)
            {
                r.Status = CatalogStatuses.Max(r.Status, CatalogStatuses.Ambiguous);
            }
        }

        var ambiguousColumns = d.Columns.Count(c => c.Status == CatalogStatuses.Ambiguous);
        var familyClasses = dataRows.Where(r => r.FamilyStatus == CatalogStatuses.Ambiguous && r.ContinuationOf == null).Select(r => r.FamilyClass).Distinct().Count();
        var continuation = dataRows.Any(r => r.ContinuationOf != null) && user.Continuation is not { Accept: true } ? 1 : 0;
        var duplicateGroups = d.Groups.Count(g => g.Reasons.Any(x => x.StartsWith("doublon", StringComparison.Ordinal)));
        var invalidRows = dataRows.Count(r => r.Status == CatalogStatuses.Invalid);
        var ambiguousCells = dataRows.Sum(r => r.Cells.Count(c => c.Status == CatalogStatuses.Ambiguous && d.Columns[c.C].Importable && !(c.Reason ?? string.Empty).StartsWith("reprise", StringComparison.Ordinal)));
        d.Blockers["header"] = headerBlocked ? 1 : 0;
        d.Blockers["columns"] = ambiguousColumns;
        d.Blockers["familyClasses"] = familyClasses;
        d.Blockers["continuation"] = continuation;
        d.Blockers["duplicateGroups"] = duplicateGroups;
        d.Blockers["invalidRows"] = invalidRows;
        d.Blockers["ambiguousCells"] = ambiguousCells;
        d.Confirmations = d.Blockers.Values.Sum();
        d.Applicable = d.Confirmations == 0 && dataRows.All(r => !CatalogStatuses.Blocking(r.Status));
        return d;
    }

    /// <summary>
    /// Monotonie P1 (portée du banc B0) : avec l'IA, aucune colonne, famille, valeur ni regroupement importable qui ne le
    /// soit aussi par les règles seules (mêmes décisions de l'utilisateur), avec le même champ, la même famille, la même valeur.
    /// </summary>
    public static IReadOnlyList<string> MonotonicViolations(CatalogInterpretation withAi, CatalogInterpretation rulesOnly)
    {
        var violations = new List<string>();
        foreach (var c in withAi.Columns)
        {
            var r0 = rulesOnly.Columns[c.C];
            if (c.Importable && (!r0.Importable || r0.Field != c.Field))
            {
                violations.Add($"c{c.C} {c.Field}");
            }
        }

        var r0Rows = rulesOnly.Rows.ToDictionary(r => r.Line);
        foreach (var r in withAi.DataRows)
        {
            if (!r0Rows.TryGetValue(r.Line, out var r0))
            {
                violations.Add($"L{r.Line} absente");
                continue;
            }

            if (r.FamilyAuto && (!r0.FamilyAuto || r0.Family != r.Family))
            {
                violations.Add($"L{r.Line} famille {r.Family}");
            }

            foreach (var cell in r.Cells.Where(x => x.Value != null && !CatalogStatuses.Blocking(x.Status) && withAi.Columns[x.C].Importable))
            {
                var c0 = r0.Cells.FirstOrDefault(x => x.C == cell.C);
                if (c0 == null || c0.Value != cell.Value || CatalogStatuses.Blocking(c0.Status) || !rulesOnly.Columns[cell.C].Importable)
                {
                    violations.Add($"L{r.Line} c{cell.C} valeur");
                }
            }
        }

        foreach (var g in withAi.Groups.Where(g => g.Status == CatalogStatuses.Valid && g.Lines.Count > 1))
        {
            var ids = g.Lines.Select(l => r0Rows.TryGetValue(l, out var r0) ? r0.Group : -1).Distinct().ToList();
            if (ids.Count != 1 || ids[0] < 0 || rulesOnly.Groups[ids[0]].Status != CatalogStatuses.Valid)
            {
                violations.Add($"groupe {string.Join(",", g.Lines)}");
            }
        }

        return violations;
    }

    /// <summary>Empreinte déterministe d'une interprétation (déterminisme vérifié par les tests).</summary>
    public static string Hash(CatalogInterpretation d)
    {
        var sb = new StringBuilder();
        sb.Append(d.HeaderPresent).Append(d.HeaderStatus).Append('|');
        foreach (var c in d.Columns)
        {
            sb.Append(c.C).Append(':').Append(c.Field).Append(':').Append(c.Status).Append(':').Append(c.Origin).Append(';');
        }

        foreach (var r in d.Rows)
        {
            sb.Append(r.Line).Append(r.Kind).Append(r.Status).Append(r.Family).Append(r.FamilyStatus).Append(r.Group).Append(r.ContinuationOf);
            foreach (var cell in r.Cells)
            {
                sb.Append(cell.C).Append('=').Append(cell.Value).Append(cell.Status);
            }

            sb.Append('\n');
        }

        foreach (var g in d.Groups)
        {
            sb.Append(g.Id).Append(g.Status).Append(string.Join(",", g.Lines)).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>Synthèse JSON (diagnostic, journal de test).</summary>
    public static JsonObject Summary(CatalogInterpretation d) => new()
    {
        ["header"] = new JsonObject { ["present"] = d.HeaderPresent, ["status"] = d.HeaderStatus, ["rules"] = d.HeaderRule, ["ai"] = d.HeaderAi },
        ["aiUsed"] = d.AiUsed,
        ["applicable"] = d.Applicable,
        ["confirmations"] = d.Confirmations,
        ["columns"] = new JsonArray([.. d.Columns.Select(c => (JsonNode?)new JsonObject { ["c"] = c.C, ["field"] = c.Field, ["status"] = c.Status, ["origin"] = c.Origin })]),
    };

    [GeneratedRegex(@"^(\d+([.,]\d{1,2})?)\s*(€|eur)$", RegexOptions.IgnoreCase)]
    private static partial Regex EuroAmount();

    [GeneratedRegex(@"(€|eur|\$|usd)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountSymbol();

    [GeneratedRegex(@"^\d+(\.\d{1,4})?$")]
    private static partial Regex PlainAmount();

    [GeneratedRegex(@"^(\d+([.,]\d+)?)\s*mm$", RegexOptions.IgnoreCase)]
    private static partial Regex Millimetres();

    [GeneratedRegex(@"^(\d{2})\s*[-x×/□ ]\s*(\d{2})\s*[-x×/□ ]\s*(\d{3})$")]
    private static partial Regex FrameDimensions();

    [GeneratedRegex(@"^(\d+)\s*(lentilles?|pcs?|pi[eè]ces?|unit[eé]s?|flacons?|bo[iî]tes?)$", RegexOptions.IgnoreCase)]
    private static partial Regex PackUnit();

    [GeneratedRegex(@"^(\d+)\s*ml$", RegexOptions.IgnoreCase)]
    private static partial Regex Millilitres();

    [GeneratedRegex(@"^(\d+)\s*(j|jours?|days?)$", RegexOptions.IgnoreCase)]
    private static partial Regex Days();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyCode();

    [GeneratedRegex(@"^\d{2}(\s*[/;|]\s*\d{2})+$")]
    private static partial Regex DiameterList();

    [GeneratedRegex(@"\s*[/;|]\s*")]
    private static partial Regex DiameterSeparators();
}
