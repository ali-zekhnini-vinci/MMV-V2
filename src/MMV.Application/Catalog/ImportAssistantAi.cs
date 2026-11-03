namespace MMV.Application.Catalog;

/// <summary>Issue d'une analyse par l'assistant IA local (codes bornés, servis au poste et tracés).</summary>
public static class AssistantStatuses
{
    /// <summary>Analyse effectuée, propositions validées par le serveur.</summary>
    public const string Used = "used";

    /// <summary>Correspondance déjà complète par les règles ou le modèle du fournisseur : aucune inférence lancée.</summary>
    public const string NotNeeded = "not_needed";

    /// <summary>Assistant désactivé par la configuration du serveur (ou point d'accès non local refusé).</summary>
    public const string Disabled = "disabled";

    /// <summary>Service local injoignable ou modèle absent : moteur déterministe seul.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>Délai d'inférence dépassé : moteur déterministe seul.</summary>
    public const string Timeout = "timeout";

    /// <summary>Réponse hors schéma : ignorée en totalité, moteur déterministe seul.</summary>
    public const string InvalidOutput = "invalid_output";
}

/// <summary>Anomalies sémantiques qu'un modèle peut signaler sur une colonne (liste FERMÉE ; aucun texte libre servi).</summary>
public static class AssistantWarnings
{
    public const string DecimalComma = "decimal_comma";
    public const string CurrencyInValues = "currency_in_values";
    public const string UnitInValues = "unit_in_values";
    public const string MixedContent = "mixed_content";
    public const string ValuesMismatchHeader = "values_mismatch_header";
    public const string PossibleDuplicateColumn = "possible_duplicate_column";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DecimalComma, CurrencyInValues, UnitInValues, MixedContent, ValuesMismatchHeader, PossibleDuplicateColumn,
    };
}

/// <summary>Colonne transmise au modèle : en-tête et quelques valeurs d'exemple, tronquées (DONNÉES non fiables).</summary>
public sealed record AssistantColumn(string Header, IReadOnlyList<string> Samples);

/// <summary>Champ MMV proposé au modèle : clé technique, obligation, description métier.</summary>
public sealed record AssistantField(string Key, bool Required, string Description);

/// <summary>
/// Demande d'analyse GLOBALE d'un fichier (une seule inférence par fichier, jamais une par ligne). <paramref name="FromSupplier"/> :
/// fichier d'un fournisseur désigné au dépôt (ses codes article sont SES références, pas le SKU interne du magasin).
/// </summary>
public sealed record AssistantRequest(string Kind, IReadOnlyList<AssistantColumn> Columns, IReadOnlyList<AssistantField> Fields, bool FromSupplier = false);

/// <summary>Proposition brute du modèle pour une colonne : champ (null = ignorer), confiance high / medium / low.</summary>
public sealed record AssistantProposal(string Header, string? Field, string Confidence);

/// <summary>Anomalie signalée sur une colonne (code borné).</summary>
public sealed record AssistantWarning(string Header, string Code);

/// <summary>Résultat d'une analyse : statut, modèle, durée d'inférence, propositions et anomalies (non encore validées).</summary>
public sealed record AssistantAnalysis(
    string Status, string? Model, long InferenceMs, IReadOnlyList<AssistantProposal> Proposals, IReadOnlyList<AssistantWarning> Warnings)
{
    public static AssistantAnalysis Failed(string status, string? model, long inferenceMs = 0) => new(status, model, inferenceMs, [], []);
}

/// <summary>État du service servi au poste (aucun détail réseau).</summary>
public sealed record AssistantServiceStatus(bool Enabled, bool Available, string? Model);

/// <summary>
/// Assistant IA LOCAL de correspondance des colonnes (UX-IA, 03/11/2026). Implémentation sur le serveur du magasin
/// (jamais sur les postes, jamais un service en ligne). Contrat : il PROPOSE, le serveur VALIDE
/// (<see cref="ImportAssistantMerge"/>) ; il ne reçoit que les en-têtes et quelques exemples tronqués, ne renvoie qu'une
/// correspondance au schéma JSON strict, n'écrit rien et n'exécute rien.
/// </summary>
public interface IImportMappingAssistant
{
    Task<AssistantAnalysis> AnalyzeAsync(AssistantRequest request, CancellationToken cancellationToken);

    Task<AssistantServiceStatus> StatusAsync(CancellationToken cancellationToken);
}

/// <summary>Synthèse de l'analyse servie avec l'aperçu : moteur utilisé, durées mesurées, anomalies validées.</summary>
public sealed record ImportAnalysis(string AssistantStatus, string? AssistantModel, long ParseMs, long AssistantMs, IReadOnlyList<AssistantWarning> Warnings);

/// <summary>
/// Préparation et validation des propositions de l'assistant IA (UX-IA). Règles :
/// <list type="bullet">
/// <item>les correspondances du modèle fournisseur et les synonymes exacts ne sont JAMAIS remplacées ;</item>
/// <item>une proposition n'est retenue que si la colonne existe telle quelle dans le fichier et si le champ appartient au
/// type d'import ; doublons de champ départagés par la confiance, ex aequo écartés ; confiance « low » écartée ;</item>
/// <item>un champ MONÉTAIRE (prix d'achat, coefficient, prix de vente) n'est jamais appliqué sur la seule foi de
/// l'assistant : il est présenté « à confirmer », préchoisi (aucune valeur commerciale décidée sans l'utilisateur) ;</item>
/// <item>les valeurs ne sont jamais modifiées : seule la correspondance colonne → champ est proposée ;</item>
/// <item>une proposition dont les VALEURS d'exemple sont incompatibles avec le champ (code de famille ou de politique
/// inconnu, code-barres non numérique, montant illisible, devise ou pays mal formés…) est écartée (contrôle déterministe).</item>
/// </list>
/// </summary>
public static class ImportAssistantMerge
{
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";

    public const int MaxColumns = 60;
    public const int MaxSamples = 4;
    public const int MaxSampleLength = 40;
    public const int MaxHeaderLength = 80;

    /// <summary>Descriptions métier des champs (français, sans valeur d'exemple inventée).</summary>
    public static IReadOnlyDictionary<string, string> Descriptions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["familyCode"] = "famille de produit (monture optique, solaire, verre, lentille, accessoire, prestation)",
        ["brand"] = "marque commerciale du produit",
        ["modelName"] = "nom ou désignation du modèle",
        ["modelDescription"] = "description longue du modèle",
        ["variantLabel"] = "libellé de la déclinaison (coloris, calibre, conditionnement)",
        ["color"] = "couleur ou coloris",
        ["sku"] = "code article INTERNE du magasin (SKU)",
        ["manufacturerReference"] = "référence du fabricant / de la marque",
        ["barcode"] = "code-barres EAN / GTIN / UPC",
        ["stockPolicy"] = "politique de stock (stocké, sur commande, prestation)",
        ["lensManufacturer"] = "fabricant du verre ophtalmique",
        ["lensRange"] = "gamme du verre ophtalmique",
        ["lensIndex"] = "indice de réfraction du verre",
        ["lensTreatments"] = "traitements du verre",
        ["supplierCode"] = "code du fournisseur",
        ["supplierReference"] = "référence de l'article CHEZ LE FOURNISSEUR",
        ["primarySupplier"] = "indicateur fournisseur principal (oui / non)",
        ["code"] = "code court du fournisseur",
        ["name"] = "raison sociale du fournisseur",
        ["countryCode"] = "pays (code ISO à deux lettres)",
        ["contactName"] = "nom du contact",
        ["email"] = "adresse e-mail",
        ["phone"] = "téléphone",
        ["addressLine"] = "adresse postale (rue)",
        ["postalCode"] = "code postal",
        ["city"] = "ville",
        ["notes"] = "remarques",
        ["purchasePriceHt"] = "prix d'ACHAT hors taxes (prix net fournisseur, coût)",
        ["coefficient"] = "coefficient multiplicateur appliqué au prix d'achat",
        ["salePriceHt"] = "prix de VENTE hors taxes au client",
        ["currency"] = "devise des montants (EUR, MAD…)",
    };

    /// <summary>Demande au modèle : colonnes (exemples non vides, distincts, tronqués), champs du type d'import.</summary>
    public static AssistantRequest BuildRequest(string kind, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, bool fromSupplier = false)
    {
        var columns = new List<AssistantColumn>();
        for (var c = 0; c < headers.Count && c < MaxColumns; c++)
        {
            var samples = rows.Where(r => c < r.Length).Select(r => Clean(r[c], MaxSampleLength)).Where(v => v.Length > 0)
                .Distinct(StringComparer.Ordinal).Take(MaxSamples).ToList();
            columns.Add(new AssistantColumn(headers[c], samples));
        }

        var fields = ImportService.Fields[kind].Select(f => new AssistantField(f.Key, f.Required, Descriptions.GetValueOrDefault(f.Key, f.Key))).ToList();
        return new AssistantRequest(kind, columns, fields, fromSupplier);
    }

    /// <summary>
    /// Faut-il interroger l'assistant ? Oui s'il reste une colonne non reconnue, une ambiguïté ou un champ requis absent.
    /// <paramref name="reviewed"/> : en-têtes du fichier ayant servi au modèle fournisseur appris — une colonne qu'il ignorait
    /// a déjà été examinée (pas de nouvelle inférence pour elle).
    /// </summary>
    public static bool Needed(string kind, IReadOnlyList<string> headers, IReadOnlyList<MappingSuggestion> suggestions, IReadOnlyCollection<string>? reviewed = null)
    {
        var used = suggestions.Select(s => s.Header).Concat(reviewed ?? []).ToHashSet(StringComparer.Ordinal);
        var mapped = suggestions.Where(s => s.Confidence != MatchConfidence.Ambiguous).Select(s => s.Field).ToHashSet(StringComparer.Ordinal);
        return headers.Any(h => !used.Contains(h))
            || suggestions.Any(s => s.Confidence == MatchConfidence.Ambiguous)
            || ImportService.Fields[kind].Any(f => f.Required && !mapped.Contains(f.Key) && f.Key is not ("familyCode" or "stockPolicy"));
    }

    /// <summary>Valeurs non vides des premières lignes, par en-tête (contrôle de compatibilité des propositions).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Samples(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var samples = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        for (var c = 0; c < headers.Count; c++)
        {
            samples.TryAdd(headers[c], [.. rows.Where(r => c < r.Length).Select(r => r[c].Trim()).Where(v => v.Length > 0).Take(50)]);
        }

        return samples;
    }

    /// <summary>
    /// Valeurs d'exemple compatibles avec le champ proposé ? Contrôle déterministe borné aux champs à format reconnaissable ;
    /// les champs libres (libellés, références, marques) sont toujours compatibles. Colonne vide : compatible.
    /// </summary>
    public static bool ValuesFit(string field, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return true;
        }

        return field switch
        {
            "familyCode" => values.All(v => ImportService.FamilyCode(v) is not null),
            "stockPolicy" => values.All(v => ImportService.Policy(v) is not null),
            "primarySupplier" => values.All(v => ImportService.Boolean(v) is not null),
            "barcode" => values.All(v => v.Length is >= 8 and <= 14 && v.All(char.IsAsciiDigit)),
            "purchasePriceHt" or "salePriceHt" or "coefficient" => values.All(v => NumberText.Normalize(v, null, null) is { Value: not null } or { Error: NumberText.AmbiguousNumber }),
            "currency" => values.All(v => v.Length == 3 && v.All(char.IsAsciiLetter)),
            "countryCode" => values.All(v => v.Length == 2 && v.All(char.IsAsciiLetter)),
            "email" => values.All(v => v.Contains('@', StringComparison.Ordinal)),
            "lensIndex" => values.All(v => NumberText.Normalize(v, null, null).Value is not null),
            _ => true,
        };
    }

    /// <summary>Propositions finales : règles déterministes d'abord, puis propositions VALIDÉES de l'assistant.</summary>
    public static IReadOnlyList<MappingSuggestion> Merge(
        string kind, IReadOnlyList<string> headers, IReadOnlyList<MappingSuggestion> rules, AssistantAnalysis analysis,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? samples = null)
    {
        if (analysis.Status != AssistantStatuses.Used)
        {
            return rules;
        }

        var fields = ImportService.Fields[kind].Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var valid = Validate(analysis.Proposals, headers, fields, samples);
        var result = rules.ToDictionary(s => s.Field, StringComparer.Ordinal);
        var byHeader = rules.GroupBy(s => s.Header, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var proposal in valid.OrderByDescending(p => p.Confidence == High))
        {
            var field = proposal.Field!;
            if (byHeader.TryGetValue(proposal.Header, out var existing))
            {
                var rule = existing[0];
                if (rule.Confidence is MatchConfidence.Profile or MatchConfidence.Exact)
                {
                    continue; // règle sûre : jamais remplacée
                }

                if (rule.Confidence == MatchConfidence.Probable && rule.Field == field)
                {
                    // Règle probable confirmée par l'assistant (sauf montant : reste « à vérifier », déjà appliquée par les règles).
                    result[field] = rule with { Confidence = Monetary(field) ? MatchConfidence.Probable : MatchConfidence.Assistant, Source = MatchSources.Assistant };
                    continue;
                }

                if (rule.Confidence == MatchConfidence.Ambiguous && rule.Alternatives.Contains(field, StringComparer.Ordinal) && !TakenElsewhere(result, field, proposal.Header))
                {
                    // Ambiguïté levée : choix de l'assistant en tête ; appliqué seulement hors montant et en confiance haute.
                    result.Remove(rule.Field);
                    var applied = !Monetary(field) && proposal.Confidence == High;
                    result[field] = new MappingSuggestion(field, proposal.Header, applied ? MatchConfidence.Assistant : MatchConfidence.Ambiguous,
                        [field, .. rule.Alternatives.Where(a => a != field)], MatchSources.Assistant);
                }

                continue; // contradiction avec une règle probable : la règle reste, rien n'est imposé
            }

            if (result.ContainsKey(field))
            {
                continue; // champ déjà pris par une autre colonne
            }

            var confidence = Monetary(field) ? MatchConfidence.Ambiguous : proposal.Confidence == High ? MatchConfidence.Assistant : MatchConfidence.Probable;
            IReadOnlyList<string> alternatives = Monetary(field)
                ? [field, .. ImportService.MoneyFields.Where(m => m != field && fields.Contains(m) && !result.ContainsKey(m)).Order(StringComparer.Ordinal)]
                : [];
            result[field] = new MappingSuggestion(field, proposal.Header, confidence, alternatives, MatchSources.Assistant);
            byHeader[proposal.Header] = [result[field]];
        }

        var order = ImportService.Fields[kind].Select(f => f.Key).ToList();
        return [.. result.Values.OrderBy(s => order.IndexOf(s.Field))];
    }

    /// <summary>Anomalies retenues : colonne existante, code de la liste fermée, une par colonne et par code.</summary>
    public static IReadOnlyList<AssistantWarning> ValidWarnings(IReadOnlyList<AssistantWarning> warnings, IReadOnlyList<string> headers) =>
        [.. warnings.Where(w => headers.Contains(w.Header, StringComparer.Ordinal) && AssistantWarnings.All.Contains(w.Code)).DistinctBy(w => (w.Header, w.Code)).Take(MaxColumns)];

    /// <summary>Propositions conformes : colonne du fichier, champ du type, confiance connue et non « low » ; doublons départagés.</summary>
    private static List<AssistantProposal> Validate(
        IReadOnlyList<AssistantProposal> proposals, IReadOnlyList<string> headers, HashSet<string> fields, IReadOnlyDictionary<string, IReadOnlyList<string>>? samples)
    {
        // Une colonne décrite plusieurs fois par le modèle : réponse incohérente pour cette colonne, écartée entièrement.
        var repeated = proposals.GroupBy(p => p.Header, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var candidates = proposals
            .Where(p => p.Field is not null && fields.Contains(p.Field) && headers.Contains(p.Header, StringComparer.Ordinal) && p.Confidence is High or Medium
                && !repeated.Contains(p.Header) && (samples is null || ValuesFit(p.Field, samples.GetValueOrDefault(p.Header) ?? [])))
            .ToList();
        var kept = new List<AssistantProposal>();
        foreach (var group in candidates.GroupBy(p => p.Field, StringComparer.Ordinal))
        {
            var best = group.Where(p => p.Confidence == High).ToList() is { Count: > 0 } high ? high : [.. group];
            // Même champ pour plusieurs colonnes « color » / « variantLabel » admis par les règles, pas par l'assistant.
            if (best.Count == 1)
            {
                kept.Add(best[0]);
            }
        }

        return kept;
    }

    private static bool Monetary(string field) => ImportService.MoneyFields.Contains(field);

    private static bool TakenElsewhere(Dictionary<string, MappingSuggestion> result, string field, string header) =>
        result.TryGetValue(field, out var other) && other.Header != header && other.Confidence != MatchConfidence.Ambiguous;

    /// <summary>Texte transmis au modèle : caractères de contrôle retirés, longueur bornée.</summary>
    public static string Clean(string? value, int max)
    {
        var text = new string([.. (value ?? string.Empty).Where(c => !char.IsControl(c))]).Trim();
        return text.Length <= max ? text : text[..max];
    }
}

/// <summary>Origine d'une proposition de correspondance.</summary>
public static class MatchSources
{
    public const string Rules = "rules";
    public const string Assistant = "assistant";
}
