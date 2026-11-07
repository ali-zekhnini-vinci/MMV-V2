using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;

namespace MMV.Application.Catalog.Assisted;

// ---------------------------------------------------------------------------------------------------- contrats (Gate B § 7)

public sealed record AssistantStatusView(bool Enabled, string Mode, string ConfiguredModel, bool OllamaReachable, string? OllamaVersion, bool ModelInstalled, string? InstalledDigest, bool WizardEnabled);

public sealed record InspectView(int ByteLength, string Sha256, string Encoding, string Delimiter, int LineCount, int ColumnCount, IReadOnlyList<IReadOnlyList<string>> Preview, string? SuggestedTarget, IReadOnlyList<string> Evidence, IReadOnlyList<string> Anomalies);

public sealed record AnalysisEventView(long Seq, DateTimeOffset AtUtc, string Code, JsonNode? Data);

public sealed record AnalysisModelView(string Configured, string? Active, string? Digest, string? OllamaVersion);

public sealed record ProgressView(string Label, int Done, int Total);

public sealed record AnalysisStateView(
    int Attempt, string Status, int? BasedOnRevision, int? QueuePosition, long ElapsedMs, AnalysisModelView Model, IReadOnlyList<AnalysisEventView> Events,
    IReadOnlyList<ProgressView> Progress, string? Indeterminate, JsonNode? Coverage, IReadOnlyList<JsonObject> Families, IReadOnlyList<string> StructureAnomalies,
    bool LateResultDiscarded, string? Error, bool RulesOnly, string? RulesOnlyReason, IReadOnlyList<AnalysisEventView> BatchEvents);

public sealed record StepView(string State, string? Sublabel);

public sealed record CreatedView(Guid BatchId, uint Version, int Revision, JsonObject Original, JsonObject Reader, AnalysisStateView Analysis);

public sealed record AssistantView(
    JsonObject Batch, IReadOnlyDictionary<string, StepView> Steps, AnalysisStateView Analysis, JsonObject Review, JsonNode? Check, IReadOnlyList<string> Permissions);

public sealed record RowsView(int Total, IReadOnlyDictionary<string, int> Counters, IReadOnlyList<AssistantRowView> Rows);

public sealed record DecisionInput(string? Type, JsonObject? Target, JsonObject? Value, string? ExamplesDigest);

public sealed record RevalidationView(int Revision, IReadOnlyDictionary<string, int> Counters, IReadOnlyList<string> Resolved, IReadOnlyList<string> Raised, bool CheckStale);

public sealed record AppliedView(int ProductsCreated, int VariantsCreated, int Updated, int Prices, int OffersCreated, int OffersUpdated, int ReferencesCreated, DateTimeOffset AppliedAtUtc, long ElapsedMs);

public sealed record ImportsSummaryView(int Total, DateTimeOffset? LastUploadedAtUtc, int Resumable);

/// <summary>
/// Orchestration du nouveau parcours (C2.1) : cinq étapes dont l'état est CALCULÉ par le serveur à chaque lecture
/// (rechargement, autre poste, « Reprendre » : même étape, mêmes décisions). Aucune règle métier côté poste. Réutilise le
/// plan et l'application du catalogue (<see cref="ImportService"/>) et du référentiel des verres (<see cref="AssistedLensPlanner"/>).
/// Les montants exigent une confirmation colonne par colonne portant l'empreinte des exemples servis ; l'application exige
/// un contrôle à jour, aucune analyse en cours, aucune anomalie bloquante, une confirmation explicite et les droits des champs.
/// </summary>
public sealed class AssistedImportService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IImportStore imports, IImportAssistantStore assistant, ImportService catalogImport, AssistedLensPlanner lensPlanner,
    ICatalogInterpreter interpreter, IAnalysisScheduler scheduler, AssistedImportOptions options, TimeProvider clock, IAuditRecorder audit)
{
    public const int MaxPageSize = 100;
    public const int MaxDecisionsPerRequest = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Données d'un lot nécessaires à une revalidation complète.</summary>
    private sealed record Context(
        ImportBatch Batch, BatchAssistant State, OriginalInfo Original, CsvDocument Document, IReadOnlyList<BatchDecision> Decisions, AssistantUserLayer User,
        BatchAnalysis? Latest, BatchAnalysis? Usable, CatalogInterpretation Interpretation, string? Currency);

    // ------------------------------------------------------------------ état du service et inspection

    public async Task<AssistantStatusView> StatusAsync(CancellationToken cancellationToken)
    {
        var probe = options.Enabled && options.Mode != AnalysisModes.Off ? await interpreter.ProbeAsync(cancellationToken) : new InterpreterProbe(false, null, false, null);
        return new AssistantStatusView(options.Enabled, options.Enabled ? options.Mode : AnalysisModes.Off, options.Model, probe.Reachable, probe.OllamaVersion, probe.ModelInstalled,
            probe.InstalledDigest, options.WizardEnabled);
    }

    /// <summary>Inspection SANS écriture : refus techniques seulement, aperçu brut, destination suggérée avec ses preuves.</summary>
    public static Outcome<InspectView> Inspect(string? contentBase64)
    {
        if (Bytes(contentBase64) is not { } bytes)
        {
            return Outcome.Refuse<InspectView>("invalid_base64");
        }

        var inspection = TolerantCsvReader.Inspect(bytes);
        if (inspection.Document is not { } document)
        {
            return Outcome.Refuse<InspectView>(inspection.Refusal!);
        }

        var (target, evidence) = SuggestTarget(document);
        var preview = document.Records.Take(8).Select(r => (IReadOnlyList<string>)[.. r.Cells.Select(c => c.Length <= 160 ? c : c[..160])]).ToList();
        return Outcome.Ok(new InspectView(
            inspection.ByteLength, inspection.Sha256, document.Encoding, document.DelimiterLabel, document.Records.Count, document.Columns, preview, target, evidence,
            [.. document.Records.SelectMany(r => r.Anomalies).Concat(document.Anomalies).Distinct()]));
    }

    /// <summary>Destination suggérée (jamais imposée) : intitulés propres aux offres de verres (plages, indice, gamme).</summary>
    public static (string? Target, List<string> Evidence) SuggestTarget(CsvDocument document)
    {
        if (document.Records.Count == 0)
        {
            return (null, []);
        }

        var lensHeaders = document.Records[0].Cells.Where(h =>
        {
            var t = CatalogLexicon.Tok(h);
            return t.Overlaps(["sph", "sphere", "cyl", "cylindre", "addition", "add", "indice", "gamme", "traitement", "traitements"]) && !CatalogLexicon.Suspicious(h);
        }).ToList();
        return lensHeaders.Count >= 3
            ? (AssistantTargets.Lenses, [.. lensHeaders.Select(h => $"intitulé « {h} »")])
            : (AssistantTargets.Products, ["intitulés de catalogue des produits"]);
    }

    private static byte[]? Bytes(string? base64)
    {
        if (string.IsNullOrEmpty(base64) || base64.Length > ((ImportBatch.MaxBytes + 2) / 3 * 4) + 8)
        {
            return base64 is { Length: > 0 } ? new byte[ImportBatch.MaxBytes + 1] : null;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ dépôt (étape 1 → 2)

    public async Task<Outcome<CreatedView>> CreateAsync(
        Guid requestId, string? fileName, string? contentBase64, Guid? supplierId, string? target, string? analysisMode, Guid? priceStoreId, CatalogActor actor,
        CancellationToken cancellationToken)
    {
        if (await imports.FindBatchByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.UploadedByUserId == actor.UserId && ImportService.IsAssistedBatch(existing)
                ? Outcome.Ok(await CreatedAsync(existing.Id, cancellationToken), replayed: true)
                : Outcome.Conflict<CreatedView>("request_id_reused");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var name = CatalogText.Clean(fileName, "fileName", ImportBatch.FileNameMaxLength, required: true, errors);
        if (target is null || !AssistantTargets.All.Contains(target))
        {
            errors["target"] = [target is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown];
        }

        if (analysisMode is not (null or "auto" or "rules_only"))
        {
            errors["analysis"] = [CatalogFieldErrors.Unknown];
        }

        if (errors.Count > 0)
        {
            return Outcome.Invalid<CreatedView>(errors);
        }

        if (supplierId is { } sid && await store.FindSupplierAsync(sid, cancellationToken) is null or { IsArchived: true })
        {
            return Outcome.Refuse<CreatedView>("supplier_unknown");
        }

        if (Bytes(contentBase64) is not { } bytes)
        {
            return Outcome.Refuse<CreatedView>("invalid_base64");
        }

        var watch = Stopwatch.StartNew();
        var inspection = TolerantCsvReader.Inspect(bytes);
        if (inspection.Document is not { } document)
        {
            return Outcome.Refuse<CreatedView>(inspection.Refusal!);
        }

        var readMs = watch.ElapsedMilliseconds;
        var now = CatalogClock.Now(clock);
        var batch = ImportBatch.Upload(
            Guid.CreateVersion7(now), ImportKinds.Catalog, null, name!, document.TextSha256, document.Text, document.Records.Count, ImportService.AssistedMapping(1), actor.UserId, now,
            requestId, supplierId);
        imports.Add(batch);
        assistant.Add(BatchOriginal.Create(batch.Id, inspection.Sha256, bytes, document.Encoding, document.DelimiterLabel, now));
        var state = BatchAssistant.Create(batch.Id, target!, priceStoreId);
        assistant.Add(state);
        BatchEvent(batch.Id, now, AnalysisEventCodes.FileReceived, new JsonObject { ["bytes"] = bytes.Length, ["sha256"] = inspection.Sha256 });
        BatchEvent(batch.Id, now, AnalysisEventCodes.EncodingDetected, new JsonObject { ["encoding"] = document.Encoding, ["delimiter"] = document.DelimiterLabel });
        BatchEvent(batch.Id, now, AnalysisEventCodes.StructureRead, new JsonObject
        {
            ["lines"] = document.Records.Count, ["columns"] = document.Columns, ["modalColumns"] = document.ModalColumns,
            ["irregular"] = document.Records.Count(r => r.Anomalies.Contains("fewer_cells") || r.Anomalies.Contains("more_cells")), ["elapsedMs"] = readMs,
        });
        var rules = CatalogInterpretationValidator.Validate(document, null, null, target!);
        BatchEvent(batch.Id, now, AnalysisEventCodes.RulesValidated, new JsonObject
        {
            ["rows"] = rules.DataRows.Count(), ["confirmations"] = rules.Confirmations, ["elapsedMs"] = watch.ElapsedMilliseconds - readMs,
        });

        BatchAnalysis? analysis = null;
        if (analysisMode == "rules_only")
        {
            state.ChooseRulesOnly(actor.UserId, now, "user");
            BatchEvent(batch.Id, now, AnalysisEventCodes.RulesOnlyChosen, new JsonObject { ["reason"] = "user" });
        }
        else if (!options.Enabled || options.Mode == AnalysisModes.Off)
        {
            state.ChooseRulesOnly(actor.UserId, now, "configuration");
            BatchEvent(batch.Id, now, AnalysisEventCodes.AnalysisDisabled, new JsonObject { ["reason"] = "configuration" });
        }
        else if (options.Mode == AnalysisModes.UnresolvedOnly && rules.Confirmations == 0)
        {
            state.ChooseRulesOnly(actor.UserId, now, "not_needed");
            BatchEvent(batch.Id, now, AnalysisEventCodes.AnalysisDisabled, new JsonObject { ["reason"] = "not_needed" });
        }
        else
        {
            analysis = Queue(batch.Id, 1, 1, actor.UserId, now);
        }

        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportUploaded, AuditActions.ImportEntity, batch.Id,
        [
            CatalogAudit.Field("kind", null, ImportKinds.Catalog), CatalogAudit.Field("pipeline", null, AssistantPipelines.AssistantV2), CatalogAudit.Field("target", null, target),
            CatalogAudit.Field("lineCount", null, document.Records.Count.ToString(CultureInfo.InvariantCulture)), CatalogAudit.Field("originalSha256", null, inspection.Sha256),
            CatalogAudit.Field("assistantStatus", null, analysis is null ? "rules_only" : AnalysisStatuses.Queued), CatalogAudit.Field("assistantModel", null, analysis is null ? null : options.Model),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_batches_creation_request_id" }
                && await imports.FindBatchByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.UploadedByUserId == actor.UserId
                ? Outcome.Ok(await CreatedAsync(replay.Id, cancellationToken), replayed: true)
                : Outcome.Conflict<CreatedView>();
        }

        if (analysis is not null)
        {
            await EnqueueAsync(analysis, cancellationToken);
        }

        return Outcome.Created(await CreatedAsync(batch.Id, cancellationToken));
    }

    private BatchAnalysis Queue(Guid batchId, int attempt, int revision, Guid userId, DateTimeOffset now)
    {
        var analysis = BatchAnalysis.Queue(Guid.CreateVersion7(now), batchId, attempt, revision, CatalogInterpretationContract.Version, options.Model, userId, now);
        assistant.Add(analysis);
        assistant.Add(BatchAnalysisEvent.Create(analysis.Id, batchId, now, AnalysisEventCodes.OllamaQueued, new JsonObject { ["attempt"] = attempt, ["model"] = options.Model }.ToJsonString()));
        return analysis;
    }

    /// <summary>Mise en file APRÈS l'enregistrement ; file pleine : tentative close « indisponible » (choix explicite proposé).</summary>
    private async Task EnqueueAsync(BatchAnalysis analysis, CancellationToken cancellationToken)
    {
        if (scheduler.TryEnqueue(analysis.Id))
        {
            return;
        }

        var tracked = await assistant.FindAnalysisAsync(analysis.Id, cancellationToken);
        if (tracked is { Status: AnalysisStatuses.Queued })
        {
            var now = CatalogClock.Now(clock);
            tracked.Fail(now, AnalysisStatuses.Unavailable, null, null, "analysis_queue_full", null);
            assistant.Add(BatchAnalysisEvent.Create(tracked.Id, tracked.BatchId, now, AnalysisEventCodes.OllamaUnavailable, new JsonObject { ["error"] = "analysis_queue_full" }.ToJsonString()));
            await unitOfWork.SaveAsync(cancellationToken);
        }
    }

    private void BatchEvent(Guid batchId, DateTimeOffset now, string code, JsonObject data) => assistant.Add(BatchAnalysisEvent.Create(null, batchId, now, code, data.ToJsonString()));

    private async Task<CreatedView> CreatedAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(batchId, cancellationToken);
        var c = context!;
        return new CreatedView(
            c.Batch.Id, c.Batch.Version, c.State.Revision,
            new JsonObject { ["sha256"] = c.Original.Sha256, ["byteLength"] = c.Original.ByteLength, ["encoding"] = c.Original.Encoding, ["delimiter"] = c.Original.Delimiter },
            new JsonObject
            {
                ["lines"] = c.Document.Records.Count, ["columns"] = c.Document.Columns,
                ["anomalies"] = new JsonArray([.. c.Document.Records.SelectMany(r => r.Anomalies).Concat(c.Document.Anomalies).Distinct().Select(a => (JsonNode?)JsonValue.Create(a))]),
            },
            await AnalysisViewAsync(c, 0, cancellationToken));
    }

    // ------------------------------------------------------------------ lecture et revalidation

    private async Task<Context?> LoadAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await imports.FindBatchAsync(batchId, cancellationToken);
        if (batch is null || !ImportService.IsAssistedBatch(batch))
        {
            return null;
        }

        var state = await assistant.FindAssistantAsync(batchId, cancellationToken);
        var original = await assistant.FindOriginalAsync(batchId, cancellationToken);
        if (state is null || original is null)
        {
            return null;
        }

        var decisions = await assistant.DecisionsAsync(batchId, cancellationToken);
        var user = AssistantUserLayer.From(decisions);
        var latest = await assistant.LatestAnalysisAsync(batchId, cancellationToken);
        var usable = await assistant.LatestUsableAnalysisAsync(batchId, cancellationToken);
        var (document, interpretation) = InterpretationCache.Get(batch, state, original.Encoding, usable, user);
        // Devise du magasin du lot : jamais d'EUR par défaut si le magasin ou sa devise sont introuvables (COR-CSVAI-03).
        var currency = state.StoreId is { } sid && (await store.GetStoreAsync(sid, cancellationToken))?.CurrencyCode is { Length: 3 } code ? code : null;
        return new Context(batch, state, original, document, decisions, user, latest, usable, interpretation, currency);
    }

    private static CatalogMaterializer.IssueContext IssueContext(Context c) => new(c.State.Target, c.Currency, null, c.Batch.SupplierId is not null);

    public async Task<Outcome<AssistantView>> GetAsync(Guid batchId, CatalogActor actor, CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<AssistantView>();
        }

        var issues = CatalogMaterializer.Issues(c.Interpretation, c.User, IssueContext(c));
        var counters = CatalogMaterializer.Counters(c.Interpretation, issues);
        var groups = new JsonArray([.. issues.Where(i => i.Grouped).Select(i => (JsonNode?)IssueSummary(i))]);
        var review = new JsonObject
        {
            ["counters"] = JsonSerializer.SerializeToNode(counters, Json),
            ["confirmations"] = c.Interpretation.Confirmations,
            ["blockers"] = JsonSerializer.SerializeToNode(c.Interpretation.Blockers, Json),
            ["groups"] = groups,
            ["issues"] = issues.Count,
            ["columns"] = new JsonArray([.. c.Interpretation.Columns.Select(col => (JsonNode?)new JsonObject
            {
                ["column"] = col.C, ["header"] = col.Header, ["field"] = col.Field, ["status"] = col.Status, ["origin"] = col.Origin, ["suggestion"] = col.Suggestion,
                ["aiField"] = col.AiField, ["userRevision"] = col.UserRevision, ["notWritten"] = col.Importable ? CatalogMaterializer.NotWrittenReason(col.Field, c.State.Target) : null,
            })]),
            ["decisions"] = new JsonArray([.. c.Decisions.OrderBy(d => d.Revision).Select(d => (JsonNode?)DecisionView(d))]),
        };
        var check = c.State.CheckSummaryJson is { } json && c.Batch.Status is ImportStatuses.Checked or ImportStatuses.Applied ? JsonNode.Parse(json) : null;
        if (check is JsonObject checkObject)
        {
            checkObject.Remove(PlanFingerprintKey);
            checkObject["stale"] = c.State.CheckedRevision != c.State.Revision;
        }

        var permissions = new List<string> { Permissions.CatalogImport };
        foreach (var p in new[] { Permissions.CostsRead, Permissions.CostsWrite, Permissions.PricingManual, Permissions.CatalogConfigure })
        {
            if (actor.Has(p))
            {
                permissions.Add(p);
            }
        }

        var analysis = await AnalysisViewAsync(c, 0, cancellationToken);
        return Outcome.Ok(new AssistantView(BatchView(c), Steps(c, check as JsonObject), analysis, review, check, permissions));
    }

    private static JsonObject BatchView(Context c) => new()
    {
        ["id"] = c.Batch.Id, ["status"] = c.Batch.Status, ["version"] = c.Batch.Version, ["revision"] = c.State.Revision, ["checkedRevision"] = c.State.CheckedRevision,
        ["target"] = c.State.Target, ["supplierId"] = c.Batch.SupplierId, ["fileName"] = c.Batch.FileName, ["uploadedAtUtc"] = c.Batch.UploadedAtUtc,
        ["rulesOnly"] = c.State.RulesOnly, ["rulesOnlyReason"] = c.State.RulesOnlyReason, ["currency"] = c.Currency, ["storeId"] = c.State.StoreId,
        ["original"] = new JsonObject { ["sha256"] = c.Original.Sha256, ["byteLength"] = c.Original.ByteLength, ["encoding"] = c.Original.Encoding, ["delimiter"] = c.Original.Delimiter },
        ["lines"] = c.Document.Records.Count, ["columns"] = c.Document.Columns,
    };

    private static JsonObject DecisionView(BatchDecision d) => new()
    {
        ["revision"] = d.Revision, ["type"] = d.Type, ["target"] = JsonNode.Parse(d.TargetJson), ["value"] = JsonNode.Parse(d.ValueJson), ["author"] = d.AuthorUserId,
        ["createdAtUtc"] = d.CreatedAtUtc,
    };

    private static JsonObject IssueSummary(AssistantIssue i) => new()
    {
        ["key"] = i.Key, ["code"] = i.Code, ["severity"] = i.Severity, ["lines"] = i.Lines.Count, ["column"] = i.Column, ["interpretation"] = i.Interpretation,
        ["explanation"] = i.Explanation, ["suggestion"] = i.Suggestion, ["suggestionOrigin"] = i.SuggestionOrigin,
    };

    /// <summary>État de chaque étape, calculé par le serveur (Gate B § 4, § 8).</summary>
    private static Dictionary<string, StepView> Steps(Context c, JsonObject? check)
    {
        var closed = c.Batch.Status is ImportStatuses.Applied or ImportStatuses.Cancelled;
        var a = c.Latest?.Status;
        var active = a is not null && AnalysisStatuses.IsActive(a);
        var reviewable = (a is not null && AnalysisStatuses.IsUsable(a)) || c.State.RulesOnly;
        var steps = new Dictionary<string, StepView>(StringComparer.Ordinal)
        {
            ["deposit"] = new("done", c.Batch.FileName),
            ["analysis"] = active ? new("current", a == AnalysisStatuses.Queued ? "en file" : "analyse en cours")
                : a == AnalysisStatuses.Done ? new("done", "analyse terminée")
                : a == AnalysisStatuses.Incomplete ? new("attention", "analyse incomplète")
                : c.State.RulesOnly ? new("done", c.State.RulesOnlyReason == "configuration" ? "IA désactivée par configuration" : "règles seules")
                : a is not null && AnalysisStatuses.RequiresChoice(a) ? new("blocked", "choix requis : relancer ou vérifier sans IA")
                : new("todo", null),
        };
        if (!reviewable)
        {
            steps["review"] = new("locked", null);
            steps["check"] = new("locked", null);
            steps["import"] = new("locked", null);
        }
        else
        {
            var blocking = c.Interpretation.Confirmations > 0 || c.Interpretation.DataRows.Any(r => CatalogStatuses.Blocking(r.Status));
            steps["review"] = new(blocking ? "attention" : "done", blocking ? $"{c.Interpretation.Confirmations} point(s) à traiter" : "rien à corriger");
            var stale = c.State.CheckedRevision is { } checkedRev && checkedRev != c.State.Revision;
            var remaining = check?["blocking"]?.GetValue<int>() ?? 0;
            steps["check"] = check is null ? new("todo", null)
                : stale ? new("stale", "périmé : une correction a suivi le contrôle")
                : remaining > 0 ? new("blocked", $"{remaining} erreur(s) restante(s)")
                : new("done", "contrôle à jour");
            var ready = check is not null && !stale && remaining == 0 && check["nothingToImport"]?.GetValue<bool>() != true && !active && c.Batch.Status == ImportStatuses.Checked;
            steps["import"] = c.Batch.Status == ImportStatuses.Applied ? new("done", "importé") : ready ? new("todo", "prêt à importer") : new("locked", null);
        }

        if (closed)
        {
            foreach (var key in steps.Keys.ToList())
            {
                steps[key] = steps[key] with { Sublabel = (steps[key].Sublabel is null ? string.Empty : steps[key].Sublabel + " · ") + (c.Batch.Status == ImportStatuses.Applied ? "lot importé (lecture seule)" : "lot abandonné (lecture seule)") };
            }
        }

        return steps;
    }

    // ------------------------------------------------------------------ analyse (étape 2)

    public async Task<Outcome<AnalysisStateView>> AnalysisAsync(Guid batchId, long after, CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        return c is null ? Outcome.Missing<AnalysisStateView>() : Outcome.Ok(await AnalysisViewAsync(c, after, cancellationToken));
    }

    private async Task<AnalysisStateView> AnalysisViewAsync(Context c, long after, CancellationToken cancellationToken)
    {
        var a = c.Latest;
        var events = a is null ? [] : await assistant.EventsAsync(a.Id, after, 200, cancellationToken);
        var batchLevel = after == 0 ? await assistant.BatchEventsAsync(c.Batch.Id, cancellationToken) : [];
        var now = CatalogClock.Now(clock);
        var elapsed = a is null ? 0 : (long)(((a.EndedAtUtc ?? now) - (a.StartedAtUtc ?? a.QueuedAtUtc)).TotalMilliseconds);
        int? position = a is { Status: AnalysisStatuses.Queued } ? await assistant.QueuePositionAsync(a, cancellationToken) + 1 : null;
        var data = c.Interpretation.DataRows.ToList();
        var progress = new List<ProgressView>
        {
            new("Lecture complète du fichier", c.Document.Records.Count, c.Document.Records.Count),
            new("Validation par les règles", data.Count, data.Count),
        };
        if (a is not null && AnalysisStatuses.IsUsable(a.Status))
        {
            progress.Add(new("Revalidation avec la proposition de l'IA", data.Count, data.Count));
        }

        var families = c.Interpretation.DataRows.GroupBy(r => (r.Family ?? "?", r.FamilyStatus)).Select(g => new JsonObject
        {
            ["family"] = g.Key.Item1, ["status"] = g.Key.FamilyStatus, ["rows"] = g.Count(),
        }).ToList();
        var structure = c.Document.Records.SelectMany(r => r.Anomalies.Where(x => x != "empty_line").Select(x => $"L{r.Line} : {x}")).Take(50).ToList();
        return new AnalysisStateView(
            a?.Attempt ?? 0, a?.Status ?? "none", a?.BasedOnRevision, position, elapsed,
            new AnalysisModelView(a?.ConfiguredModel ?? options.Model, a is not null && AnalysisStatuses.IsUsable(a.Status) ? a.Model : null, a?.ModelDigest, a?.OllamaVersion),
            [.. events.Select(View)],
            progress,
            a is { Status: AnalysisStatuses.Queued } ? "En attente dans la file d'analyse" : a is { Status: AnalysisStatuses.Running } ? "Le modèle analyse l'échantillon (durée non mesurable à l'avance)" : null,
            a is null ? null : JsonNode.Parse(a.CoverageJson),
            families, structure, a?.LateResultDiscarded ?? false, a?.Error, c.State.RulesOnly, c.State.RulesOnlyReason, [.. batchLevel.Select(View)]);

        static AnalysisEventView View(BatchAnalysisEvent e) => new(e.Id, e.AtUtc, e.Code, JsonNode.Parse(e.DataJson));
    }

    /// <summary>T7 : nouvelle tentative (tentative précédente terminale, lot ouvert).</summary>
    public async Task<Outcome<AnalysisStateView>> RetryAsync(Guid batchId, Guid requestId, uint version, CatalogActor actor, CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<AnalysisStateView>();
        }

        if (c.Batch.Status is ImportStatuses.Applied or ImportStatuses.Cancelled)
        {
            return Outcome.Conflict<AnalysisStateView>("batch_closed");
        }

        if (c.Latest is { } latest && AnalysisStatuses.IsActive(latest.Status))
        {
            return Outcome.Conflict<AnalysisStateView>("analysis_running");
        }

        if (c.Batch.Version != version)
        {
            return Outcome.Conflict<AnalysisStateView>();
        }

        if (!options.Enabled || options.Mode == AnalysisModes.Off)
        {
            return Outcome.Refuse<AnalysisStateView>("analysis_disabled");
        }

        var now = CatalogClock.Now(clock);
        var attempt = await assistant.AnalysisCountAsync(batchId, cancellationToken) + 1;
        var analysis = Queue(batchId, attempt, c.State.Revision, actor.UserId, now);
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportAnalysisRequested, AuditActions.ImportEntity, batchId,
            [CatalogAudit.Field("attempt", null, attempt.ToString(CultureInfo.InvariantCulture)), CatalogAudit.Field("model", null, options.Model)]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return Outcome.Conflict<AnalysisStateView>("analysis_running");
        }

        await EnqueueAsync(analysis, cancellationToken);
        return Outcome.Ok(await AnalysisViewAsync((await LoadAsync(batchId, cancellationToken))!, 0, cancellationToken));
    }

    /// <summary>T6 : annulation par comparaison-échange (une fin de traitement concurrente perd et devient « résultat tardif »).</summary>
    public async Task<Outcome<AnalysisStateView>> CancelAnalysisAsync(Guid batchId, CatalogActor actor, CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<AnalysisStateView>();
        }

        if (c.Latest is not { } latest || !AnalysisStatuses.IsActive(latest.Status))
        {
            return Outcome.Conflict<AnalysisStateView>("analysis_not_running");
        }

        var now = CatalogClock.Now(clock);
        latest.Cancel(now);
        assistant.Add(BatchAnalysisEvent.Create(latest.Id, batchId, now, AnalysisEventCodes.AnalysisCancelled, new JsonObject { ["by"] = actor.UserId }.ToJsonString()));
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportAnalysisCancelled, AuditActions.ImportEntity, batchId,
            [CatalogAudit.Field("attempt", null, latest.Attempt.ToString(CultureInfo.InvariantCulture))]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            // La tentative a terminé entre-temps (CAS perdu) : rien n'est annulé.
            return Outcome.Conflict<AnalysisStateView>("analysis_not_running");
        }

        scheduler.Cancel(latest.Id);
        return Outcome.Ok(await AnalysisViewAsync((await LoadAsync(batchId, cancellationToken))!, 0, cancellationToken));
    }

    // ------------------------------------------------------------------ vérifier et corriger (étape 3)

    public async Task<Outcome<RowsView>> RowsAsync(Guid batchId, string? filter, int page, int pageSize, CatalogActor actor, CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<RowsView>();
        }

        var issues = CatalogMaterializer.Issues(c.Interpretation, c.User, IssueContext(c));
        var (total, rows) = CatalogMaterializer.Rows(c.Interpretation, issues, CatalogMaterializer.Filters.Contains(filter) ? filter! : "all", Math.Max(1, page),
            Math.Clamp(pageSize, 1, MaxPageSize), actor.Has(Permissions.CostsRead));
        return Outcome.Ok(new RowsView(total, CatalogMaterializer.Counters(c.Interpretation, issues), rows));
    }

    public async Task<Outcome<AssistantIssue>> IssueAsync(Guid batchId, string key, CatalogActor actor, CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<AssistantIssue>();
        }

        var context = IssueContext(c);
        if (key.StartsWith("dup:sku:", StringComparison.Ordinal))
        {
            // Diagnostic : le SKU existe-t-il déjà au catalogue, et sur quel modèle ?
            var existing = await imports.VariantsBySkuAsync([key[8..]], cancellationToken);
            var models = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (sku, variant) in existing)
            {
                models[sku] = (await store.FindModelAsync(variant.ModelId, cancellationToken))?.Name ?? "?";
            }

            context = context with { ExistingSkuModels = models };
        }

        var issue = CatalogMaterializer.Issues(c.Interpretation, c.User, context).FirstOrDefault(i => i.Key == key);
        if (issue is null)
        {
            return Outcome.Missing<AssistantIssue>("issue_not_found");
        }

        if (issue.Examples is not null && !actor.Has(Permissions.CostsRead))
        {
            // Montants masqués sans costs.read : la confirmation reste possible pour qui a les droits du champ.
            issue = issue with { Examples = new JsonObject { ["hidden"] = true }, Original = ["•••"] };
        }

        return Outcome.Ok(issue);
    }

    /// <summary>
    /// Décisions = révisions append-only (<c>baseRevision</c> = révision courante, sinon <c>409 revision_conflict</c>), puis
    /// revalidation COMPLÈTE ; le contrôle précédent devient périmé. Montants : une décision par requête, empreinte des
    /// exemples exigée, droit du champ exigé ; aucune route « tout confirmer ».
    /// </summary>
    public async Task<Outcome<RevalidationView>> DecideAsync(Guid batchId, Guid requestId, int baseRevision, IReadOnlyList<DecisionInput>? decisions, CatalogActor actor,
        CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<RevalidationView>();
        }

        if (await assistant.FindDecisionByRequestAsync(batchId, requestId, cancellationToken) is { } replayed)
        {
            return replayed.AuthorUserId == actor.UserId
                ? Outcome.Ok(Revalidation(c, c.Interpretation, c.State.Revision), replayed: true)
                : Outcome.Conflict<RevalidationView>("request_id_reused");
        }

        if (c.Batch.Status is ImportStatuses.Applied or ImportStatuses.Cancelled)
        {
            return Outcome.Conflict<RevalidationView>("batch_closed");
        }

        if (baseRevision != c.State.Revision)
        {
            return Outcome.Conflict<RevalidationView>("revision_conflict", new { revision = c.State.Revision });
        }

        if (decisions is null || decisions.Count == 0 || decisions.Count > MaxDecisionsPerRequest)
        {
            return Outcome.Invalid<RevalidationView>("decisions", decisions is null or { Count: 0 } ? CatalogFieldErrors.Required : CatalogFieldErrors.TooLong);
        }

        var issues = CatalogMaterializer.Issues(c.Interpretation, c.User, IssueContext(c));
        var moneyDecisions = 0;
        var now = CatalogClock.Now(clock);
        var revision = c.State.Revision;
        var reviewable = (c.Latest is { } l && AnalysisStatuses.IsUsable(l.Status)) || c.State.RulesOnly;
        foreach (var input in decisions)
        {
            var type = input.Type;
            var target = input.Target ?? [];
            var value = input.Value ?? [];
            var refusal = await RefusalAsync(c, issues, type, target, value, input.ExamplesDigest, actor, reviewable, cancellationToken);
            if (refusal is not null)
            {
                return refusal.Value.Forbidden ? Outcome.Forbidden<RevalidationView>(refusal.Value.Code) : Outcome.Refuse<RevalidationView>("decision_invalid", new { code = refusal.Value.Code, type });
            }

            if (type == DecisionTypes.ColumnField && AssistantUserLayer.Text(value, "field") is "purchasePriceHt" or "salePriceHt" && ++moneyDecisions > 1)
            {
                return Outcome.Refuse<RevalidationView>("decision_invalid", new { code = "one_money_decision_per_request", type });
            }

            revision++;
            var supersedes = type == DecisionTypes.Revoke && AssistantUserLayer.Int(target, "revision") is { } r ? c.Decisions.FirstOrDefault(d => d.Revision == r)?.Id : null;
            assistant.Add(BatchDecision.Create(Guid.CreateVersion7(now), batchId, revision, type!, target.ToJsonString(), value.ToJsonString(), input.ExamplesDigest, supersedes, actor.UserId, now,
                revision == c.State.Revision + 1 ? requestId : ProductCreationService.Derive(requestId, $"decision:{revision}")));
            if (type == DecisionTypes.RulesOnly)
            {
                c.State.ChooseRulesOnly(actor.UserId, now, "user");
                assistant.Add(BatchAnalysisEvent.Create(null, batchId, now, AnalysisEventCodes.RulesOnlyChosen, new JsonObject { ["revision"] = revision }.ToJsonString()));
                CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportRulesOnlyChosen, AuditActions.ImportEntity, batchId,
                    [CatalogAudit.Field("revision", null, revision.ToString(CultureInfo.InvariantCulture))]);
            }
        }

        while (c.State.Revision < revision)
        {
            c.State.Advance();
        }

        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportDecisionRecorded, AuditActions.ImportEntity, batchId,
        [
            CatalogAudit.Field("revision", baseRevision.ToString(CultureInfo.InvariantCulture), revision.ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("types", null, string.Join(",", decisions.Select(d => d.Type).Distinct())),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            var current = await assistant.FindAssistantAsync(batchId, cancellationToken);
            return Outcome.Conflict<RevalidationView>("revision_conflict", new { revision = current?.Revision });
        }

        var after = (await LoadAsync(batchId, cancellationToken))!;
        var raisedIssues = CatalogMaterializer.Issues(after.Interpretation, after.User, IssueContext(after)).Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var before = issues.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        return Outcome.Ok(new RevalidationView(
            after.State.Revision, CatalogMaterializer.Counters(after.Interpretation, CatalogMaterializer.Issues(after.Interpretation, after.User, IssueContext(after))),
            [.. before.Except(raisedIssues)], [.. raisedIssues.Except(before)], after.State.CheckedRevision is not null && after.State.CheckedRevision != after.State.Revision));
    }

    private static RevalidationView Revalidation(Context c, CatalogInterpretation d, int revision)
    {
        var issues = CatalogMaterializer.Issues(d, c.User, IssueContext(c));
        return new RevalidationView(revision, CatalogMaterializer.Counters(d, issues), [], [], c.State.CheckedRevision is not null && c.State.CheckedRevision != revision);
    }

    /// <summary>Contrôle d'une décision : type connu, cible existante, valeur licite, droits du champ, empreinte des exemples.</summary>
    private async Task<(string Code, bool Forbidden)?> RefusalAsync(
        Context c, IReadOnlyList<AssistantIssue> issues, string? type, JsonObject target, JsonObject value, string? digest, CatalogActor actor, bool reviewable,
        CancellationToken cancellationToken)
    {
        if (type is null || !DecisionTypes.All.Contains(type))
        {
            return ("type_unknown", false);
        }

        if (type == DecisionTypes.RulesOnly)
        {
            var a = c.Latest?.Status;
            var allowed = !c.State.RulesOnly && (a is null || AnalysisStatuses.RequiresChoice(a));
            return allowed ? null : ("action_not_allowed", false);
        }

        if (!reviewable && type != DecisionTypes.Revoke)
        {
            return ("analysis_required", false);
        }

        var lines = c.Document.Records.Select(r => r.Line).ToHashSet();
        switch (type)
        {
            case DecisionTypes.ColumnField:
            {
                var column = AssistantUserLayer.Int(target, "column");
                var field = AssistantUserLayer.Text(value, "field");
                if (column is null || column < 0 || column >= c.Document.Columns || field is null || !(field == CatalogMaterializer.Ignore || CatalogLexicon.AllFieldKeys.Contains(field)) || field == "unknown")
                {
                    return ("value_invalid", false);
                }

                if (field is "salePriceTtc" or "priceUnqualified")
                {
                    return ("action_not_allowed", false);
                }

                if (field is "purchasePriceHt" or "salePriceHt")
                {
                    if (!actor.Has(field == "purchasePriceHt" ? Permissions.CostsWrite : Permissions.PricingManual))
                    {
                        return (field == "purchasePriceHt" ? "costs_forbidden" : "manual_price_forbidden", true);
                    }

                    if (c.State.Target == AssistantTargets.Lenses && field == "salePriceHt")
                    {
                        return ("action_not_allowed", false);
                    }

                    var (_, expected) = CatalogMaterializer.MoneyExamples(c.Interpretation, column.Value, c.Currency);
                    if (digest is null || !string.Equals(digest, expected, StringComparison.Ordinal))
                    {
                        return ("examples_not_reviewed", false);
                    }
                }

                if (field != CatalogMaterializer.Ignore)
                {
                    var col = c.Interpretation.Columns[column.Value];
                    var check = CatalogInterpretationValidator.CheckColumn(field, col.Profile, new HeaderRule(null, [], false, false, false, false, string.Empty));
                    if (check.Violation)
                    {
                        return ("value_invalid", false);
                    }

                    if (c.Interpretation.Columns.Any(x => x.C != column && x.Field == field && x.Importable && field is not ("notes" or "color" or "variantLabel")))
                    {
                        return ("field_already_used", false);
                    }
                }

                return null;
            }

            case DecisionTypes.HeaderPresence:
                return AssistantUserLayer.Bool(value, "present") is null ? ("value_invalid", false) : null;
            case DecisionTypes.FamilyClass:
            {
                var key = AssistantUserLayer.Text(target, "classKey");
                var family = AssistantUserLayer.Text(value, "family");
                return key is null || family is null || !CatalogLexicon.FamilyCodes.Contains(family) || !c.Interpretation.DataRows.Any(r => r.FamilyClass == key)
                    ? ("value_invalid", false)
                    : null;
            }

            case DecisionTypes.Continuation:
                return AssistantUserLayer.Bool(value, "accept") is null || (AssistantUserLayer.Int(target, "line") is { } cl && !lines.Contains(cl)) ? ("value_invalid", false) : null;
            case DecisionTypes.CellValue:
            {
                var line = AssistantUserLayer.Int(target, "line");
                var column = AssistantUserLayer.Int(target, "column");
                var text = AssistantUserLayer.Text(value, "value");
                if (line is null || !lines.Contains(line.Value) || column is null || column < 0 || column >= c.Document.Columns || text is null || text.Length > 500)
                {
                    return ("value_invalid", false);
                }

                var col = c.Interpretation.Columns[column.Value];
                if (CatalogLexicon.Money.Contains(col.Field) && !actor.Has(col.Field == "salePriceHt" ? Permissions.PricingManual : Permissions.CostsWrite))
                {
                    return (col.Field == "salePriceHt" ? "manual_price_forbidden" : "costs_forbidden", true);
                }

                if (col.Importable && CatalogInterpretationValidator.Cell(col.Field, text, c.User.DecimalSeparator?.Separator).Status == CatalogStatuses.Invalid)
                {
                    return ("value_invalid", false);
                }

                if (col.Field is "sku" or "supplierReference" or "manufacturerReference" or "barcode" && text.Trim().Length > 0)
                {
                    // Unicité revérifiée dans le fichier (et au catalogue pour un SKU porté par un autre modèle : refusé au contrôle).
                    var normalized = col.Field == "barcode" ? text.Trim() : CatalogKeys.Normalize(text.Trim());
                    var clash = c.Interpretation.DataRows.Any(r => r.Line != line && r.Cells.Any(x => x.C == column && (col.Field == "barcode" ? x.Input.Trim() : CatalogKeys.Normalize(x.Input.Trim())) == normalized));
                    if (clash)
                    {
                        return ("sku_exists", false);
                    }
                }

                return null;
            }

            case DecisionTypes.RowSkip or DecisionTypes.RowRestore:
                return AssistantUserLayer.Int(target, "line") is { } skip && lines.Contains(skip) ? null : ("value_invalid", false);
            case DecisionTypes.DecimalSeparator:
                return AssistantUserLayer.Text(value, "separator") is "," or "." ? null : ("value_invalid", false);
            case DecisionTypes.FileDefault:
            {
                var field = AssistantUserLayer.Text(target, "field");
                var v = AssistantUserLayer.Text(value, "value");
                var ok = field switch
                {
                    "stockPolicy" => c.State.Target == AssistantTargets.Lenses ? v is "stocked" or "on_order" : v is "stocked" or "order_only",
                    "lensType" => c.State.Target == AssistantTargets.Lenses && v is not null && LensTypes.All.Contains(v),
                    // Seule la devise du magasin peut être confirmée (aucune conversion, jamais une devise inventée).
                    "currency" => c.Currency is not null && string.Equals(v, c.Currency, StringComparison.Ordinal)
                        && CatalogMaterializer.LinesWithoutCurrency(c.Interpretation, c.State.Target).Count > 0,
                    _ => false,
                };
                if (ok && field == "currency")
                {
                    // Droit des montants concernés : costs.write (achat) ou pricing.manual (vente).
                    var moneyFields = CatalogMaterializer.MoneyFields(c.State.Target);
                    var missing = c.Interpretation.Columns.Where(x => x.Importable && moneyFields.Contains(x.Field))
                        .Select(x => x.Field == "salePriceHt" ? Permissions.PricingManual : Permissions.CostsWrite).Distinct().FirstOrDefault(p => !actor.Has(p));
                    if (missing is not null)
                    {
                        return (missing == Permissions.CostsWrite ? "costs_forbidden" : "manual_price_forbidden", true);
                    }
                }

                return ok ? null : ("value_invalid", false);
            }

            case DecisionTypes.CreateReference:
            {
                if (c.State.Target != AssistantTargets.Lenses || !actor.Has(Permissions.CatalogConfigure))
                {
                    return c.State.Target != AssistantTargets.Lenses ? ("action_not_allowed", false) : ("catalog_configure_required", true);
                }

                var kind = AssistantUserLayer.Text(target, "kind");
                var name = AssistantUserLayer.Text(target, "name");
                if (kind is not ("manufacturer" or "range") || string.IsNullOrWhiteSpace(name) || name.Length > CatalogLimits.NameMaxLength)
                {
                    return ("value_invalid", false);
                }

                // Seulement une référence RÉELLEMENT citée par le fichier et absente du référentiel.
                var plan = await lensPlanner.PlanAsync(c.Interpretation, c.User, c.Batch.SupplierId, c.Batch.FileName, c.Currency, cancellationToken);
                var key = AssistantUserLayer.ReferenceKey(kind, name, AssistantUserLayer.Text(target, "manufacturer"));
                return plan.References.Any(r => AssistantUserLayer.ReferenceKey(r.Kind, r.Name, r.Manufacturer) == key) ? null : ("value_invalid", false);
            }

            case DecisionTypes.Revoke:
            {
                var r = AssistantUserLayer.Int(target, "revision");
                var decision = r is null ? null : c.Decisions.FirstOrDefault(d => d.Revision == r);
                return decision is null || decision.Type is DecisionTypes.Revoke or DecisionTypes.RulesOnly ? ("value_invalid", false) : null;
            }
        }

        _ = issues;
        return ("type_unknown", false);
    }

    // ------------------------------------------------------------------ contrôle à blanc (étape 4)

    public async Task<Outcome<JsonObject>> CheckAsync(Guid batchId, Guid requestId, uint version, int revision, CatalogActor actor, CancellationToken cancellationToken)
    {
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<JsonObject>();
        }

        if (c.Batch.LastRequestId == requestId && c.State.CheckSummaryJson is { } replay)
        {
            var again = (JsonObject)JsonNode.Parse(replay)!;
            again.Remove(PlanFingerprintKey);
            return Outcome.Ok(again, replayed: true);
        }

        if (c.Batch.Version != version)
        {
            return Outcome.Conflict<JsonObject>();
        }

        if (c.Batch.Status is ImportStatuses.Applied or ImportStatuses.Cancelled)
        {
            return Outcome.Conflict<JsonObject>("batch_closed");
        }

        if (c.Latest is { } latest && AnalysisStatuses.IsActive(latest.Status))
        {
            return Outcome.Conflict<JsonObject>("analysis_running");
        }

        if (!((c.Latest is { } l && AnalysisStatuses.IsUsable(l.Status)) || c.State.RulesOnly))
        {
            return Outcome.Conflict<JsonObject>("analysis_required");
        }

        if (revision != c.State.Revision)
        {
            return Outcome.Conflict<JsonObject>("revision_conflict", new { revision = c.State.Revision });
        }

        var watch = Stopwatch.StartNew();
        var (summary, rows, costs, manual, errorCount) = await PlanSummaryAsync(c, cancellationToken);
        var now = CatalogClock.Now(clock);
        summary["basedOnRevision"] = c.State.Revision;
        summary["checkedAtUtc"] = now;
        summary["elapsedMs"] = watch.ElapsedMilliseconds;
        await imports.ReplaceRowsAsync(c.Batch.Id, rows, cancellationToken);
        c.Batch.MarkChecked(ImportService.AssistedMapping(c.State.Revision), errorCount, costs, manual, actor.UserId, now, requestId);
        // GATE D : l'empreinte du plan métier est enregistrée avec la synthèse (base de l'application), jamais servie au client.
        c.State.MarkChecked(c.State.Revision, summary.ToJsonString());
        summary.Remove(PlanFingerprintKey);
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportChecked, AuditActions.ImportEntity, c.Batch.Id,
        [
            CatalogAudit.Field("lineCount", null, rows.Count.ToString(CultureInfo.InvariantCulture)), CatalogAudit.Field("status", null, ImportStatuses.Checked),
            CatalogAudit.Field("revision", null, c.State.Revision.ToString(CultureInfo.InvariantCulture)),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? Outcome.Ok(summary) : Outcome.Conflict<JsonObject>();
    }

    /// <summary>Clé (interne, retirée de toute réponse) de l'empreinte du plan métier contrôlé dans la synthèse enregistrée.</summary>
    internal const string PlanFingerprintKey = "planFingerprint";

    /// <summary>
    /// GATE D — empreinte du plan MÉTIER des offres de verres : pour chaque ligne, issue, identité, offre existante
    /// rapprochée, champs modifiés, revalidation et base (état actuel des caractéristiques fournies, valeurs écrites) ;
    /// références à créer. Deux plans de même empreinte écrivent exactement la même chose sur le même état.
    /// </summary>
    internal static string LensFingerprint(LensImportPlan plan) => Fingerprint(
        plan.Rows.OrderBy(r => r.Line).Select(r => $"row|{r.Line}|{r.Outcome}|{r.OfferKey}|{r.ExistingOfferId:N}|{string.Join(",", r.ChangedFields)}|{r.RevalidationRequired}|{r.Basis}")
            .Concat(plan.References.OrderBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.Name, StringComparer.Ordinal).Select(r => $"ref|{r.Kind}|{r.Name}|{r.Manufacturer}|{string.Join(",", r.Lines)}")));

    /// <summary>
    /// GATE D — empreinte du plan MÉTIER des articles : pour chaque ligne, issue, cible et valeurs écrites (montants
    /// compris) ; base des articles existants (état actuel de chaque champ fourni, tarif du magasin compris).
    /// </summary>
    internal static string ProductFingerprint(AssistedPlan plan) => Fingerprint(
        plan.Rows.OrderBy(r => r.LineNo).Select(r => $"row|{r.LineNo}|{r.Outcome}|{r.TargetKey}|{JsonSerializer.Serialize(new SortedDictionary<string, string>(r.Values.ToDictionary(), StringComparer.Ordinal))}")
            .Concat(plan.Basis.Select(b => "basis|" + b)));

    private static string Fingerprint(IEnumerable<string> lines) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))));

    /// <summary>Empreinte enregistrée au contrôle confirmé (absente d'un contrôle antérieur au correctif : nouveau contrôle exigé).</summary>
    private static string? CheckedFingerprint(Context c) =>
        c.State.CheckSummaryJson is { } json && JsonNode.Parse(json) is JsonObject summary && summary[PlanFingerprintKey] is JsonValue value ? value.GetValue<string>() : null;

    /// <summary>Plan complet SANS écriture métier : compteurs, avant / après, correspondances, provenance, erreurs restantes.</summary>
    private async Task<(JsonObject Summary, List<ImportRow> Rows, bool Costs, bool Manual, int Errors)> PlanSummaryAsync(Context c, CancellationToken cancellationToken)
    {
        var d = c.Interpretation;
        var issues = CatalogMaterializer.Issues(d, c.User, IssueContext(c));
        var rows = new List<ImportRow>();
        var remaining = new JsonArray();
        int productsToCreate = 0, variantsToCreate = 0, toUpdate = 0, unchanged = 0;
        var costs = false;
        var manual = false;
        var changes = new JsonArray();
        foreach (var issue in issues.Where(i => CatalogStatuses.Blocking(i.Severity)))
        {
            remaining.Add(new JsonObject { ["key"] = issue.Key, ["code"] = issue.Code, ["lines"] = new JsonArray([.. issue.Lines.Take(20).Select(x => (JsonNode?)x)]), ["explanation"] = issue.Explanation });
        }

        var blockedRows = d.DataRows.Where(r => CatalogStatuses.Blocking(r.Status)).ToList();
        foreach (var r in blockedRows)
        {
            rows.Add(ImportRow.Create(c.Batch.Id, r.Line, "{}", ImportOutcomes.Error, JsonSerializer.Serialize(r.Reasons.Distinct().Take(10).Select(x => new { field = "row", code = "blocked", reason = x }), Json), null));
        }

        var planErrors = 0;
        string fingerprint;
        JsonArray references = [];
        var offersToRevalidate = 0;
        if (c.State.Target == AssistantTargets.Lenses)
        {
            var plan = await lensPlanner.PlanAsync(d, c.User, c.Batch.SupplierId, c.Batch.FileName, c.Currency, cancellationToken);
            fingerprint = LensFingerprint(plan);
            costs = plan.ContainsCosts;
            foreach (var row in plan.Rows)
            {
                rows.Add(ImportRow.Create(c.Batch.Id, row.Line, JsonSerializer.Serialize(new { row.Supplier, row.Manufacturer, row.Range, row.LensType, row.Index, row.Treatments, row.Omitted }, Json),
                    row.Outcome, JsonSerializer.Serialize(row.Errors, Json), row.OfferKey?.Length > 200 ? row.OfferKey[..200] : row.OfferKey));
                switch (row.Outcome)
                {
                    case ImportOutcomes.Create: variantsToCreate++; break;
                    case ImportOutcomes.Update: toUpdate++; break;
                    case ImportOutcomes.Unchanged: unchanged++; break;
                    default:
                        planErrors++;
                        remaining.Add(new JsonObject { ["key"] = $"plan:{row.Line}", ["code"] = row.Errors.Count > 0 ? row.Errors[0].Code : null, ["lines"] = new JsonArray(row.Line), ["explanation"] = string.Join(", ", row.Errors.Select(e => $"{e.Field} : {e.Code}")) });
                        break;
                }

                foreach (var field in row.ChangedFields)
                {
                    var change = new JsonObject { ["line"] = row.Line, ["sku"] = $"{row.Range} · {row.LensType}", ["field"] = field };
                    if (field == "rulesComplete" && row.RevalidationRequired)
                    {
                        // COR-CSVAI-02 : une capacité change, la fiche déclarée complète devra être revalidée.
                        change["before"] = "fiche complète";
                        change["after"] = "fiche à revalider";
                    }

                    changes.Add(change);
                }
            }

            productsToCreate = plan.References.Count(r => c.User.CreateReferences.ContainsKey(AssistantUserLayer.ReferenceKey(r.Kind, r.Name, r.Manufacturer)));
            offersToRevalidate = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Update && r.RevalidationRequired);
            references = new JsonArray([.. plan.References.Select(r => (JsonNode?)new JsonObject
            {
                ["kind"] = r.Kind, ["name"] = r.Name, ["manufacturer"] = r.Manufacturer, ["lines"] = r.Lines.Count,
                ["decided"] = c.User.CreateReferences.ContainsKey(AssistantUserLayer.ReferenceKey(r.Kind, r.Name, r.Manufacturer)),
            })]);
        }
        else
        {
            var materialized = CatalogMaterializer.ProductRows(d, c.User, c.Currency);
            var plan = await catalogImport.PlanAssistedCatalogAsync(c.Batch.SupplierId, c.State.StoreId, materialized, cancellationToken);
            fingerprint = ProductFingerprint(plan);
            costs = plan.ContainsCosts;
            manual = plan.ContainsManualPrices;
            foreach (var row in plan.Rows)
            {
                rows.Add(ImportRow.Create(c.Batch.Id, row.LineNo, JsonSerializer.Serialize(row.Values, Json), row.Outcome, JsonSerializer.Serialize(row.Errors, Json), row.TargetKey));
                switch (row.Outcome)
                {
                    case ImportOutcomes.Create: variantsToCreate++; break;
                    case ImportOutcomes.Update: toUpdate++; break;
                    case ImportOutcomes.Unchanged: unchanged++; break;
                    default:
                        planErrors++;
                        remaining.Add(new JsonObject { ["key"] = $"plan:{row.LineNo}", ["code"] = row.Errors.Count > 0 ? row.Errors[0].Code : null, ["lines"] = new JsonArray(row.LineNo), ["explanation"] = string.Join(", ", row.Errors.Select(e => $"{e.Field} : {e.Code}")) });
                        break;
                }
            }

            productsToCreate = await NewModelsAsync(plan, cancellationToken);
            foreach (var change in plan.Changes.Take(200))
            {
                var money = change.Field is "purchasePriceHt" or "salePriceHt";
                changes.Add(new JsonObject { ["line"] = change.Line, ["sku"] = change.Sku, ["field"] = change.Field, ["before"] = money ? "•••" : change.Before, ["after"] = money ? "•••" : change.After, ["money"] = money });
            }
        }

        var errorCount = blockedRows.Count + planErrors;
        var mappings = new JsonArray([.. d.Columns.Where(x => x.Importable).Select(x => (JsonNode?)new JsonObject
        {
            ["column"] = x.C, ["header"] = x.Header, ["field"] = x.Field, ["origin"] = x.Origin, ["userRevision"] = x.UserRevision,
            ["notWritten"] = CatalogMaterializer.NotWrittenReason(x.Field, c.State.Target),
        })]);
        var provenance = new JsonArray([.. c.Decisions.OrderBy(x => x.Revision).Select(x => (JsonNode?)new JsonObject
        {
            ["revision"] = x.Revision, ["type"] = x.Type, ["target"] = JsonNode.Parse(x.TargetJson), ["author"] = x.AuthorUserId, ["createdAtUtc"] = x.CreatedAtUtc,
            ["revoked"] = c.Decisions.Any(r => r.Type == DecisionTypes.Revoke && r.SupersedesId == x.Id),
        })]);
        var summary = new JsonObject
        {
            ["target"] = c.State.Target,
            ["productsToCreate"] = productsToCreate,
            ["variantsToCreate"] = variantsToCreate,
            ["toUpdate"] = toUpdate,
            ["unchanged"] = unchanged,
            ["skipped"] = d.Rows.Count(r => r.Kind == "skipped"),
            ["duplicates"] = issues.Count(i => i.Key.StartsWith("dup:", StringComparison.Ordinal) || i.Code == "variant_duplicate"),
            ["ambiguous"] = d.DataRows.Count(r => r.Status == CatalogStatuses.Ambiguous),
            ["blocking"] = errorCount + (d.Confirmations > 0 && blockedRows.Count == 0 ? d.Confirmations : 0),
            ["nothingToImport"] = variantsToCreate + toUpdate == 0,
            ["containsCosts"] = costs,
            ["containsManualPrices"] = manual,
            ["currency"] = c.Currency,
            ["changes"] = changes,
            ["mappings"] = mappings,
            ["provenance"] = provenance,
            ["remaining"] = remaining,
            ["references"] = references,
            ["analysis"] = c.Latest is null ? null : new JsonObject { ["attempt"] = c.Latest.Attempt, ["status"] = c.Latest.Status, ["model"] = c.Latest.Model },
            ["rulesOnly"] = c.State.RulesOnly,
        };
        if (c.State.Target == AssistantTargets.Lenses)
        {
            summary["offersToRevalidate"] = offersToRevalidate;
        }

        summary[PlanFingerprintKey] = fingerprint;

        return (summary, rows, costs, manual, errorCount);
    }

    /// <summary>Modèles réellement nouveaux : ceux des variantes créées qui n'existent pas encore au catalogue.</summary>
    private async Task<int> NewModelsAsync(AssistedPlan plan, CancellationToken cancellationToken)
    {
        var newModels = plan.Rows.Where(r => r.Outcome == ImportOutcomes.Create)
            .Select(r => $"{r.Values.GetValueOrDefault("familyCode")}|{CatalogKeys.Normalize(r.Values.GetValueOrDefault("brand") ?? string.Empty)}|{CatalogKeys.Normalize(r.Values.GetValueOrDefault("modelName") ?? string.Empty)}")
            .ToHashSet(StringComparer.Ordinal);
        if (newModels.Count == 0)
        {
            return 0;
        }

        var existingModels = await imports.ModelsByKeyAsync([.. plan.Rows.Select(r => r.Values.GetValueOrDefault("familyCode")).OfType<string>().Distinct()], cancellationToken);
        return newModels.Count(k => !existingModels.Keys.Any(e => e.StartsWith(k, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Le plan recalculé dans la transaction d'application doit être CELUI du contrôle confirmé (mêmes lignes, mêmes
    /// issues) : sinon le catalogue a changé depuis le contrôle, l'utilisateur relance la vérification (rien n'est écrit).
    /// GATE D : contrôle complété par l'empreinte du plan MÉTIER (<see cref="LensFingerprint"/>,
    /// <see cref="ProductFingerprint"/>) — une ligne restée « mise à jour » dont un prix ou une caractéristique fournie a
    /// changé entre-temps est refusée.
    /// </summary>
    private async Task<bool> SameAsCheckAsync(Guid batchId, IEnumerable<(int Line, string Outcome)> planned, CancellationToken cancellationToken)
    {
        var checkedRows = (await imports.RowsAsync(batchId, cancellationToken)).Select(r => (r.LineNo, r.Outcome)).Order().ToList();
        return checkedRows.SequenceEqual(planned.Order());
    }

    // ------------------------------------------------------------------ confirmation et import (étape 5)

    public async Task<Outcome<AppliedView>> ApplyAsync(Guid batchId, Guid requestId, uint version, int revision, bool confirmed, CatalogActor actor, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var c = await LoadAsync(batchId, cancellationToken);
        if (c is null)
        {
            return Outcome.Missing<AppliedView>();
        }

        if (c.Batch.LastRequestId == requestId && c.Batch.Status == ImportStatuses.Applied)
        {
            return Outcome.Ok(await AppliedAsync(c, 0, cancellationToken), replayed: true);
        }

        if (!confirmed)
        {
            return Outcome.Refuse<AppliedView>("confirmation_required");
        }

        if (c.Batch.Version != version)
        {
            return Outcome.Conflict<AppliedView>();
        }

        if (c.Batch.Status == ImportStatuses.Applied)
        {
            return Outcome.Conflict<AppliedView>("already_applied");
        }

        if (c.Batch.Status != ImportStatuses.Checked)
        {
            return c.Batch.Status == ImportStatuses.Cancelled ? Outcome.Conflict<AppliedView>("batch_closed") : Outcome.Conflict<AppliedView>("check_stale");
        }

        if (c.Latest is { } latest && AnalysisStatuses.IsActive(latest.Status))
        {
            return Outcome.Conflict<AppliedView>("analysis_running");
        }

        if (revision != c.State.Revision || c.State.CheckedRevision != c.State.Revision)
        {
            return Outcome.Conflict<AppliedView>("check_stale", new { revision = c.State.Revision, checkedRevision = c.State.CheckedRevision });
        }

        if (await imports.AppliedFileExistsAsync(c.Batch.FileSha256, c.Batch.Kind, c.Batch.StoreId, cancellationToken))
        {
            return Outcome.Conflict<AppliedView>("already_applied");
        }

        if (c.Interpretation.Confirmations > 0 || c.Interpretation.DataRows.Any(r => CatalogStatuses.Blocking(r.Status)))
        {
            return Outcome.Refuse<AppliedView>("blocking_remaining", new { confirmations = c.Interpretation.Confirmations });
        }

        var now = CatalogClock.Now(clock);
        int products = 0, created = 0, updated = 0, prices = 0, offersCreated = 0, offersUpdated = 0, referencesCreated = 0;
        if (CheckedFingerprint(c) is not { } checkedFingerprint)
        {
            // Contrôle enregistré avant le correctif GATE D (sans empreinte du plan métier) : nouveau contrôle exigé.
            return Outcome.Conflict<AppliedView>("check_stale", new { reason = "check_outdated" });
        }

        if (c.State.Target == AssistantTargets.Lenses)
        {
            // GATE D : offres existantes visées verrouillées AVANT la relecture du plan (aucune écriture concurrente ne
            // s'intercale entre la relecture et l'écriture), puis plan relu sur l'état verrouillé.
            var first = await lensPlanner.PlanAsync(c.Interpretation, c.User, c.Batch.SupplierId, c.Batch.FileName, c.Currency, cancellationToken);
            await lensPlanner.LockExistingAsync(first, cancellationToken);
            var plan = await lensPlanner.PlanAsync(c.Interpretation, c.User, c.Batch.SupplierId, c.Batch.FileName, c.Currency, cancellationToken);
            if (plan.Rows.Any(r => r.Outcome == ImportOutcomes.Error))
            {
                return Outcome.Refuse<AppliedView>("blocking_remaining", new { errors = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Error) });
            }

            if (!plan.Rows.Any(r => r.Outcome is ImportOutcomes.Create or ImportOutcomes.Update))
            {
                return Outcome.Refuse<AppliedView>("nothing_to_import");
            }

            foreach (var permission in AssistedLensPlanner.RequiredPermissions(plan.ContainsCosts).Where(p => !actor.Has(p)))
            {
                return Outcome.Forbidden<AppliedView>(permission == Permissions.CostsWrite ? "costs_forbidden" : "catalog_configure_required");
            }

            if (!await SameAsCheckAsync(batchId, plan.Rows.Select(r => (r.Line, r.Outcome)), cancellationToken) || LensFingerprint(plan) != checkedFingerprint)
            {
                return Outcome.Conflict<AppliedView>("check_stale", new { reason = "catalog_changed" });
            }

            foreach (var action in plan.Actions)
            {
                var outcome = await action(actor, now, requestId, cancellationToken);
                if (outcome.Kind == OutcomeKind.Conflict)
                {
                    // Version d'une offre différente de celle relue (filet de sécurité derrière le verrou) : rien n'est écrit.
                    return Outcome.Conflict<AppliedView>("check_stale", new { reason = "catalog_changed", code = outcome.Code });
                }

                if (!outcome.IsSuccess)
                {
                    // Tout ou rien : la transaction n'est pas validée (libération = annulation).
                    return Outcome.Refuse<AppliedView>("apply_refused", new { code = outcome.Code, errors = outcome.Errors });
                }
            }

            offersCreated = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Create);
            offersUpdated = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Update);
            referencesCreated = plan.Actions.Count - offersCreated - offersUpdated;
        }
        else
        {
            // GATE D : variantes existantes visées et leurs tarifs du magasin verrouillés AVANT la relecture du plan, puis plan
            // relu sur l'état verrouillé (un prix modifié entre-temps est relu, jamais écrasé en silence).
            var first = await catalogImport.PlanAssistedCatalogAsync(c.Batch.SupplierId, c.State.StoreId, CatalogMaterializer.ProductRows(c.Interpretation, c.User, c.Currency), cancellationToken);
            await catalogImport.LockAssistedTargetsAsync(first, c.State.StoreId, cancellationToken);
            var plan = await catalogImport.PlanAssistedCatalogAsync(c.Batch.SupplierId, c.State.StoreId, CatalogMaterializer.ProductRows(c.Interpretation, c.User, c.Currency), cancellationToken);
            if (plan.Rows.Any(r => r.Outcome == ImportOutcomes.Error))
            {
                return Outcome.Refuse<AppliedView>("blocking_remaining", new { errors = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Error) });
            }

            if (!plan.Rows.Any(r => r.Outcome is ImportOutcomes.Create or ImportOutcomes.Update))
            {
                return Outcome.Refuse<AppliedView>("nothing_to_import");
            }

            if ((plan.ContainsCosts && !actor.Has(Permissions.CostsWrite)) || (plan.ContainsManualPrices && !actor.Has(Permissions.PricingManual)))
            {
                return Outcome.Forbidden<AppliedView>(plan.ContainsCosts && !actor.Has(Permissions.CostsWrite) ? "costs_forbidden" : "manual_price_forbidden");
            }

            if (c.State.StoreId is { } storeId && (plan.ContainsCosts || plan.ContainsManualPrices) && !await store.LockStoreAsync(storeId, cancellationToken))
            {
                return Outcome.Missing<AppliedView>("store_not_found");
            }

            if (!await SameAsCheckAsync(batchId, plan.Rows.Select(r => (r.LineNo, r.Outcome)), cancellationToken) || ProductFingerprint(plan) != checkedFingerprint)
            {
                return Outcome.Conflict<AppliedView>("check_stale", new { reason = "catalog_changed" });
            }

            products = await NewModelsAsync(plan, cancellationToken);

            foreach (var action in plan.Actions)
            {
                await action(actor.UserId, now, requestId, cancellationToken);
            }

            created = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Create);
            updated = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Update);
            prices = plan.Rows.Count(r => r.Outcome is ImportOutcomes.Create or ImportOutcomes.Update && (r.Values.ContainsKey("price:purchasePriceHt") || r.Values.ContainsKey("price:salePriceHt")));
        }

        // COR-CSVAI-04 : compteurs RÉELLEMENT écrits, enregistrés avec la synthèse AVANT la clôture du lot ; la réponse de la
        // première application et celle d'un rejeu (même requestId) relisent ce même résultat.
        var applied = new AppliedView(products, created, updated, prices, offersCreated, offersUpdated, referencesCreated, now, watch.ElapsedMilliseconds);
        var withApplied = c.State.CheckSummaryJson is { } checkJson && JsonNode.Parse(checkJson) is JsonObject checkSummary ? checkSummary : [];
        withApplied["applied"] = JsonSerializer.SerializeToNode(applied, Json);
        c.State.RecordApplied(withApplied.ToJsonString());
        var recorded = await unitOfWork.SaveAsync(cancellationToken);
        if (!recorded.Saved)
        {
            // Écriture concurrente détectée par la version (xmin) d'une ligne écrite : plan périmé, rien n'est écrit.
            return recorded.Status == CommitStatus.VersionConflict
                ? Outcome.Conflict<AppliedView>("check_stale", new { reason = "catalog_changed" })
                : CatalogService.UniqueRefusal<AppliedView>(recorded);
        }

        var batch = (await imports.FindBatchAsync(batchId, cancellationToken))!;
        batch.MarkApplied(actor.UserId, now, requestId);
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportApplied, AuditActions.ImportEntity, batch.Id,
        [
            CatalogAudit.Field("kind", null, batch.Kind), CatalogAudit.Field("pipeline", null, AssistantPipelines.AssistantV2), CatalogAudit.Field("target", null, c.State.Target),
            CatalogAudit.Field("revision", null, c.State.Revision.ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("analysisAttempt", null, c.Usable?.Attempt.ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("decisions", null, c.Decisions.Count.ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("lineCount", null, (created + updated + offersCreated + offersUpdated).ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("status", ImportStatuses.Checked, ImportStatuses.Applied),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_batches_applied_file" }
                ? Outcome.Conflict<AppliedView>("already_applied")
                : CatalogService.UniqueRefusal<AppliedView>(commit);
        }

        await transaction.CommitAsync(cancellationToken);
        var reread = (await LoadAsync(batchId, cancellationToken))!;
        return Outcome.Ok(await AppliedAsync(reread, watch.ElapsedMilliseconds, cancellationToken));
    }

    /// <summary>
    /// Nombres RÉELLEMENT enregistrés, relus après la transaction (rapport du lot appliqué) : résultat enregistré à
    /// l'application (identique pour la première réponse et tout rejeu) ; à défaut (lot appliqué avant COR-CSVAI-04),
    /// lignes du rapport.
    /// </summary>
    private async Task<AppliedView> AppliedAsync(Context c, long elapsedMs, CancellationToken cancellationToken)
    {
        var summary = c.State.CheckSummaryJson is { } json ? JsonNode.Parse(json) : null;
        if (summary?["applied"] is JsonObject recorded && recorded.Deserialize<AppliedView>(Json) is { } stored)
        {
            return stored with { AppliedAtUtc = c.Batch.AppliedAtUtc ?? stored.AppliedAtUtc };
        }

        var rows = await imports.RowsAsync(c.Batch.Id, cancellationToken);
        return new AppliedView(
            summary?["productsToCreate"]?.GetValue<int>() ?? 0, rows.Count(r => r.Outcome == ImportOutcomes.Create), rows.Count(r => r.Outcome == ImportOutcomes.Update), 0, 0, 0, 0,
            c.Batch.AppliedAtUtc ?? CatalogClock.Now(clock), elapsedMs);
    }

    // ------------------------------------------------------------------ historique

    public async Task<ImportsSummaryView> SummaryAsync(CancellationToken cancellationToken)
    {
        var (total, last, resumable) = await assistant.BatchSummaryAsync(cancellationToken);
        return new ImportsSummaryView(total, last, resumable);
    }
}
