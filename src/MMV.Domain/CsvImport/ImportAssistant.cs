namespace MMV.Domain.CsvImport;

/// <summary>Parcours d'un lot du nouvel assistant d'import (CSV-IMPORT-AI-01, migration 0009).</summary>
public static class AssistantPipelines
{
    public const string AssistantV2 = "assistant_v2";
}

/// <summary>Destination d'un lot de l'assistant : catalogue des produits ou référentiel des verres (offres fournisseurs).</summary>
public static class AssistantTargets
{
    public const string Products = "products";
    public const string Lenses = "lenses";

    public static IReadOnlyList<string> All { get; } = [Products, Lenses];
}

/// <summary>
/// États d'une tentative d'analyse par l'IA locale (Gate B § 8). <c>queued → running → terminal</c> seulement ; une tentative
/// terminale est figée, sauf l'enregistrement UNIQUE d'un résultat tardif sur une tentative annulée ou interrompue.
/// </summary>
public static class AnalysisStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Done = "done";
    public const string Incomplete = "incomplete";
    public const string Unavailable = "unavailable";
    public const string Timeout = "timeout";
    public const string InvalidOutput = "invalid_output";
    public const string Cancelled = "cancelled";
    public const string Interrupted = "interrupted";

    public static IReadOnlyList<string> All { get; } = [Queued, Running, Done, Incomplete, Unavailable, Timeout, InvalidOutput, Cancelled, Interrupted];

    public static bool IsActive(string status) => status is Queued or Running;

    /// <summary>Proposition exploitable (revalidée par les règles).</summary>
    public static bool IsUsable(string status) => status is Done or Incomplete;

    /// <summary>Échec qui exige un choix explicite : relancer ou vérifier sans IA (Q-CSVAI-08).</summary>
    public static bool RequiresChoice(string status) => status is Unavailable or Timeout or InvalidOutput or Cancelled or Interrupted;
}

/// <summary>Codes bornés des événements d'activité, chacun émis là où l'action a RÉELLEMENT eu lieu (jamais simulé).</summary>
public static class AnalysisEventCodes
{
    public const string FileReceived = "file_received";
    public const string EncodingDetected = "encoding_detected";
    public const string StructureRead = "structure_read";
    public const string RulesValidated = "rules_validated";
    public const string SamplePrepared = "sample_prepared";
    public const string OllamaQueued = "ollama_queued";
    public const string OllamaRunning = "ollama_running";
    public const string OllamaReceived = "ollama_received";
    public const string ValidationRunning = "validation_running";
    public const string AnalysisDone = "analysis_done";
    public const string OllamaUnavailable = "ollama_unavailable";
    public const string OllamaTimeout = "ollama_timeout";
    public const string OllamaInvalidOutput = "ollama_invalid_output";
    public const string AnalysisCancelled = "analysis_cancelled";
    public const string AnalysisInterrupted = "analysis_interrupted";
    public const string LateResultDiscarded = "late_result_discarded";
    public const string RulesOnlyChosen = "rules_only_chosen";
    public const string AnalysisDisabled = "analysis_disabled";

    public static IReadOnlyList<string> All { get; } =
    [
        FileReceived, EncodingDetected, StructureRead, RulesValidated, SamplePrepared, OllamaQueued, OllamaRunning, OllamaReceived, ValidationRunning,
        AnalysisDone, OllamaUnavailable, OllamaTimeout, OllamaInvalidOutput, AnalysisCancelled, AnalysisInterrupted, LateResultDiscarded, RulesOnlyChosen,
        AnalysisDisabled,
    ];
}

/// <summary>Types de décision de l'utilisateur (révisions append-only, revalidées par le serveur).</summary>
public static class DecisionTypes
{
    public const string ColumnField = "column_field";
    public const string HeaderPresence = "header_presence";
    public const string FamilyClass = "family_class";
    public const string Continuation = "continuation";
    public const string CellValue = "cell_value";
    public const string RowSkip = "row_skip";
    public const string RowRestore = "row_restore";
    public const string DecimalSeparator = "decimal_separator";
    public const string FileDefault = "file_default";
    public const string CreateReference = "create_reference";
    public const string RulesOnly = "rules_only";
    public const string Revoke = "revoke";

    public static IReadOnlyList<string> All { get; } =
        [ColumnField, HeaderPresence, FamilyClass, Continuation, CellValue, RowSkip, RowRestore, DecimalSeparator, FileDefault, CreateReference, RulesOnly, Revoke];
}

/// <summary>
/// Fichier d'origine d'un lot de l'assistant : octets exacts (bornés à 2 Mio), empreinte des octets, encodage et
/// séparateur détectés. Jamais réécrit ; seule modification permise : purge des octets par la maintenance (tracée),
/// empreinte, taille, encodage et séparateur conservés. Aucune suppression.
/// </summary>
public sealed class BatchOriginal
{
    public const int EncodingMaxLength = 16;
    public const int DelimiterMaxLength = 4;

    private BatchOriginal()
    {
        OriginalSha256 = string.Empty;
        Encoding = string.Empty;
        Delimiter = string.Empty;
    }

    public Guid BatchId { get; private set; }

    public string OriginalSha256 { get; private set; }

    public int ByteLength { get; private set; }

    public string Encoding { get; private set; }

    public string Delimiter { get; private set; }

    public byte[]? Bytes { get; private set; }

    public DateTimeOffset StoredAtUtc { get; private set; }

    public DateTimeOffset? PurgedAtUtc { get; private set; }

    public Guid? PurgedByUserId { get; private set; }

    public static BatchOriginal Create(Guid batchId, string sha256, byte[] bytes, string encoding, string delimiter, DateTimeOffset now)
    {
        if (bytes.Length > ImportBatch.MaxBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), "Original > 2 Mio.");
        }

        return new BatchOriginal
        {
            BatchId = batchId,
            OriginalSha256 = sha256,
            ByteLength = bytes.Length,
            Encoding = encoding,
            Delimiter = delimiter,
            Bytes = bytes,
            StoredAtUtc = now,
        };
    }
}

/// <summary>
/// État courant d'un lot du nouveau parcours : destination, révision courante des décisions, révision contrôlée, choix
/// tracé « vérifier sans IA ». Concurrence par version (<c>xmin</c>) ; jamais supprimé.
/// </summary>
public sealed class BatchAssistant
{
    private BatchAssistant()
    {
        Pipeline = AssistantPipelines.AssistantV2;
        Target = AssistantTargets.Products;
    }

    public Guid BatchId { get; private set; }

    public string Pipeline { get; private set; }

    public string Target { get; private set; }

    public int Revision { get; private set; }

    public int? CheckedRevision { get; private set; }

    public DateTimeOffset? RulesOnlyAtUtc { get; private set; }

    public Guid? RulesOnlyByUserId { get; private set; }

    /// <summary>Raison du mode « règles seules » : <c>user</c> (choix tracé) ou <c>configuration</c> (Mode = off).</summary>
    public string? RulesOnlyReason { get; private set; }

    /// <summary>Magasin dont le tarif reçoit les montants confirmés (magasin actif au dépôt) ; null : aucun montant importable.</summary>
    public Guid? StoreId { get; private set; }

    /// <summary>Synthèse du dernier contrôle à blanc (bornée), servie à l'étape 4 tant qu'aucune décision ne l'a périmée.</summary>
    public string? CheckSummaryJson { get; private set; }

    public uint Version { get; private set; }

    public bool RulesOnly => RulesOnlyAtUtc is not null;

    public static BatchAssistant Create(Guid batchId, string target, Guid? storeId) => new() { BatchId = batchId, Target = target, Revision = 1, StoreId = storeId };

    /// <summary>Révision suivante (nouvelle décision) : le contrôle précédent devient périmé.</summary>
    public int Advance() => ++Revision;

    public void MarkChecked(int revision, string summaryJson)
    {
        CheckedRevision = revision;
        CheckSummaryJson = summaryJson;
    }

    /// <summary>
    /// Résultat de l'application (compteurs réellement enregistrés) joint à la synthèse du contrôle, écrit dans LA
    /// transaction d'application AVANT la clôture du lot : un rejeu de la même requête relit exactement ce résultat.
    /// </summary>
    public void RecordApplied(string summaryJson) => CheckSummaryJson = summaryJson;

    public void ChooseRulesOnly(Guid userId, DateTimeOffset now, string reason)
    {
        if (RulesOnlyAtUtc is null)
        {
            RulesOnlyAtUtc = now;
            RulesOnlyByUserId = userId;
            RulesOnlyReason = reason;
        }
    }
}

/// <summary>
/// Tentative d'analyse par l'IA locale (une ligne par tentative, jamais réécrite une fois terminale). Le modèle actif
/// n'est renseigné qu'à la réception de la réponse d'Ollama (champ <c>model</c> de la réponse) ; aucune trace de
/// raisonnement n'est conservée. Sortie brute bornée à 64 Kio.
/// </summary>
public sealed class BatchAnalysis
{
    public const int RawOutputMaxLength = 65536;
    public const int ModelMaxLength = 100;
    public const int ErrorMaxLength = 200;

    private BatchAnalysis()
    {
        Contract = string.Empty;
        ConfiguredModel = string.Empty;
        Status = AnalysisStatuses.Queued;
        CoverageJson = "{}";
    }

    public Guid Id { get; private set; }

    public Guid BatchId { get; private set; }

    public int Attempt { get; private set; }

    public int BasedOnRevision { get; private set; }

    public string Contract { get; private set; }

    public string ConfiguredModel { get; private set; }

    /// <summary>Modèle CONFIRMÉ par la réponse d'Ollama (null tant qu'aucune réponse n'est reçue).</summary>
    public string? Model { get; private set; }

    public string? ModelDigest { get; private set; }

    public string? OllamaVersion { get; private set; }

    public string? PromptSha256 { get; private set; }

    public string? SampleSha256 { get; private set; }

    public string Status { get; private set; }

    public DateTimeOffset QueuedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    public long? DurationMs { get; private set; }

    public int? EvalCount { get; private set; }

    public string CoverageJson { get; private set; }

    public string? RawOutput { get; private set; }

    public string? ProposalJson { get; private set; }

    public string? Error { get; private set; }

    public bool LateResultDiscarded { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public uint Version { get; private set; }

    public static BatchAnalysis Queue(Guid id, Guid batchId, int attempt, int basedOnRevision, string contract, string configuredModel, Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = id,
            BatchId = batchId,
            Attempt = attempt,
            BasedOnRevision = basedOnRevision,
            Contract = contract,
            ConfiguredModel = configuredModel,
            RequestedByUserId = userId,
            QueuedAtUtc = now,
        };

    public void Start(DateTimeOffset now, string promptSha256, string sampleSha256, string coverageJson, string? modelDigest, string? ollamaVersion)
    {
        Require(Status == AnalysisStatuses.Queued, "Tentative non en file.");
        Status = AnalysisStatuses.Running;
        StartedAtUtc = now;
        PromptSha256 = promptSha256;
        SampleSha256 = sampleSha256;
        CoverageJson = coverageJson;
        ModelDigest = modelDigest;
        OllamaVersion = ollamaVersion;
    }

    /// <summary>Réponse conforme : <c>done</c> (couverture complète) ou <c>incomplete</c>.</summary>
    public void Complete(DateTimeOffset now, bool complete, string model, string? rawOutput, string proposalJson, int? evalCount)
    {
        Require(Status == AnalysisStatuses.Running, "Tentative non en cours.");
        Status = complete ? AnalysisStatuses.Done : AnalysisStatuses.Incomplete;
        Model = Bound(model, ModelMaxLength);
        RawOutput = Bound(rawOutput, RawOutputMaxLength);
        ProposalJson = proposalJson;
        EvalCount = evalCount;
        End(now);
    }

    /// <summary>Échec technique : indisponible, délai, sortie hors contrat (le modèle répondant est conservé s'il est connu).</summary>
    public void Fail(DateTimeOffset now, string status, string? model, string? rawOutput, string? error, int? evalCount)
    {
        // Depuis « queued » : seulement l'indisponibilité constatée avant le démarrage (file pleine).
        Require((Status == AnalysisStatuses.Running && status is AnalysisStatuses.Unavailable or AnalysisStatuses.Timeout or AnalysisStatuses.InvalidOutput)
            || (Status == AnalysisStatuses.Queued && status == AnalysisStatuses.Unavailable), "Échec non permis.");
        Status = status;
        Model = Bound(model, ModelMaxLength);
        RawOutput = Bound(rawOutput, RawOutputMaxLength);
        Error = Bound(error, ErrorMaxLength);
        EvalCount = evalCount;
        End(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        Require(AnalysisStatuses.IsActive(Status), "Tentative non active.");
        Status = AnalysisStatuses.Cancelled;
        End(now);
    }

    /// <summary>Redémarrage de l'API pendant une tentative active (T14).</summary>
    public void Interrupt(DateTimeOffset now)
    {
        Require(AnalysisStatuses.IsActive(Status), "Tentative non active.");
        Status = AnalysisStatuses.Interrupted;
        End(now);
    }

    /// <summary>Résultat arrivé après annulation ou interruption (T13) : conservé pour la traçabilité, SANS effet.</summary>
    public void RecordLateResult(string? model, string? rawOutput)
    {
        Require(Status is AnalysisStatuses.Cancelled or AnalysisStatuses.Interrupted && !LateResultDiscarded, "Résultat tardif non permis.");
        LateResultDiscarded = true;
        Model ??= Bound(model, ModelMaxLength);
        RawOutput ??= Bound(rawOutput, RawOutputMaxLength);
    }

    private void End(DateTimeOffset now)
    {
        EndedAtUtc = now;
        DurationMs = (long)((now - (StartedAtUtc ?? QueuedAtUtc)).TotalMilliseconds);
    }

    private static string? Bound(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

/// <summary>Événement d'activité réel d'une tentative (ajout seul). Ordre = identifiant croissant.</summary>
public sealed class BatchAnalysisEvent
{
    public const int DataMaxLength = 4096;

    private BatchAnalysisEvent()
    {
        Code = string.Empty;
        DataJson = "{}";
    }

    public long Id { get; private set; }

    /// <summary>Tentative concernée ; null pour un événement du lot (réception, lecture, règles, choix « sans IA »).</summary>
    public Guid? AnalysisId { get; private set; }

    public Guid BatchId { get; private set; }

    public DateTimeOffset AtUtc { get; private set; }

    public string Code { get; private set; }

    public string DataJson { get; private set; }

    public static BatchAnalysisEvent Create(Guid? analysisId, Guid batchId, DateTimeOffset now, string code, string dataJson)
    {
        if (!AnalysisEventCodes.All.Contains(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Code d'événement inconnu.");
        }

        return new BatchAnalysisEvent { AnalysisId = analysisId, BatchId = batchId, AtUtc = now, Code = code, DataJson = dataJson.Length <= DataMaxLength ? dataJson : "{}" };
    }
}

/// <summary>
/// Décision de l'utilisateur = UNE révision (numéro unique par lot), ajout seul : type, cible, valeur, empreinte des
/// exemples servis (montants), auteur, horodatage. Révoquer = nouvelle décision <c>revoke</c> qui désigne la précédente.
/// </summary>
public sealed class BatchDecision
{
    public const int JsonMaxLength = 4096;

    private BatchDecision()
    {
        Type = string.Empty;
        TargetJson = "{}";
        ValueJson = "{}";
    }

    public Guid Id { get; private set; }

    public Guid BatchId { get; private set; }

    public int Revision { get; private set; }

    public string Type { get; private set; }

    public string TargetJson { get; private set; }

    public string ValueJson { get; private set; }

    public string? ExamplesDigest { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid RequestId { get; private set; }

    public static BatchDecision Create(
        Guid id, Guid batchId, int revision, string type, string targetJson, string valueJson, string? examplesDigest, Guid? supersedesId, Guid userId, DateTimeOffset now,
        Guid requestId)
    {
        if (!DecisionTypes.All.Contains(type) || targetJson.Length > JsonMaxLength || valueJson.Length > JsonMaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Décision hors contrat.");
        }

        return new BatchDecision
        {
            Id = id,
            BatchId = batchId,
            Revision = revision,
            Type = type,
            TargetJson = targetJson,
            ValueJson = valueJson,
            ExamplesDigest = examplesDigest,
            SupersedesId = supersedesId,
            AuthorUserId = userId,
            CreatedAtUtc = now,
            RequestId = requestId,
        };
    }
}
