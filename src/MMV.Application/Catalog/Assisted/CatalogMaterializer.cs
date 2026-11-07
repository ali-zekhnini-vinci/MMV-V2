using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;

namespace MMV.Application.Catalog.Assisted;

/// <summary>Action LICITE calculée par le serveur pour une anomalie (le poste n'en invente aucune).</summary>
public sealed record AssistantAction(string Type, string Label, IReadOnlyList<string> Requires, JsonObject Target, JsonObject? Constraints = null);

/// <summary>Action non proposée, avec sa raison (affichée).</summary>
public sealed record AssistantNotOffered(string Type, string Reason);

/// <summary>Cause testée d'un diagnostic (doublon de SKU / référence) : retenue ou écartée, avec sa preuve.</summary>
public sealed record AssistantDiagnosis(string Cause, bool Retained, string Evidence);

/// <summary>
/// Anomalie servie à l'étape 3 : valeurs d'origine, interprétation proposée, explication, suggestion (et son origine),
/// actions licites, actions non proposées, diagnostic, exemples représentatifs (montants) et leur empreinte.
/// <see cref="Grouped"/> : confirmation groupée (une décision vaut pour plusieurs lignes).
/// </summary>
public sealed record AssistantIssue(
    string Key, string Code, string Severity, IReadOnlyList<int> Lines, int? Column, IReadOnlyList<string> Original, string? Interpretation, string Explanation,
    string? Suggestion, string? SuggestionOrigin, IReadOnlyList<AssistantAction> Actions, IReadOnlyList<AssistantNotOffered> NotOffered,
    IReadOnlyList<AssistantDiagnosis>? Diagnosis = null, JsonObject? Examples = null, string? ExamplesDigest = null, bool Grouped = false);

/// <summary>Ligne du tableau de l'étape 3 (valeurs d'origine et interprétées, statut, anomalies).</summary>
public sealed record AssistantRowView(
    int Line, string Kind, string Status, string? Family, string FamilyStatus, string? Product, string? Variant, string? Reference, IReadOnlyList<AssistantCellView> Cells,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> IssueKeys, int? ContinuationOf, IReadOnlyList<string> Flags);

public sealed record AssistantCellView(int Column, string Field, string Original, string? Corrected, string? Value, string Status, string? Reason, int SourceLine, bool Money);

/// <summary>Ligne prête pour le plan d'écriture (champs du catalogue MMV, valeurs dérivées des seules cellules ou corrections).</summary>
public sealed record MaterializedRow(int LineNo, Dictionary<string, string> Values, IReadOnlyList<ImportError> PreErrors);

/// <summary>
/// Matérialisation serveur (C1.3) : à partir d'une interprétation validée, confirmations groupées, anomalies et actions
/// licites, diagnostic des doublons, exemples représentatifs des montants (empreinte <c>examplesDigest</c>), lignes du
/// tableau, et projection des lignes importables vers les champs d'écriture du catalogue. Jamais : SKU proposé par MMV ou
/// par l'IA, fusion automatique, association automatique d'un SKU porté par un autre modèle (décision du 06/11/2026).
/// </summary>
public static class CatalogMaterializer
{
    public const string Ignore = "ignore";

    /// <summary>Champs écrits au catalogue des produits par le nouveau parcours.</summary>
    public static readonly HashSet<string> ProductWriteFields =
    [
        "familyCode", "brand", "modelName", "modelDescription", "variantLabel", "color", "sku", "manufacturerReference", "supplierReference", "barcode",
        "stockPolicy", "supplierCode", "lensManufacturer", "lensRange", "lensIndex", "lensTreatments",
        "eyeSize", "bridgeSize", "templeLength", "frameDimensions", "material", "shape", "lensTint", "replacement", "power", "baseCurve", "diameter", "packSize", "volumeMl",
        "purchasePriceHt", "salePriceHt",
    ];

    /// <summary>Champs écrits au référentiel des verres (offres fournisseurs).</summary>
    public static readonly HashSet<string> LensWriteFields =
    [
        "lensManufacturer", "lensRange", "lensType", "lensIndex", "lensMaterial", "lensTreatments", "sphereMin", "sphereMax", "cylinderMin", "cylinderMax",
        "additionMin", "additionMax", "diameter", "purchasePriceHt", "leadTimeDays", "combinationMax", "stockPolicy", "supplierCode", "supplierReference", "currency", "notes",
    ];

    private static readonly string[] ModelAttributes = ["material", "shape", "replacement"];
    private static readonly string[] VariantAttributes = ["eyeSize", "bridgeSize", "templeLength", "lensTint", "power", "baseCurve", "diameter", "packSize", "volumeMl"];

    /// <summary>Raison pour laquelle un champ reconnu n'est PAS écrit (affichée, jamais silencieuse).</summary>
    public static string? NotWrittenReason(string field, string target) => field switch
    {
        "salePriceTtc" => "MMV enregistre des prix HT : aucune conversion TTC → HT",
        "priceUnqualified" => "montant non qualifié : choisir prix d'achat HT, prix de vente HT ou ne pas importer",
        "vatRate" => "taux de TVA non importé (fixé par la famille et le magasin)",
        "quantity" => "quantité non importée : le stock se saisit par mouvement",
        "unit" or "supplierName" => "information conservée dans le fichier d'origine, non importée",
        "notes" when target == AssistantTargets.Products => "remarque conservée dans le fichier d'origine, non importée",
        "cylinder" or "axis" => "cylindre et axe d'une lentille torique : non gérés par le catalogue actuel",
        "currency" when target == AssistantTargets.Products => null,
        _ when target == AssistantTargets.Products && !ProductWriteFields.Contains(field) => "champ du référentiel des verres : utiliser la destination « Référentiel des verres »",
        _ when target == AssistantTargets.Lenses && !LensWriteFields.Contains(field) => "champ sans équivalent dans une offre de verres",
        "salePriceHt" when target == AssistantTargets.Lenses => "une offre de verres porte un prix fournisseur (achat HT), pas un prix de vente",
        _ => null,
    };

    // ------------------------------------------------------------------ devise des montants (COR-CSVAI-03)

    /// <summary>Montant sans devise dans le fichier, devise du magasin non confirmée : la ligne n'est pas importable.</summary>
    public const string CurrencyUnconfirmed = "currency_unconfirmed";

    /// <summary>Devise du magasin introuvable (configuration) : aucun montant importable, jamais d'EUR par défaut.</summary>
    public const string StoreCurrencyUnknown = "store_currency_unknown";

    /// <summary>Champs de montant écrits selon la destination.</summary>
    public static IReadOnlyList<string> MoneyFields(string target) => target == AssistantTargets.Lenses ? ["purchasePriceHt"] : ["purchasePriceHt", "salePriceHt"];

    /// <summary>
    /// Devise du magasin CONFIRMÉE par l'utilisateur pour les montants du fichier qui n'en portent pas (décision
    /// <c>file_default</c> sur le champ <c>currency</c>) ; nulle si rien n'est confirmé, si la devise du magasin est
    /// introuvable ou si elle a changé depuis la confirmation. Une devise absente du CSV n'est jamais « fournie ».
    /// </summary>
    public static string? ConfirmedCurrency(AssistantUserLayer user, string? storeCurrency) =>
        storeCurrency is not null && user.FileDefaults.TryGetValue("currency", out var confirmed) && string.Equals(confirmed.Value, storeCurrency, StringComparison.OrdinalIgnoreCase)
            ? storeCurrency
            : null;

    /// <summary>Code d'erreur d'un montant sans devise explicite ni confirmée.</summary>
    public static string MissingCurrency(string? storeCurrency) => storeCurrency is null ? StoreCurrencyUnknown : CurrencyUnconfirmed;

    /// <summary>Lignes importables qui portent un montant écrit SANS devise explicite dans le fichier.</summary>
    public static List<int> LinesWithoutCurrency(CatalogInterpretation d, string target)
    {
        var moneyFields = MoneyFields(target);
        return
        [
            .. d.DataRows.Where(r => CatalogInterpretation.RowImportable(r) || r.Status == CatalogStatuses.Ambiguous)
                .Where(r => r.Cells.Any(c => moneyFields.Contains(c.Field) && d.Columns[c.C].Importable && c.Input.Trim().Length > 0)
                    && !r.Cells.Any(c => c.Field == "currency" && d.Columns[c.C].Importable && c.Value != null))
                .Select(r => r.Line),
        ];
    }

    // ------------------------------------------------------------------ exemples des montants

    /// <summary>
    /// Statistiques sur TOUTES les lignes et exemples représentatifs choisis par le serveur (première ligne, minimum, médiane,
    /// maximum, avertissements, illisibles) d'une colonne de montant ; l'empreinte lie une décision aux exemples servis.
    /// </summary>
    public static (JsonObject Examples, string Digest) MoneyExamples(CatalogInterpretation d, int column, string? currency)
    {
        var cells = d.DataRows.Select(r => (r.Line, Cell: r.Cells.FirstOrDefault(c => c.C == column), Raw: d.Document.Records.First(x => x.Line == r.Line).Cell(column)))
            .ToList();
        var readable = new List<(int Line, string Raw, decimal Value, string Status)>();
        var unreadable = new List<(int Line, string Raw)>();
        var empty = 0;
        foreach (var (line, cell, raw) in cells)
        {
            var input = cell?.Input ?? raw;
            if (input.Trim().Length == 0)
            {
                empty++;
                continue;
            }

            var (value, status, _) = CatalogInterpretationValidator.Cell("purchasePriceHt", input, null);
            if (value != null && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                readable.Add((line, input, amount, status));
            }
            else
            {
                unreadable.Add((line, input));
            }
        }

        var ordered = readable.OrderBy(x => x.Value).ThenBy(x => x.Line).ToList();
        var picks = new List<(string Role, int Line, string Raw, string? Value)>();
        void Pick(string role, (int Line, string Raw, decimal Value, string Status)? x)
        {
            if (x is { } p)
            {
                picks.Add((role, p.Line, p.Raw, p.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        Pick("first", readable.Count > 0 ? readable.MinBy(x => x.Line) : null);
        Pick("min", ordered.Count > 0 ? ordered[0] : null);
        Pick("median", ordered.Count > 0 ? ordered[ordered.Count / 2] : null);
        Pick("max", ordered.Count > 0 ? ordered[^1] : null);
        foreach (var w in readable.Where(x => x.Status == CatalogStatuses.Warning).Take(2))
        {
            picks.Add(("warning", w.Line, w.Raw, w.Value.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var u in unreadable.Take(3))
        {
            picks.Add(("unreadable", u.Line, u.Raw, null));
        }

        var examples = new JsonArray([.. picks.Select(p => (JsonNode?)new JsonObject { ["role"] = p.Role, ["line"] = p.Line, ["original"] = p.Raw, ["value"] = p.Value })]);
        var stats = new JsonObject
        {
            ["column"] = column,
            ["header"] = d.Columns[column].Header,
            ["rows"] = cells.Count,
            ["readable"] = readable.Count,
            ["unreadable"] = unreadable.Count,
            ["empty"] = empty,
            ["min"] = ordered.Count > 0 ? ordered[0].Value.ToString(CultureInfo.InvariantCulture) : null,
            ["median"] = ordered.Count > 0 ? ordered[ordered.Count / 2].Value.ToString(CultureInfo.InvariantCulture) : null,
            ["max"] = ordered.Count > 0 ? ordered[^1].Value.ToString(CultureInfo.InvariantCulture) : null,
            ["currency"] = currency,
            ["conversion"] = false,
            ["examples"] = examples,
        };
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(column.ToString(CultureInfo.InvariantCulture) + "|" + examples.ToJsonString() + "|" + stats["readable"] + "|" + stats["unreadable"])));
        return (stats, digest);
    }

    // ------------------------------------------------------------------ anomalies et actions licites

    /// <summary>Contexte de calcul des anomalies : destination, devise, rattachements catalogue connus (SKU existants).</summary>
    public sealed record IssueContext(string Target, string? Currency, IReadOnlyDictionary<string, string>? ExistingSkuModels = null, bool SupplierKnown = false);

    public static List<AssistantIssue> Issues(CatalogInterpretation d, AssistantUserLayer user, IssueContext context)
    {
        var issues = new List<AssistantIssue>();
        var dataRows = d.DataRows.ToList();
        static JsonObject T(params (string Key, JsonNode? Value)[] pairs)
        {
            var o = new JsonObject();
            foreach (var (k, v) in pairs)
            {
                o[k] = v;
            }

            return o;
        }

        // En-tête.
        if (d.HeaderStatus == CatalogStatuses.Ambiguous)
        {
            issues.Add(new AssistantIssue(
                "header", "header_uncertain", CatalogStatuses.Ambiguous, [d.Document.Records.Count > 0 ? d.Document.Records[0].Line : 1], null,
                [.. (d.Document.Records.Count > 0 ? d.Document.Records[0].Cells : [])], d.HeaderPresent ? "la ligne 1 contient des intitulés" : "la ligne 1 est un article",
                "Les règles et l'analyse ne s'accordent pas sur la présence d'une ligne d'intitulés.", d.HeaderAi, d.HeaderAi is null ? null : InterpretationOrigins.Ai,
                [
                    new(DecisionTypes.HeaderPresence, "La ligne 1 contient des intitulés", [], T(), T(("present", true))),
                    new(DecisionTypes.HeaderPresence, "La ligne 1 est un article", [], T(), T(("present", false))),
                ],
                [], Grouped: true));
        }

        // Colonnes (confirmations groupées : une décision vaut pour toutes les lignes).
        foreach (var c in d.Columns)
        {
            var money = CatalogLexicon.Money.Contains(c.Field) || c.RuleAlternatives.Any(CatalogLexicon.Money.Contains) || (c.Suggestion != null && CatalogLexicon.Money.Contains(c.Suggestion));
            var pending = c.Status == CatalogStatuses.Ambiguous;
            var useful = c.Status == CatalogStatuses.Unrecognized && c.UserRevision == null && c.Profile.N > 0 && (c.Suggestion != null || c.RuleAlternatives.Length > 0);
            if (!pending && !useful)
            {
                continue;
            }

            var choices = new List<string>();
            foreach (var f in c.RuleAlternatives.Prepend(c.Field).Append(c.Suggestion ?? "unknown").Append(c.RuleField ?? "unknown"))
            {
                if (f is not ("unknown" or "priceUnqualified" or "salePriceTtc") && CatalogLexicon.AllFieldKeys.Contains(f) && !choices.Contains(f))
                {
                    choices.Add(f);
                }
            }

            if (money)
            {
                // Montant : choix SANS présélection, parmi les seuls champs HT importables ; « ne pas importer » toujours possible.
                choices = context.Target == AssistantTargets.Lenses ? ["purchasePriceHt"] : ["purchasePriceHt", "salePriceHt"];
            }

            var actions = new List<AssistantAction>();
            JsonObject? examples = null;
            string? digest = null;
            if (money)
            {
                (examples, digest) = MoneyExamples(d, c.C, context.Currency);
            }

            foreach (var f in choices)
            {
                var requires = f == "purchasePriceHt" ? new[] { Permissions.CostsWrite } : f == "salePriceHt" ? [Permissions.PricingManual] : [];
                actions.Add(new AssistantAction(DecisionTypes.ColumnField, money ? MoneyLabel(f, examples?["readable"]?.GetValue<int>() ?? 0) : $"Importer comme « {f} »", requires, T(("column", c.C)),
                    T(("field", f), ("examplesDigest", digest))));
            }

            actions.Add(new AssistantAction(DecisionTypes.ColumnField, "Ne pas importer cette colonne", [], T(("column", c.C)), T(("field", Ignore))));
            var notOffered = new List<AssistantNotOffered>();
            if (money)
            {
                notOffered.Add(new(DecisionTypes.ColumnField + ":salePriceTtc", "MMV enregistre des prix HT ; aucune conversion TTC → HT n'est faite"));
                notOffered.Add(new("confirm_all", "les montants se confirment colonne par colonne, après lecture des exemples"));
            }

            issues.Add(new AssistantIssue(
                $"col:{c.C}", money ? "money_column" : useful ? "column_unrecognized" : "column_ambiguous", pending ? CatalogStatuses.Ambiguous : CatalogStatuses.Unrecognized,
                [], c.C, [c.Header, .. c.Profile.Values.Take(4)], c.Field == "unknown" ? null : c.Field, string.Join(" ; ", c.Reasons.Skip(1).DefaultIfEmpty(c.Reasons.FirstOrDefault() ?? string.Empty)),
                money ? null : c.Suggestion ?? (c.Origin == InterpretationOrigins.Ai ? c.Field : null), money ? null : c.Suggestion != null ? InterpretationOrigins.AiSuggestion : c.Origin,
                actions, notOffered, Examples: examples, ExamplesDigest: digest, Grouped: true));
        }

        // Familles (classes stables, Q-CSVAI-10 : aucune famille sur supposition).
        foreach (var cls in dataRows.Where(r => r.FamilyStatus == CatalogStatuses.Ambiguous && r.ContinuationOf == null && r.FamilyClass != null).GroupBy(r => r.FamilyClass!))
        {
            var lines = cls.Select(r => r.Line).ToList();
            var suggestion = cls.Select(r => r.FamilySuggestion).FirstOrDefault(s => s != null);
            issues.Add(new AssistantIssue(
                $"family:{cls.Key}", "family_unconfirmed", CatalogStatuses.Ambiguous, lines, null, [.. cls.Take(3).Select(r => Describe(d, r))],
                cls.First().Family, cls.First().Reasons.FirstOrDefault(x => x.StartsWith("famille", StringComparison.Ordinal)) ?? "famille à confirmer",
                suggestion, suggestion is null ? null : InterpretationOrigins.AiSuggestion,
                [.. CatalogLexicon.FamilyCodes.Select(f => new AssistantAction(DecisionTypes.FamilyClass, $"{FamilyLabel(f)} pour les {lines.Count} lignes", [], T(("classKey", cls.Key)), T(("family", f))))],
                [new("auto_family", "aucune famille n'est attribuée sur supposition (ni par MMV, ni par l'IA)")], Grouped: true));
        }

        // Continuation.
        var continuation = dataRows.Where(r => r.ContinuationOf != null).ToList();
        if (continuation.Count > 0 && user.Continuation is not { Accept: true })
        {
            issues.Add(new AssistantIssue(
                "continuation", "continuation_rows", CatalogStatuses.Ambiguous, [.. continuation.Select(r => r.Line)], null,
                [.. continuation.Take(3).Select(r => $"L{r.Line} ← L{r.ContinuationOf}")], "identité reprise de la ligne précédente",
                "Ces lignes n'ont ni marque ni modèle : MMV propose de reprendre l'identité de l'article qui les précède (aucune autre valeur reprise).", null, null,
                [
                    new(DecisionTypes.Continuation, $"Confirmer la reprise pour les {continuation.Count} lignes", [], T(), T(("accept", true))),
                    new(DecisionTypes.Continuation, "Refuser la reprise (lignes à corriger ou ignorer)", [], T(), T(("accept", false))),
                ],
                [], Grouped: true));
        }

        // Valeur par défaut de fichier : politique de stock (produits) ou approvisionnement (verres).
        var policyColumn = d.Columns.Any(c => c.Field == "stockPolicy" && c.Importable);
        var needsPolicy = !policyColumn && !user.FileDefaults.ContainsKey("stockPolicy")
            && dataRows.Any(r => CatalogInterpretation.RowImportable(r) || r.Status == CatalogStatuses.Ambiguous)
            && (context.Target == AssistantTargets.Lenses || dataRows.Any(r => r.Family != Families.Services));
        if (needsPolicy)
        {
            var values = context.Target == AssistantTargets.Lenses ? new[] { ("stocked", "Verres tenus en stock"), ("on_order", "Verres commandés à la demande") }
                : [("stocked", "Articles stockés"), ("order_only", "Articles sur commande")];
            issues.Add(new AssistantIssue(
                "default:stockPolicy", "default_required", CatalogStatuses.Ambiguous, [], null, [], null,
                context.Target == AssistantTargets.Lenses ? "Le fichier n'indique pas si les verres sont stockés ou commandés." : "Le fichier n'indique pas la politique de stock des articles.",
                null, null, [.. values.Select(v => new AssistantAction(DecisionTypes.FileDefault, v.Item2, [], T(("field", "stockPolicy")), T(("value", v.Item1))))],
                [new("auto_default", "aucune valeur par défaut n'est choisie sans vous")], Grouped: true));
        }

        // Devise des montants (COR-CSVAI-03) : une devise absente du fichier n'est jamais tenue pour fournie ; la devise du
        // magasin est PROPOSÉE et doit être confirmée pour les montants concernés ; aucune conversion ; jamais d'EUR par défaut.
        var withoutCurrency = LinesWithoutCurrency(d, context.Target);
        if (withoutCurrency.Count > 0 && ConfirmedCurrency(user, context.Currency) is null)
        {
            var moneyFields = MoneyFields(context.Target);
            var moneyColumns = d.Columns.Where(c => c.Importable && moneyFields.Contains(c.Field)).ToList();
            var ignoreActions = moneyColumns.Select(c => new AssistantAction(DecisionTypes.ColumnField, $"Ne pas importer la colonne « {c.Header} »", [], T(("column", c.C)), T(("field", Ignore))));
            var stale = user.FileDefaults.TryGetValue("currency", out var previous) ? previous.Value : null;
            if (context.Currency is { } storeCurrency)
            {
                var requires = moneyColumns.Select(c => c.Field == "salePriceHt" ? Permissions.PricingManual : Permissions.CostsWrite).Distinct().ToArray();
                issues.Add(new AssistantIssue(
                    "default:currency", "currency_confirmation_required", CatalogStatuses.Ambiguous, withoutCurrency, null, [.. moneyColumns.Select(c => c.Header)], storeCurrency,
                    (stale is null ? string.Empty : $"La devise confirmée ({stale}) n'est plus celle du magasin. ")
                        + $"Le fichier n'indique pas la devise de {withoutCurrency.Count} ligne(s) portant un montant. MMV propose la devise du magasin ({storeCurrency}) : à confirmer, sans aucune conversion.",
                    null, null,
                    [
                        new AssistantAction(DecisionTypes.FileDefault, $"Les montants sont en {storeCurrency} (devise du magasin) pour les {withoutCurrency.Count} lignes", requires,
                            T(("field", "currency")), T(("value", storeCurrency))),
                        .. ignoreActions,
                    ],
                    [new("auto_default", "la devise du magasin n'est jamais appliquée sans votre confirmation"), new("conversion", "aucune conversion de devise")], Grouped: true));
            }
            else
            {
                issues.Add(new AssistantIssue(
                    "default:currency", StoreCurrencyUnknown, CatalogStatuses.Invalid, withoutCurrency, null, [.. moneyColumns.Select(c => c.Header)], null,
                    $"Devise du magasin introuvable (configuration) : les montants de {withoutCurrency.Count} ligne(s) ne peuvent pas être importés. MMV n'utilise jamais une devise par défaut.",
                    null, null, [.. ignoreActions],
                    [new("auto_default", "aucune devise par défaut (EUR ou autre) n'est utilisée"), new("conversion", "aucune conversion de devise")], Grouped: true));
            }
        }

        // Doublons de références (diagnostic serveur).
        foreach (var f in new[] { "sku", "supplierReference", "manufacturerReference", "barcode" })
        {
            foreach (var g in dataRows.SelectMany(r => r.Cells.Where(c => c.Field == f && c.Reason is "référence en double dans le fichier" or "EAN en double dans le fichier").Select(c => (Row: r, Cell: c)))
                .GroupBy(x => f == "barcode" ? x.Cell.Input.Trim() : CatalogKeys.Normalize(x.Cell.Input.Trim())))
            {
                var rows = g.Select(x => x.Row).ToList();
                var column = g.First().Cell.C;
                var diagnosis = Diagnose(d, f, column, rows, context);
                var actions = new List<AssistantAction>();
                foreach (var r in rows)
                {
                    actions.Add(new AssistantAction(DecisionTypes.CellValue, $"Modifier la valeur de la ligne {r.Line}", [], T(("line", r.Line), ("column", column)),
                        T(("value", null), ("unique", true))));
                    actions.Add(new AssistantAction(DecisionTypes.RowSkip, $"Ignorer la ligne {r.Line}", [], T(("line", r.Line))));
                }

                if (diagnosis.Any(x => x.Cause == "column_mapping" && x.Retained))
                {
                    actions.Add(new AssistantAction(DecisionTypes.ColumnField, "Ne pas importer cette colonne comme " + f, [], T(("column", column)), T(("field", Ignore))));
                }

                issues.Add(new AssistantIssue(
                    $"dup:{f}:{g.Key}", f == "barcode" ? "barcode_duplicate" : f == "sku" ? "sku_duplicate" : "reference_duplicate", CatalogStatuses.Invalid,
                    [.. rows.Select(r => r.Line)], column, [.. g.Select(x => x.Cell.Raw)], g.Key,
                    $"La valeur « {g.First().Cell.Input.Trim()} » apparaît sur {rows.Count} lignes : une {Label(f)} identifie un seul article.", null, null, actions,
                    [
                        new("merge", "aucune fusion automatique de lignes ou de produits"),
                        new("generate_sku", f == "sku" ? "le SKU est présent dans le fichier : MMV ne le remplace pas et n'en propose pas" : "MMV ne propose aucune valeur de référence"),
                        new("link_existing", "association automatique à un article existant refusée (décision du propriétaire du 06/11/2026)"),
                    ],
                    diagnosis));
            }
        }

        // Lignes : cellules bloquantes et lignes structurellement invalides.
        foreach (var r in dataRows.Where(r => CatalogStatuses.Blocking(r.Status)))
        {
            foreach (var cell in r.Cells.Where(c => CatalogStatuses.Blocking(c.Status) && d.Columns[c.C].Importable && c.Reason is not ("référence en double dans le fichier" or "EAN en double dans le fichier")
                && !(c.Reason ?? string.Empty).StartsWith("reprise", StringComparison.Ordinal)))
            {
                var money = CatalogLexicon.Money.Contains(cell.Field);
                var actions = new List<AssistantAction>
                {
                    new(DecisionTypes.CellValue, "Corriger la valeur", money ? cell.Field == "salePriceHt" ? [Permissions.PricingManual] : [Permissions.CostsWrite] : [], T(("line", r.Line), ("column", cell.C)), T(("value", null))),
                    new(DecisionTypes.RowSkip, "Ignorer la ligne", [], T(("line", r.Line))),
                };
                if (money && cell.Reason?.Contains("séparateur", StringComparison.Ordinal) == true)
                {
                    actions.Add(new(DecisionTypes.DecimalSeparator, "La virgule est le séparateur décimal du fichier", [], T(), T(("separator", ","))));
                    actions.Add(new(DecisionTypes.DecimalSeparator, "Le point est le séparateur décimal du fichier", [], T(), T(("separator", "."))));
                }

                issues.Add(new AssistantIssue(
                    $"cell:{r.Line}:{cell.C}", cell.Status == CatalogStatuses.Invalid ? "cell_invalid" : "cell_ambiguous", cell.Status, [r.Line], cell.C,
                    [cell.Raw], cell.Value, cell.Reason ?? "valeur à vérifier", null, null, actions,
                    [new("guess", "aucune valeur n'est devinée ni corrigée en silence")]));
            }

            if (r.Structural || r.Reasons.Any(x => x.StartsWith("identité", StringComparison.Ordinal)))
            {
                issues.Add(new AssistantIssue(
                    $"row:{r.Line}", r.Structural ? "row_misaligned" : "row_identity", r.Status, [r.Line], null, [.. d.Document.Records.First(x => x.Line == r.Line).Cells],
                    null, string.Join(" ; ", r.Reasons.Where(x => x.StartsWith("ligne", StringComparison.Ordinal) || x.StartsWith("identité", StringComparison.Ordinal))), null, null,
                    [
                        new(DecisionTypes.RowSkip, "Ignorer la ligne", [], T(("line", r.Line))),
                        .. d.Columns.Where(c => c.Importable && CatalogLexicon.IdentityText.Contains(c.Field)).Select(c =>
                            new AssistantAction(DecisionTypes.CellValue, $"Saisir « {c.Header} »", [], T(("line", r.Line), ("column", c.C)), T(("value", null)))),
                    ],
                    [new("shift", "aucune cellule n'est décalée pour « réaligner » une ligne")]));
            }
        }

        // Groupes : doublons potentiels de déclinaison, déclinaisons non distinguées.
        foreach (var g in d.Groups.Where(g => g.Reasons.Any(x => x.StartsWith("doublon", StringComparison.Ordinal) || x.StartsWith("déclinaisons", StringComparison.Ordinal))))
        {
            var duplicate = g.Reasons.Any(x => x.StartsWith("doublon", StringComparison.Ordinal));
            issues.Add(new AssistantIssue(
                $"group:{g.Lines[0]}", duplicate ? "variant_duplicate" : "variants_undistinguished", CatalogStatuses.Ambiguous, [.. g.Lines], null,
                [.. g.Lines.Take(4).Select(l => Describe(d, d.Rows.First(r => r.Line == l)))], null, string.Join(" ; ", g.Reasons), null, null,
                [
                    .. g.Lines.Select(l => new AssistantAction(DecisionTypes.RowSkip, $"Ignorer la ligne {l}", [], T(("line", l)))),
                    .. duplicate ? [] : d.Columns.Where(c => c.Status == CatalogStatuses.Unrecognized && c.Profile.N > 0).Select(c =>
                        new AssistantAction(DecisionTypes.ColumnField, $"Utiliser « {c.Header} » comme libellé de déclinaison", [], T(("column", c.C)), T(("field", "variantLabel")))),
                ],
                [new("merge", "aucune fusion automatique de lignes")]));
        }

        return issues;
    }

    /// <summary>Diagnostic d'un doublon : chaque cause testée et affichée comme retenue ou écartée, avec sa preuve.</summary>
    public static List<AssistantDiagnosis> Diagnose(CatalogInterpretation d, string field, int column, IReadOnlyList<RowDecision> rows, IssueContext context)
    {
        var causes = new List<AssistantDiagnosis>();
        var variantFields = new HashSet<string>(["color", "variantLabel", "eyeSize", "bridgeSize", "templeLength", "frameDimensions", "power", "baseCurve", "diameter", "packSize", "volumeMl", "lensTint", "barcode"]);
        string Signature(RowDecision r, bool variantsOnly) => string.Join("|", r.Cells.Where(c => c.C != column && (!variantsOnly || variantFields.Contains(c.Field))).OrderBy(c => c.C).Select(c => c.Input.Trim()));
        var distinctVariants = rows.Select(r => Signature(r, true)).Distinct().Count();
        causes.Add(new("in_file_conflict", distinctVariants > 1,
            distinctVariants > 1 ? $"{rows.Count} lignes, {distinctVariants} déclinaisons ou EAN différents pour la même valeur" : "les déclinaisons et EAN des lignes concernées sont identiques"));
        var profile = d.Columns[column].Profile;
        var lowDiversity = profile.N >= 4 && (profile.Distinct <= 2 || profile.Distinct < 0.5 * profile.N);
        var otherNotion = profile.Share("G") >= 0.8 ? "valeurs au format code-barres" : profile.Values.Count(v => v.Trim().Contains(' ', StringComparison.Ordinal)) >= 0.6 * profile.N ? "valeurs en plusieurs mots (libellés)" : null;
        causes.Add(new("column_mapping", lowDiversity || otherNotion != null,
            lowDiversity ? $"colonne à faible diversité : {profile.Distinct} valeurs distinctes sur {profile.N} lignes" : otherNotion ?? $"colonne diversifiée : {profile.Distinct} valeurs distinctes sur {profile.N} lignes"));
        if (field == "sku" && context.ExistingSkuModels is { } existing)
        {
            var value = CatalogKeys.Normalize(rows[0].Cells.First(c => c.C == column).Input.Trim());
            causes.Add(new("catalog_existing", existing.ContainsKey(value),
                existing.TryGetValue(value, out var model) ? $"le SKU existe déjà au catalogue (modèle « {model} ») : la ligne le MODIFIERA si le modèle est le même, sinon conflit" : "aucun article du catalogue ne porte ce SKU"));
        }

        var continued = rows.Where(r => r.ContinuationOf != null).ToList();
        causes.Add(new("continuation", continued.Count > 0,
            continued.Count > 0 ? $"identité reprise d'une ligne de continuation : {string.Join(", ", continued.Select(r => $"L{r.Line} ← L{r.ContinuationOf}"))}" : "aucune ligne concernée n'est une continuation"));
        var identical = rows.Select(r => Signature(r, false)).Distinct().Count() == 1;
        causes.Add(new("true_duplicate", identical, identical ? "toutes les valeurs reconnues des lignes sont identiques" : "les lignes diffèrent sur au moins une valeur reconnue"));
        return causes;
    }

    // ------------------------------------------------------------------ lignes du tableau et compteurs

    public static readonly string[] Filters = ["all", "valid", "corrections", "ambiguous", "unrecognized", "duplicates"];

    public static Dictionary<string, int> Counters(CatalogInterpretation d, IReadOnlyCollection<AssistantIssue> issues)
    {
        var rows = d.Rows.Where(r => r.Kind is "data" or "skipped" or "comment").ToList();
        var duplicateLines = DuplicateLines(d, issues);
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["all"] = rows.Count,
            ["valid"] = rows.Count(r => r.Kind == "data" && !CatalogStatuses.Blocking(r.Status)),
            ["corrections"] = rows.Count(r => r.Kind == "data" && r.Status == CatalogStatuses.Invalid),
            ["ambiguous"] = rows.Count(r => r.Kind == "data" && r.Status == CatalogStatuses.Ambiguous),
            ["unrecognized"] = rows.Count(r => HasUnrecognizedData(d, r)),
            ["duplicates"] = rows.Count(r => duplicateLines.Contains(r.Line)),
            ["skipped"] = rows.Count(r => r.Kind == "skipped"),
        };
    }

    private static HashSet<int> DuplicateLines(CatalogInterpretation d, IEnumerable<AssistantIssue> issues) =>
        [.. issues.Where(i => i.Key.StartsWith("dup:", StringComparison.Ordinal) || i.Code == "variant_duplicate").SelectMany(i => i.Lines)];

    private static bool HasUnrecognizedData(CatalogInterpretation d, RowDecision r)
    {
        var record = d.Document.Records.First(x => x.Line == r.Line);
        return r.Kind == "data" && d.Columns.Any(c => c.Status == CatalogStatuses.Unrecognized && record.Cell(c.C).Trim().Length > 0);
    }

    public static (int Total, List<AssistantRowView> Rows) Rows(CatalogInterpretation d, IReadOnlyCollection<AssistantIssue> issues, string filter, int page, int pageSize, bool costsVisible)
    {
        var duplicateLines = DuplicateLines(d, issues);
        var issueByLine = issues.SelectMany(i => i.Lines.Select(l => (l, i.Key))).GroupBy(x => x.l).ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());
        var rows = d.Rows.Where(r => r.Kind is "data" or "skipped" or "comment").Where(r => filter switch
        {
            "valid" => r.Kind == "data" && !CatalogStatuses.Blocking(r.Status),
            "corrections" => r.Kind == "data" && r.Status == CatalogStatuses.Invalid,
            "ambiguous" => r.Kind == "data" && r.Status == CatalogStatuses.Ambiguous,
            "unrecognized" => HasUnrecognizedData(d, r),
            "duplicates" => duplicateLines.Contains(r.Line),
            _ => true,
        }).ToList();
        var views = rows.Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).Select(r =>
        {
            string? Value(string field) => r.Cells.FirstOrDefault(c => c.Field == field)?.Value;
            var cells = r.Cells.Select(c =>
            {
                var money = CatalogLexicon.Money.Contains(c.Field);
                var hidden = money && !costsVisible;
                return new AssistantCellView(c.C, c.Field, hidden ? "•••" : c.Raw, c.CorrectedRevision is null ? null : hidden ? "•••" : c.Input, hidden ? null : c.Value, c.Status, c.Reason, c.SourceLine, money);
            }).ToList();
            var flags = new List<string>();
            if (r.ContinuationOf != null)
            {
                flags.Add("continuation");
            }

            if (r.Cells.Any(c => c.CorrectedRevision != null))
            {
                flags.Add("corrected");
            }

            if (duplicateLines.Contains(r.Line))
            {
                flags.Add("duplicate");
            }

            return new AssistantRowView(
                r.Line, r.Kind, r.Kind == "skipped" ? "IGNOREE" : r.Status, r.Family, r.FamilyStatus, Value("modelName") ?? Value("modelDescription") ?? Value("lensRange"),
                Value("variantLabel") ?? Value("color") ?? Value("power"), Value("sku") ?? Value("supplierReference") ?? Value("manufacturerReference"), cells,
                [.. r.Reasons.Distinct()], issueByLine.GetValueOrDefault(r.Line) ?? [], r.ContinuationOf, flags);
        }).ToList();
        return (rows.Count, views);
    }

    // ------------------------------------------------------------------ projection d'écriture (produits)

    /// <summary>
    /// Lignes importables → champs d'écriture du catalogue (clés de <see cref="ImportService.Fields"/>, attributs
    /// <c>attr:</c> / <c>modelattr:</c>, montants <c>price:</c>). Libellé de déclinaison : colonne dédiée, sinon coloris,
    /// sinon puissance (une seule cellule, provenance affichée) ; sinon erreur. Politique : colonne, sinon choix de fichier,
    /// sinon « prestation » pour les seules prestations (seule politique admise par la famille).
    /// </summary>
    public static List<MaterializedRow> ProductRows(CatalogInterpretation d, AssistantUserLayer user, string? storeCurrency)
    {
        var result = new List<MaterializedRow>();
        var defaultPolicy = user.FileDefaults.TryGetValue("stockPolicy", out var p) ? p.Value : null;
        var confirmedCurrency = ConfirmedCurrency(user, storeCurrency);
        foreach (var r in d.DataRows.Where(CatalogInterpretation.RowImportable))
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var errors = new List<ImportError>();
            if (r.Family != null)
            {
                values["familyCode"] = r.Family;
            }

            foreach (var cell in r.Cells.Where(c => c.Value != null && d.Columns[c.C].Importable))
            {
                var f = cell.Field;
                switch (f)
                {
                    case "brand" or "modelName" or "modelDescription" or "variantLabel" or "color" or "sku" or "manufacturerReference" or "supplierReference" or "barcode"
                        or "supplierCode" or "lensManufacturer" or "lensRange" or "lensIndex":
                        values[f] = cell.Value!;
                        break;
                    case "lensTreatments":
                        values[f] = string.Join("|", cell.Value!.Split(['|', ',', ';', '+', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        break;
                    case "stockPolicy":
                        values[f] = ImportService.Policy(cell.Value) ?? cell.Value!;
                        break;
                    case "frameDimensions":
                        var parts = cell.Value!.Split('-');
                        values["attr:eyeSize"] = parts[0];
                        values["attr:bridgeSize"] = parts[1];
                        values["attr:templeLength"] = parts[2];
                        break;
                    case "purchasePriceHt" or "salePriceHt":
                        values["price:" + f] = cell.Value!;
                        break;
                    case "currency":
                        values["price:currency"] = cell.Value!;
                        break;
                    default:
                        if (ModelAttributes.Contains(f))
                        {
                            values["modelattr:" + f] = cell.Value!;
                        }
                        else if (VariantAttributes.Contains(f))
                        {
                            values["attr:" + f] = cell.Value!;
                        }

                        break;
                }
            }

            if (!values.ContainsKey("variantLabel"))
            {
                if (values.TryGetValue("color", out var color))
                {
                    values["variantLabel"] = color;
                }
                else if (values.TryGetValue("attr:power", out var power))
                {
                    values["variantLabel"] = power;
                }
                else
                {
                    errors.Add(new ImportError("variantLabel", "variant_label_source_missing"));
                }
            }

            if (!values.ContainsKey("stockPolicy"))
            {
                if (r.Family == Families.Services)
                {
                    values["stockPolicy"] = StockPolicies.Service;
                }
                else if (defaultPolicy != null)
                {
                    values["stockPolicy"] = defaultPolicy;
                }
                else
                {
                    errors.Add(new ImportError("stockPolicy", CatalogFieldErrors.Required));
                }
            }

            // Montant sans devise dans le fichier : devise du magasin seulement si CONFIRMÉE (COR-CSVAI-03).
            if ((values.ContainsKey("price:purchasePriceHt") || values.ContainsKey("price:salePriceHt")) && !values.ContainsKey("price:currency"))
            {
                if (confirmedCurrency is not null)
                {
                    values["price:currency"] = confirmedCurrency;
                }
                else
                {
                    errors.Add(new ImportError("currency", MissingCurrency(storeCurrency)));
                }
            }

            result.Add(new MaterializedRow(r.Line, values, errors));
        }

        return result;
    }

    // ------------------------------------------------------------------ outils

    private static string MoneyLabel(string field, int rows) => field switch
    {
        // Conséquence chiffrée : nombre de montants LISIBLES qui seront écrits (les illisibles restent à corriger à part).
        "purchasePriceHt" => $"Prix d'achat HT pour les {rows} lignes lisibles",
        "salePriceHt" => $"Prix de vente HT pour les {rows} lignes lisibles",
        _ => field,
    };

    public static string FamilyLabel(string code) => Families.Find(code)?.Label ?? code;

    private static string Label(string field) => field switch
    {
        "sku" => "référence interne (SKU)",
        "supplierReference" => "référence fournisseur",
        "manufacturerReference" => "référence fabricant",
        "barcode" => "code-barres",
        _ => field,
    };

    private static string Describe(CatalogInterpretation d, RowDecision r) =>
        $"L{r.Line} : " + string.Join(" · ", d.Document.Records.First(x => x.Line == r.Line).Cells.Where(c => c.Trim().Length > 0).Take(4).Select(c => c.Length > 40 ? c[..40] + "…" : c));
}
