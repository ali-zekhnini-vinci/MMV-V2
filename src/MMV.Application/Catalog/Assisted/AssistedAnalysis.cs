using System.Diagnostics;
using System.Text.Json.Nodes;
using MMV.Domain.CsvImport;

namespace MMV.Application.Catalog.Assisted;

/// <summary>
/// Configuration du nouveau parcours (section <c>CatalogInterpreter</c>, Gate B § 10), SÉPARÉE de <c>ImportAssistant</c>
/// (parcours actuel, <c>llama3.2</c>, inchangé). <see cref="Mode"/> : <c>always</c> (défaut, décision du 06/11/2026),
/// <c>unresolved_only</c>, <c>off</c> (règles seules, tracé et visible). Délai provisoire 120 s, sans valeur de SLA.
/// Aucun téléchargement de modèle. <see cref="WizardEnabled"/> : activation contrôlée de l'écran (ancien parcours conservé).
/// </summary>
public sealed class AssistedImportOptions
{
    public const string SectionName = "CatalogInterpreter";

    public bool Enabled { get; set; } = true;

    public string Mode { get; set; } = AnalysisModes.Always;

    public string Endpoint { get; set; } = "http://127.0.0.1:11434";

    public string Model { get; set; } = "qwen3.5:4b";

    public int TimeoutSeconds { get; set; } = 120;

    public string KeepAlive { get; set; } = "30m";

    public int MaxSampleRows { get; set; } = CatalogInterpretationContract.MaxSample;

    public int QueueCapacity { get; set; } = 8;

    public bool WizardEnabled { get; set; }
}

public static class AnalysisModes
{
    public const string Always = "always";
    public const string UnresolvedOnly = "unresolved_only";
    public const string Off = "off";
}

/// <summary>Demande d'interprétation (tout est calculé par le serveur ; le modèle ne voit que l'échantillon borné).</summary>
public sealed record InterpreterRequest(CsvDocument Document, CatalogSample Sample, string SystemPrompt, string UserMessage, JsonObject Schema);

/// <summary>
/// Résultat d'une interprétation : <c>done</c> (proposition lue strictement, modèle CONFIRMÉ par la réponse d'Ollama),
/// <c>unavailable</c>, <c>timeout</c>, <c>invalid_output</c>. Aucune trace de raisonnement (champ <c>thinking</c> ignoré).
/// </summary>
public sealed record InterpreterResult(string Status, string? Model, string? RawOutput, CatalogProposal? Proposal, int? EvalCount, string? Error, long ElapsedMs);

/// <summary>État d'Ollama relevé au début d'une tentative (ou servi à l'écran) : jamais un « modèle actif ».</summary>
public sealed record InterpreterProbe(bool Reachable, string? OllamaVersion, bool ModelInstalled, string? InstalledDigest);

/// <summary>Interpréteur de catalogue (IA locale) : boucle locale seulement, délai, annulation, lecture stricte.</summary>
public interface ICatalogInterpreter
{
    string ConfiguredModel { get; }

    Task<InterpreterProbe> ProbeAsync(CancellationToken cancellationToken);

    Task<InterpreterResult> InterpretAsync(InterpreterRequest request, CancellationToken cancellationToken);
}

/// <summary>File d'analyse (une analyse à la fois par serveur) : position réelle, annulation d'une tentative en cours.</summary>
public interface IAnalysisScheduler
{
    /// <summary>Faux si la file est pleine (<c>503 analysis_queue_full</c>).</summary>
    bool TryEnqueue(Guid analysisId);

    /// <summary>Interrompt la requête Ollama de la tentative si elle est en cours (l'état est déjà <c>cancelled</c> en base).</summary>
    void Cancel(Guid analysisId);
}

/// <summary>Empreinte, taille, encodage et séparateur de l'original ; <see cref="Purged"/> : octets purgés (jamais automatiquement).</summary>
public sealed record OriginalInfo(string Sha256, int ByteLength, string Encoding, string Delimiter, bool Purged);

/// <summary>Persistance du nouveau parcours (migration 0009) ; enregistrement par <see cref="ICatalogUnitOfWork"/>.</summary>
public interface IImportAssistantStore
{
    void Add(BatchOriginal original);

    void Add(BatchAssistant assistant);

    void Add(BatchAnalysis analysis);

    void Add(BatchAnalysisEvent analysisEvent);

    void Add(BatchDecision decision);

    Task<BatchAssistant?> FindAssistantAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>Descripteur de l'original SANS ses octets (lecture fréquente : suivi de l'analyse).</summary>
    Task<OriginalInfo?> FindOriginalAsync(Guid batchId, CancellationToken cancellationToken);

    Task<BatchAnalysis?> FindAnalysisAsync(Guid id, CancellationToken cancellationToken);

    Task<BatchAnalysis?> LatestAnalysisAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>Dernière tentative terminée avec une proposition exploitable (<c>done</c> ou <c>incomplete</c>).</summary>
    Task<BatchAnalysis?> LatestUsableAnalysisAsync(Guid batchId, CancellationToken cancellationToken);

    Task<int> AnalysisCountAsync(Guid batchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BatchAnalysisEvent>> EventsAsync(Guid analysisId, long after, int limit, CancellationToken cancellationToken);

    /// <summary>Événements du lot hors tentative (réception, lecture, règles, choix « sans IA »).</summary>
    Task<IReadOnlyList<BatchAnalysisEvent>> BatchEventsAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>Tentatives en file ou en cours (reprise après redémarrage : interruption).</summary>
    Task<IReadOnlyList<BatchAnalysis>> ActiveAnalysesAsync(CancellationToken cancellationToken);

    /// <summary>Position réelle dans la file : tentatives actives enregistrées avant celle-ci.</summary>
    Task<int> QueuePositionAsync(BatchAnalysis analysis, CancellationToken cancellationToken);

    Task<IReadOnlyList<BatchDecision>> DecisionsAsync(Guid batchId, CancellationToken cancellationToken);

    Task<BatchDecision?> FindDecisionByRequestAsync(Guid batchId, Guid requestId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, BatchAnalysis>> LatestAnalysesAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken);

    Task<bool> IsAssistedAsync(Guid batchId, CancellationToken cancellationToken);

    Task<(int Total, DateTimeOffset? LastUploadedAtUtc, int Resumable)> BatchSummaryAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Exécution d'UNE tentative d'analyse (C1.5), hors requête HTTP : préparation de l'échantillon, appel de l'interpréteur,
/// enregistrement par comparaison-échange (version <c>xmin</c>). Une tentative annulée ou interrompue pendant l'appel ne
/// devient JAMAIS « terminée » : sa sortie tardive est conservée pour la traçabilité, sans effet (T13). Chaque événement
/// est écrit à l'endroit où l'action a réellement eu lieu.
/// </summary>
public sealed class AssistedAnalysisRunner(
    ICatalogUnitOfWork unitOfWork, IImportStore imports, IImportAssistantStore store, ICatalogInterpreter interpreter, TimeProvider clock, AssistedImportOptions options)
{
    public async Task RunAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        var analysis = await store.FindAnalysisAsync(analysisId, cancellationToken);
        if (analysis is null || analysis.Status != AnalysisStatuses.Queued)
        {
            return;
        }

        var batch = await imports.FindBatchAsync(analysis.BatchId, cancellationToken);
        var original = await store.FindOriginalAsync(analysis.BatchId, cancellationToken);
        if (batch is null || original is null)
        {
            return;
        }

        var document = TolerantCsvReader.Parse(batch.Content, original.Encoding);
        var sample = CatalogInterpretationContract.Sample(document, Math.Clamp(options.MaxSampleRows, 5, CatalogInterpretationContract.MaxSample));
        var systemPrompt = CatalogInterpretationContract.SystemPrompt();
        var userMessage = CatalogInterpretationContract.UserMessage(document, sample);
        var probe = await interpreter.ProbeAsync(cancellationToken);
        var now = CatalogClock.Now(clock);
        analysis.Start(now, CatalogInterpretationContract.Sha256(systemPrompt), CatalogInterpretationContract.Sha256(userMessage), sample.Coverage.ToJsonString(), probe.InstalledDigest,
            probe.OllamaVersion);
        Event(analysis, now, AnalysisEventCodes.SamplePrepared, new JsonObject
        {
            ["sampled"] = sample.Indexes.Count, ["total"] = document.Records.Count, ["complete"] = sample.Complete,
        });
        Event(analysis, now, AnalysisEventCodes.OllamaRunning, new JsonObject
        {
            ["model"] = interpreter.ConfiguredModel, ["ollamaVersion"] = probe.OllamaVersion, ["reachable"] = probe.Reachable, ["installed"] = probe.ModelInstalled,
        });
        if (!(await unitOfWork.SaveAsync(cancellationToken)).Saved)
        {
            // Annulée entre la mise en file et le démarrage : rien à faire.
            return;
        }

        InterpreterResult result;
        if (!probe.Reachable || !probe.ModelInstalled)
        {
            result = new InterpreterResult(AnalysisStatuses.Unavailable, null, null, null, null, probe.Reachable ? "model_not_installed" : "ollama_unreachable", 0);
        }
        else
        {
            try
            {
                result = await interpreter.InterpretAsync(new InterpreterRequest(document, sample, systemPrompt, userMessage, CatalogInterpretationContract.Schema(document)), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Annulation par l'utilisateur (déjà « cancelled » en base) ou arrêt du serveur (« interrupted » au redémarrage).
                return;
            }
        }

        await FinishAsync(analysisId, result, sample.Complete, document, cancellationToken);
    }

    private async Task FinishAsync(Guid analysisId, InterpreterResult result, bool complete, CsvDocument document, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var analysis = await store.FindAnalysisAsync(analysisId, cancellationToken);
            if (analysis is null)
            {
                return;
            }

            var now = CatalogClock.Now(clock);
            if (analysis.Status != AnalysisStatuses.Running)
            {
                if (analysis.Status is AnalysisStatuses.Cancelled or AnalysisStatuses.Interrupted && !analysis.LateResultDiscarded && result.RawOutput is not null)
                {
                    analysis.RecordLateResult(result.Model, result.RawOutput);
                    Event(analysis, now, AnalysisEventCodes.LateResultDiscarded, new JsonObject { ["status"] = analysis.Status, ["model"] = result.Model });
                    await unitOfWork.SaveAsync(cancellationToken);
                }

                return;
            }

            if (result.Status == AnalysisStatuses.Done && result.Proposal is { } proposal)
            {
                Event(analysis, now, AnalysisEventCodes.OllamaReceived, new JsonObject
                {
                    ["model"] = result.Model, ["digest"] = analysis.ModelDigest, ["elapsedMs"] = result.ElapsedMs, ["evalCount"] = result.EvalCount,
                });
                var watch = Stopwatch.StartNew();
                Event(analysis, now, AnalysisEventCodes.ValidationRunning, new JsonObject { ["rows"] = document.Records.Count });
                var interpretation = CatalogInterpretationValidator.Validate(document, proposal);
                analysis.Complete(now, complete, result.Model!, result.RawOutput, CatalogInterpretationContract.Serialize(proposal), result.EvalCount);
                Event(analysis, CatalogClock.Now(clock), AnalysisEventCodes.AnalysisDone, new JsonObject
                {
                    ["status"] = analysis.Status, ["validationMs"] = watch.ElapsedMilliseconds, ["confirmations"] = interpretation.Confirmations,
                    ["inconsistentFamilyRule"] = proposal.Inconsistent,
                });
            }
            else
            {
                var status = result.Status is AnalysisStatuses.Timeout or AnalysisStatuses.InvalidOutput ? result.Status : AnalysisStatuses.Unavailable;
                analysis.Fail(now, status, result.Model, result.RawOutput, result.Error, result.EvalCount);
                Event(analysis, now, status switch
                {
                    AnalysisStatuses.Timeout => AnalysisEventCodes.OllamaTimeout,
                    AnalysisStatuses.InvalidOutput => AnalysisEventCodes.OllamaInvalidOutput,
                    _ => AnalysisEventCodes.OllamaUnavailable,
                }, new JsonObject { ["error"] = result.Error, ["elapsedMs"] = result.ElapsedMs, ["timeoutSeconds"] = options.TimeoutSeconds });
            }

            var commit = await unitOfWork.SaveAsync(cancellationToken);
            if (commit.Saved || commit.Status != CommitStatus.VersionConflict)
            {
                return;
            }

            // Comparaison-échange perdue : annulée pendant l'écriture → relecture, puis résultat tardif sans effet.
        }
    }

    private void Event(BatchAnalysis analysis, DateTimeOffset now, string code, JsonObject data) =>
        store.Add(BatchAnalysisEvent.Create(analysis.Id, analysis.BatchId, now, code, data.ToJsonString()));
}
