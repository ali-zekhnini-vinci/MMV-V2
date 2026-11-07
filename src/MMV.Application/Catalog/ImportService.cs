using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stores;
using MMV.Domain.Suppliers;

namespace MMV.Application.Catalog;

/// <summary>Champ cible d'un import, avec ses alias d'en-tête reconnus pour la correspondance automatique.</summary>
public sealed record ImportField(string Key, bool Required, params string[] Aliases);

/// <summary>Erreur d'une ligne : champ (clé cible) et code borné ; jamais la valeur saisie.</summary>
public sealed record ImportError(string Field, string Code);

/// <summary>
/// Aperçu après dépôt : en-têtes, premières lignes, correspondance proposée (appliquée sauf colonnes ambiguës),
/// propositions détaillées avec leur confiance, séparateur détecté, séparateur décimal détecté ou ambigu, modèle
/// fournisseur repris (COR-05-04) ; analyse (moteur utilisé, durées, anomalies — UX-IA) et fichier identique déjà
/// appliqué (refus annoncé avant le contrôle).
/// </summary>
public sealed record ImportPreview(
    Guid BatchId, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> SampleRows, IReadOnlyDictionary<string, string> SuggestedMapping, int RowCount,
    string Delimiter, IReadOnlyList<MappingSuggestion> Suggestions, string? DecimalSeparator, bool DecimalAmbiguous, bool ProfileApplied, Guid? SupplierId,
    ImportAnalysis? Analysis = null, bool AlreadyApplied = false);

/// <summary>Synthèse d'un contrôle à blanc ou d'une application ; durée serveur mesurée de l'opération (UX-IA).</summary>
public sealed record ImportSummary(Guid BatchId, string Status, int Total, int Create, int Update, int Unchanged, int Error, long? ElapsedMs = null);

/// <summary>
/// Import CSV contrôlé (05-D, D-05-18) : dépôt (zone de préparation, empreinte SHA-256, aperçu, correspondance proposée) →
/// contrôle à blanc (rapport par ligne, AUCUNE écriture au catalogue) → application explicite, tout ou rien, rejouée
/// contre l'état COURANT dans la transaction, idempotente (même requête : état courant ; même fichier déjà appliqué :
/// 409). Aucune suppression, aucun import destructif : les lignes absentes du fichier ne changent rien. Coûts importés
/// seulement avec <c>costs.write</c>, prix manuels avec <c>pricing.manual</c> ; aucune conversion de devise ; montants
/// lus en chaînes décimales ; cellules de type formule refusées. Assistant IA LOCAL facultatif (UX-IA) : une seule
/// analyse par fichier au dépôt, hors transaction, propositions validées par <see cref="ImportAssistantMerge"/> ;
/// indisponible, en échec ou hors schéma : le moteur déterministe seul s'applique, sans erreur pour l'utilisateur.
/// Modèle d'import du fournisseur APPRIS à chaque application réussie (même transaction).
/// </summary>
public sealed partial class ImportService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IPricingStore pricing, IImportStore imports, IImportProfileStore profiles, TimeProvider clock, IAuditRecorder audit,
    IImportMappingAssistant? assistant = null)
{
    /// <summary>Lignes lues pour choisir les exemples transmis à l'assistant (quelques valeurs par colonne seulement).</summary>
    private const int AssistantSampleRows = 50;

    /// <summary>Champs monétaires dont le séparateur décimal est détecté ou confirmé (<see cref="ImportOptions.DecimalSeparator"/>).</summary>
    public static readonly IReadOnlySet<string> MoneyFields = new HashSet<string>(StringComparer.Ordinal) { "purchasePriceHt", "coefficient", "salePriceHt" };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyDictionary<string, IReadOnlyList<ImportField>> Fields { get; } = new Dictionary<string, IReadOnlyList<ImportField>>(StringComparer.Ordinal)
    {
        [ImportKinds.Catalog] =
        [
            new("familyCode", true, "famille", "family", "familycode"),
            new("brand", false, "marque", "brand", "fabricantmonture"),
            new("modelName", true, "modele", "model", "modelname", "nommodele"),
            new("modelDescription", false, "description", "descriptionmodele"),
            new("variantLabel", true, "variante", "libelle", "libellevariante", "label", "variantlabel"),
            // SKU facultatif (COR-05-04) : absent, la ligne est rapprochée par référence fournisseur ou code-barres, sinon
            // un SKU est généré par le serveur à l'application.
            new("sku", false, "sku", "referenceinterne", "refinterne"),
            new("color", false, "couleur", "coloris", "color"),
            new("manufacturerReference", false, "referencefabricant", "reffabricant", "manufacturerreference"),
            new("barcode", false, "codebarres", "ean", "gtin", "barcode"),
            new("stockPolicy", true, "politique", "politiquestock", "stockpolicy"),
            new("lensManufacturer", false, "fabricantverre", "fabricant", "lensmanufacturer"),
            new("lensRange", false, "gamme", "lensrange"),
            new("lensIndex", false, "indice", "lensindex"),
            new("lensTreatments", false, "traitements", "lenstreatments"),
            new("supplierCode", false, "codefournisseur", "fournisseur", "suppliercode"),
            new("supplierReference", false, "referencefournisseur", "reffournisseur", "supplierreference"),
            new("primarySupplier", false, "principal", "fournisseurprincipal", "primarysupplier"),
        ],
        [ImportKinds.Suppliers] =
        [
            new("code", true, "code", "codefournisseur"),
            new("name", true, "nom", "name", "raisonsociale"),
            new("countryCode", true, "pays", "country", "countrycode"),
            new("contactName", false, "contact", "contactname"),
            new("email", false, "email", "courriel", "mail"),
            new("phone", false, "telephone", "tel", "phone"),
            new("addressLine", false, "adresse", "address", "addressline"),
            new("postalCode", false, "codepostal", "cp", "postalcode"),
            new("city", false, "ville", "city"),
            new("notes", false, "notes", "remarques"),
        ],
        [ImportKinds.Prices] =
        [
            // SKU ou référence fournisseur (fichier d'un fournisseur désigné) : l'un des deux est requis.
            new("sku", false, "sku", "referenceinterne", "refinterne"),
            new("supplierReference", false, "referencefournisseur", "reffournisseur", "supplierreference"),
            new("purchasePriceHt", false, "prixachatht", "prixachat", "purchasepriceht"),
            new("coefficient", false, "coefficient", "coef"),
            new("salePriceHt", false, "prixventeht", "prixvente", "salepriceht"),
            new("currency", false, "devise", "currency"),
        ],
    };

    // ---------- dépôt ----------

    public async Task<Outcome<ImportPreview>> UploadAsync(
        string? kind, Guid? storeId, string? fileName, string? content, Guid requestId, Guid userId, CancellationToken cancellationToken, Guid? supplierId = null)
    {
        if (await imports.FindBatchByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.UploadedByUserId == userId
                ? Outcome.Ok(await PreviewAsync(existing, CsvTable.Parse(existing.Content, ImportBatch.MaxRows).Table!, cancellationToken), replayed: true)
                : Outcome.Conflict<ImportPreview>("request_id_reused");
        }

        if (supplierId is { } sid && (kind == ImportKinds.Suppliers || await store.FindSupplierAsync(sid, cancellationToken) is null or { IsArchived: true }))
        {
            return Outcome.Invalid<ImportPreview>("supplierId", kind == ImportKinds.Suppliers ? CatalogFieldErrors.NotAllowed : CatalogFieldErrors.Unknown);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (kind is null || !ImportKinds.All.Contains(kind))
        {
            errors["kind"] = [kind is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown];
        }

        if (kind == ImportKinds.Prices ? storeId is null : storeId is not null)
        {
            errors["storeId"] = [kind == ImportKinds.Prices ? CatalogFieldErrors.Required : CatalogFieldErrors.NotAllowed];
        }

        var name = CatalogText.Clean(fileName, "fileName", ImportBatch.FileNameMaxLength, required: true, errors);
        if (string.IsNullOrEmpty(content))
        {
            errors["content"] = [CatalogFieldErrors.Required];
        }
        else if (Encoding.UTF8.GetByteCount(content) > ImportBatch.MaxBytes)
        {
            errors["content"] = [CatalogFieldErrors.TooLong];
        }

        if (errors.Count > 0)
        {
            return Outcome.Invalid<ImportPreview>(errors);
        }

        var parsing = Stopwatch.StartNew();
        var (table, parseError) = CsvTable.Parse(content!, ImportBatch.MaxRows);
        var parseMs = parsing.ElapsedMilliseconds;
        if (table is null)
        {
            return Outcome.Invalid<ImportPreview>("content", parseError!);
        }

        if (storeId is { } id && await store.GetStoreAsync(id, cancellationToken) is null or { IsArchived: true })
        {
            return Outcome.Invalid<ImportPreview>("storeId", CatalogFieldErrors.Unknown);
        }

        var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content!)));
        var now = CatalogClock.Now(clock);
        var profile = supplierId is { } owner ? await profiles.FindAsync(owner, kind!, cancellationToken) : null;
        var (_, rules) = Propose(kind!, table, supplierId is not null, profile);
        var reviewed = profile is null ? null : JsonSerializer.Deserialize<List<string>>(profile.HeadersJson, Json);
        var analysis = await AnalyzeAsync(kind!, table, rules, supplierId is not null, reviewed, cancellationToken);
        var (mapping, suggestions) = Propose(kind!, table, supplierId is not null, profile, analysis);
        var batch = ImportBatch.Upload(
            Guid.CreateVersion7(now), kind!, storeId, name!, sha, content!, table.Rows.Count, JsonSerializer.Serialize(mapping, Json), userId, now, requestId, supplierId);
        imports.Add(batch);
        CatalogAudit.Record(audit, userId, now, AuditActions.ImportsModule, AuditActions.ImportUploaded, AuditActions.ImportEntity, batch.Id,
        [
            CatalogAudit.Field("kind", null, kind), CatalogAudit.Field("lineCount", null, table.Rows.Count.ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("assistantStatus", null, analysis.Status), CatalogAudit.Field("assistantModel", null, analysis.Model),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_batches_creation_request_id" }
                && await imports.FindBatchByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.UploadedByUserId == userId
                ? Outcome.Ok(await PreviewAsync(replay, table, cancellationToken), replayed: true)
                : Outcome.Conflict<ImportPreview>();
        }

        var summary = new ImportAnalysis(analysis.Status, analysis.Model, parseMs, analysis.InferenceMs, ImportAssistantMerge.ValidWarnings(analysis.Warnings, table.Headers));
        return Outcome.Created(await PreviewAsync(batch, table, cancellationToken, suggestions, summary));
    }

    /// <summary>
    /// Analyse par l'assistant IA local, UNE fois par fichier, seulement s'il reste une colonne non reconnue, une ambiguïté
    /// ou un champ requis absent. Aucune exception ne remonte (hors annulation) : échec = moteur déterministe seul.
    /// </summary>
    private async Task<AssistantAnalysis> AnalyzeAsync(
        string kind, CsvTable table, IReadOnlyList<MappingSuggestion> rules, bool fromSupplier, IReadOnlyCollection<string>? reviewed, CancellationToken cancellationToken)
    {
        if (assistant is null)
        {
            return AssistantAnalysis.Failed(AssistantStatuses.Disabled, null);
        }

        if (!ImportAssistantMerge.Needed(kind, table.Headers, rules, reviewed))
        {
            return AssistantAnalysis.Failed(AssistantStatuses.NotNeeded, null);
        }

        var request = ImportAssistantMerge.BuildRequest(kind, table.Headers, [.. table.Rows.Take(AssistantSampleRows).Select(r => r.Cells)], fromSupplier);
        return await assistant.AnalyzeAsync(request, cancellationToken);
    }

    /// <summary>
    /// Correspondance proposée (COR-05-04) : modèle du fournisseur, synonymes, contexte ; séparateur décimal détecté sur
    /// les colonnes monétaires (option ajoutée seulement s'il est certain — sinon l'écran demande confirmation).
    /// </summary>
    private static (Dictionary<string, string> Mapping, IReadOnlyList<MappingSuggestion> Suggestions) Propose(
        string kind, CsvTable table, bool supplierKnown, SupplierImportProfile? profile, AssistantAnalysis? analysis = null)
    {
        var saved = profile is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(profile.MappingJson, Json);
        var suggestions = ImportMapper.Suggest(kind, table.Headers, supplierKnown, saved);
        if (analysis is not null)
        {
            suggestions = ImportAssistantMerge.Merge(kind, table.Headers, suggestions, analysis, ImportAssistantMerge.Samples(table.Headers, [.. table.Rows.Take(AssistantSampleRows).Select(r => r.Cells)]));
        }

        var mapping = ImportMapper.Applied(suggestions);
        foreach (var (key, value) in saved ?? [])
        {
            if (ImportOptions.IsOption(key) && ImportOptions.All.Contains(key))
            {
                mapping[key] = value;
            }
        }

        if (!mapping.ContainsKey(ImportOptions.DecimalSeparator) && DetectDecimal(table, mapping) is { Separator: { } separator })
        {
            mapping[ImportOptions.DecimalSeparator] = separator.ToString();
        }

        return (mapping, suggestions);
    }

    private static (char? Separator, bool Ambiguous) DetectDecimal(CsvTable table, IReadOnlyDictionary<string, string> mapping)
    {
        var columns = mapping.Where(m => MoneyFields.Contains(m.Key)).Select(m => table.Headers.ToList().IndexOf(m.Value)).Where(i => i >= 0).ToList();
        return columns.Count == 0 ? (null, false) : NumberText.DetectColumn(table.Rows.SelectMany(r => columns.Where(c => c < r.Cells.Length).Select(c => r.Cells[c])));
    }

    /// <summary>Enregistre (ou remplace) le modèle d'import du fournisseur à partir d'une correspondance validée.</summary>
    public async Task<Outcome<Guid>> SaveProfileAsync(Guid supplierId, string kind, IReadOnlyDictionary<string, string>? mapping, IReadOnlyList<string>? headers,
        Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (kind is not (ImportKinds.Catalog or ImportKinds.Prices))
        {
            return Outcome.Missing<Guid>();
        }

        if (await store.FindSupplierAsync(supplierId, cancellationToken) is null or { IsArchived: true })
        {
            return Outcome.Invalid<Guid>("supplierId", CatalogFieldErrors.Unknown);
        }

        if (mapping is null || headers is null || headers.Count == 0 || headers.Count > 200 || mapping.Count > 60
            || CheckMapping(kind, mapping, headers, requireFields: false) is { Count: > 0 })
        {
            return Outcome.Invalid<Guid>("mapping", CatalogFieldErrors.InvalidFormat);
        }

        var now = CatalogClock.Now(clock);
        var mappingJson = JsonSerializer.Serialize(mapping, Json);
        var headersJson = JsonSerializer.Serialize(headers, Json);
        var profile = await profiles.FindAsync(supplierId, kind, cancellationToken);
        if (profile?.LastRequestId == requestId)
        {
            return Outcome.Ok(profile.Id, replayed: true);
        }

        if (profile is null)
        {
            profile = SupplierImportProfile.Create(Guid.CreateVersion7(now), supplierId, kind, mappingJson, headersJson, userId, now, requestId);
            profiles.Add(profile);
        }
        else
        {
            profile.Update(mappingJson, headersJson, userId, now, requestId);
        }

        CatalogAudit.Record(audit, userId, now, AuditActions.ImportsModule, AuditActions.ImportProfileSaved, AuditActions.ImportProfileEntity, profile.Id,
            [CatalogAudit.Field("supplierId", null, supplierId.ToString()), CatalogAudit.Field("kind", null, kind)]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? Outcome.Ok(profile.Id) : Outcome.Conflict<Guid>();
    }

    public Task<SupplierImportProfile?> FindProfileAsync(Guid supplierId, string kind, CancellationToken cancellationToken) =>
        profiles.FindAsync(supplierId, kind, cancellationToken);

    // ---------- contrôle à blanc ----------

    /// <summary>Contrôle à blanc : rapport par ligne enregistré, AUCUNE écriture au catalogue. Recontrôle possible tant que le lot n'est pas clos.</summary>
    public async Task<Outcome<ImportSummary>> CheckAsync(
        Guid batchId, IReadOnlyDictionary<string, string>? mapping, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var batch = await imports.FindBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return Outcome.Missing<ImportSummary>();
        }

        if (batch.LastRequestId == requestId)
        {
            return Outcome.Ok(await SummaryAsync(batch, cancellationToken), replayed: true);
        }

        if (batch.Version != version)
        {
            return Outcome.Conflict<ImportSummary>();
        }

        if (batch.Status is ImportStatuses.Applied or ImportStatuses.Cancelled)
        {
            return Outcome.Conflict<ImportSummary>("import_closed");
        }

        if (IsAssistedBatch(batch))
        {
            // Lot du nouveau parcours : contrôlé et appliqué par ses routes (/api/imports/{id}/assistant/…) seulement.
            return Outcome.Conflict<ImportSummary>("pipeline_mismatch");
        }

        var table = CsvTable.Parse(batch.Content, ImportBatch.MaxRows).Table!;
        var effective =mapping ?? JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson, Json) ?? [];
        if (CheckMapping(batch.Kind, effective, table.Headers) is { Count: > 0 } mappingErrors)
        {
            return Outcome.Invalid<ImportSummary>(mappingErrors);
        }

        var validation = Stopwatch.StartNew();
        var plan = await PlanAsync(batch, table, effective, cancellationToken);
        var now = CatalogClock.Now(clock);
        await imports.ReplaceRowsAsync(batch.Id, [.. plan.Rows.Select(r => ImportRow.Create(
            batch.Id, r.LineNo, JsonSerializer.Serialize(r.Values, Json), r.Outcome, JsonSerializer.Serialize(r.Errors, Json), r.TargetKey))], cancellationToken);
        var errorCount = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Error);
        batch.MarkChecked(JsonSerializer.Serialize(effective, Json), errorCount, plan.ContainsCosts, plan.ContainsManualPrices, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.ImportsModule, AuditActions.ImportChecked, AuditActions.ImportEntity, batch.Id,
            [CatalogAudit.Field("lineCount", null, plan.Rows.Count.ToString(CultureInfo.InvariantCulture)), CatalogAudit.Field("status", null, ImportStatuses.Checked)]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? Outcome.Ok(Summary(batch, plan.Rows) with { ElapsedMs = validation.ElapsedMilliseconds }) : Outcome.Conflict<ImportSummary>();
    }

    // ---------- application ----------

    /// <summary>
    /// Application explicite, TOUT OU RIEN : le plan est recalculé contre l'état courant dans la transaction ; une seule
    /// ligne en erreur → 422 <c>import_has_errors</c>, rien n'est écrit. Coûts : <c>costs.write</c> ; prix manuels :
    /// <c>pricing.manual</c> (403 sinon). Même fichier déjà appliqué → 409 <c>import_already_applied</c>.
    /// </summary>
    public async Task<Outcome<ImportSummary>> ApplyAsync(Guid batchId, uint version, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var batch = await imports.FindBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return Outcome.Missing<ImportSummary>();
        }

        if (batch.LastRequestId == requestId)
        {
            return Outcome.Ok(await SummaryAsync(batch, cancellationToken), replayed: true);
        }

        if (batch.Version != version)
        {
            return Outcome.Conflict<ImportSummary>();
        }

        if (batch.Status != ImportStatuses.Checked)
        {
            return batch.Status == ImportStatuses.Uploaded ? Outcome.Refuse<ImportSummary>("import_not_checked") : Outcome.Conflict<ImportSummary>("import_closed");
        }

        if (IsAssistedBatch(batch))
        {
            return Outcome.Conflict<ImportSummary>("pipeline_mismatch");
        }

        if ((batch.ContainsCosts && !actor.Has(Permissions.CostsWrite)) || (batch.ContainsManualPrices && !actor.Has(Permissions.PricingManual)))
        {
            return Outcome.Forbidden<ImportSummary>(batch.ContainsCosts && !actor.Has(Permissions.CostsWrite) ? "costs_forbidden" : "manual_price_forbidden");
        }

        if (await imports.AppliedFileExistsAsync(batch.FileSha256, batch.Kind, batch.StoreId, cancellationToken))
        {
            return Outcome.Conflict<ImportSummary>("import_already_applied");
        }

        if (batch.StoreId is { } storeId && !await store.LockStoreAsync(storeId, cancellationToken))
        {
            return Outcome.Missing<ImportSummary>("store_not_found");
        }

        var table = CsvTable.Parse(batch.Content, ImportBatch.MaxRows).Table!;
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson, Json) ?? [];
        var plan = await PlanAsync(batch, table, mapping, cancellationToken);
        if (plan.Rows.Any(r => r.Outcome == ImportOutcomes.Error))
        {
            return Outcome.Refuse<ImportSummary>("import_has_errors", new { errors = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Error) });
        }

        if ((plan.ContainsCosts && !actor.Has(Permissions.CostsWrite)) || (plan.ContainsManualPrices && !actor.Has(Permissions.PricingManual)))
        {
            return Outcome.Forbidden<ImportSummary>(plan.ContainsCosts && !actor.Has(Permissions.CostsWrite) ? "costs_forbidden" : "manual_price_forbidden");
        }

        var now = CatalogClock.Now(clock);
        foreach (var action in plan.Actions)
        {
            await action(actor.UserId, now, requestId, cancellationToken);
        }

        if (batch.SupplierId is { } supplierId && batch.Kind is ImportKinds.Catalog or ImportKinds.Prices)
        {
            await LearnProfileAsync(supplierId, batch.Kind, mapping, table.Headers, actor.UserId, now, requestId, cancellationToken);
        }

        batch.MarkApplied(actor.UserId, now, requestId);
        var summary = Summary(batch, plan.Rows);
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.ImportsModule, AuditActions.ImportApplied, AuditActions.ImportEntity, batch.Id,
        [
            CatalogAudit.Field("kind", null, batch.Kind),
            CatalogAudit.Field("lineCount", null, (summary.Create + summary.Update).ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("status", ImportStatuses.Checked, ImportStatuses.Applied),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_batches_applied_file" }
                ? Outcome.Conflict<ImportSummary>("import_already_applied")
                : CatalogService.UniqueRefusal<ImportSummary>(commit);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Ok(summary with { ElapsedMs = elapsed.ElapsedMilliseconds });
    }

    /// <summary>
    /// Apprentissage (UX-IA) : la correspondance d'un import APPLIQUÉ devient le modèle d'import du fournisseur (créé ou
    /// remplacé, tracé), reproposé à son prochain fichier. Même transaction que l'application : rien n'est appris d'un
    /// import refusé. Fournisseur archivé : rien n'est appris. Le séparateur décimal n'est JAMAIS appris (détecté à chaque
    /// fichier) : un montant ambigu (« 1,234 ») du fichier suivant reste signalé au lieu d'être interprété en silence.
    /// </summary>
    private async Task LearnProfileAsync(Guid supplierId, string kind, Dictionary<string, string> mapping, IReadOnlyList<string> headers,
        Guid userId, DateTimeOffset now, Guid requestId, CancellationToken cancellationToken)
    {
        if (await store.FindSupplierAsync(supplierId, cancellationToken) is null or { IsArchived: true }
            || headers.Count > 200 || mapping.Count > 60 || CheckMapping(kind, mapping, headers, requireFields: false).Count > 0)
        {
            return;
        }

        var mappingJson = JsonSerializer.Serialize(mapping.Where(m => m.Key != ImportOptions.DecimalSeparator).ToDictionary(StringComparer.Ordinal), Json);
        var headersJson = JsonSerializer.Serialize(headers, Json);
        var profile = await profiles.FindAsync(supplierId, kind, cancellationToken);
        if (profile is not null && profile.MappingJson == mappingJson && profile.HeadersJson == headersJson)
        {
            return;
        }

        if (profile is null)
        {
            profile = SupplierImportProfile.Create(Guid.CreateVersion7(now), supplierId, kind, mappingJson, headersJson, userId, now, requestId);
            profiles.Add(profile);
        }
        else
        {
            profile.Update(mappingJson, headersJson, userId, now, requestId);
        }

        CatalogAudit.Record(audit, userId, now, AuditActions.ImportsModule, AuditActions.ImportProfileSaved, AuditActions.ImportProfileEntity, profile.Id,
            [CatalogAudit.Field("supplierId", null, supplierId.ToString()), CatalogAudit.Field("kind", null, kind)]);
    }

    /// <summary>Abandon d'un lot non appliqué (définitif, sans effet sur le catalogue).</summary>
    public async Task<Outcome<ImportSummary>> CancelAsync(Guid batchId, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var batch = await imports.FindBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return Outcome.Missing<ImportSummary>();
        }

        if (batch.LastRequestId == requestId)
        {
            return Outcome.Ok(await SummaryAsync(batch, cancellationToken), replayed: true);
        }

        if (batch.Version != version)
        {
            return Outcome.Conflict<ImportSummary>();
        }

        if (batch.Status is ImportStatuses.Applied or ImportStatuses.Cancelled)
        {
            return Outcome.Conflict<ImportSummary>("import_closed");
        }

        var now = CatalogClock.Now(clock);
        batch.Cancel(requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.ImportsModule, AuditActions.ImportCancelled, AuditActions.ImportEntity, batch.Id,
            [CatalogAudit.Field("status", null, ImportStatuses.Cancelled)]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? Outcome.Ok(await SummaryAsync(batch, cancellationToken)) : Outcome.Conflict<ImportSummary>();
    }

    public async Task<ImportSummary> SummaryAsync(ImportBatch batch, CancellationToken cancellationToken) =>
        Summary(batch, [.. (await imports.RowsAsync(batch.Id, cancellationToken)).Select(r => new RowPlan(r.LineNo, new Dictionary<string, string>(), r.Outcome, [], r.TargetKey))]);

    // ---------- plan ----------

    private delegate Task ImportAction(Guid userId, DateTimeOffset now, Guid requestId, CancellationToken cancellationToken);

    private sealed record RowPlan(int LineNo, Dictionary<string, string> Values, string Outcome, List<ImportError> Errors, string? TargetKey);

    private sealed record Plan(List<RowPlan> Rows, List<ImportAction> Actions, bool ContainsCosts, bool ContainsManualPrices);

    private async Task<Plan> PlanAsync(ImportBatch batch, CsvTable table, IReadOnlyDictionary<string, string> mapping, CancellationToken cancellationToken)
    {
        var rows = table.Rows.Select(r => (r.LineNo, Values: Values(table, r.Cells, mapping))).ToList();
        var supplier = batch.SupplierId is { } sid ? await store.FindSupplierAsync(sid, cancellationToken) : null;
        var decimalSeparator = mapping.GetValueOrDefault(ImportOptions.DecimalSeparator) is { Length: 1 } d ? d[0] : (char?)null;
        return batch.Kind switch
        {
            ImportKinds.Suppliers => await PlanSuppliersAsync(rows, cancellationToken),
            ImportKinds.Prices => await PlanPricesAsync(batch.StoreId!.Value, supplier, decimalSeparator, rows, cancellationToken),
            _ => await PlanCatalogAsync(supplier, rows, cancellationToken),
        };
    }

    private async Task<Plan> PlanSuppliersAsync(List<(int LineNo, Dictionary<string, string> Values)> rows, CancellationToken cancellationToken)
    {
        var existing = await imports.SuppliersByCodeAsync([.. rows.Select(r => CatalogKeys.Normalize(r.Values.GetValueOrDefault("code") ?? string.Empty))], cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var plans = new List<RowPlan>();
        var actions = new List<ImportAction>();
        foreach (var (lineNo, values) in rows)
        {
            var errors = FormulaErrors(values);
            var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var details = CatalogInputValidator.Supplier(
                new SupplierInput(values.GetValueOrDefault("code"), values.GetValueOrDefault("name"), values.GetValueOrDefault("countryCode"), values.GetValueOrDefault("contactName"),
                    values.GetValueOrDefault("email"), values.GetValueOrDefault("phone"), values.GetValueOrDefault("addressLine"), values.GetValueOrDefault("postalCode"),
                    values.GetValueOrDefault("city"), null, values.GetValueOrDefault("notes")),
                fieldErrors);
            errors.AddRange(fieldErrors.Select(e => new ImportError(e.Key, e.Value[0])));
            var key = details is null ? null : CatalogKeys.Normalize(details.Code);
            if (key is not null && !seen.Add(key))
            {
                errors.Add(new("code", CatalogFieldErrors.Duplicate));
            }

            if (errors.Count > 0 || details is null)
            {
                plans.Add(new(lineNo, values, ImportOutcomes.Error, errors, key));
                continue;
            }

            if (existing.TryGetValue(key!, out var supplier))
            {
                if (supplier.IsArchived)
                {
                    plans.Add(new(lineNo, values, ImportOutcomes.Error, [new("code", CatalogFieldErrors.Archived)], key));
                    continue;
                }

                var changed = details.ChangedFields(supplier.Snapshot());
                plans.Add(new(lineNo, values, changed.Count == 0 ? ImportOutcomes.Unchanged : ImportOutcomes.Update, [], key));
                if (changed.Count > 0)
                {
                    actions.Add(async (userId, now, requestId, ct) =>
                    {
                        var tracked = (await store.FindSupplierAsync(supplier.Id, ct))!;
                        tracked.Update(details, userId, now, requestId);
                        CatalogAudit.Record(audit, userId, now, AuditActions.SuppliersModule, AuditActions.SupplierUpdated, AuditActions.SupplierEntity, tracked.Id, CatalogAudit.Fields(changed));
                    });
                }
            }
            else
            {
                plans.Add(new(lineNo, values, ImportOutcomes.Create, [], key));
                actions.Add((userId, now, requestId, _) =>
                {
                    var created = Supplier.Create(Guid.CreateVersion7(now), details, Guid.CreateVersion7(now), userId, now);
                    store.Add(created);
                    CatalogAudit.Record(audit, userId, now, AuditActions.SuppliersModule, AuditActions.SupplierCreated, AuditActions.SupplierEntity, created.Id,
                        CatalogAudit.Fields(details.ChangedFields(null)));
                    return Task.CompletedTask;
                });
            }
        }

        return new(plans, actions, false, false);
    }

    private async Task<Plan> PlanPricesAsync(
        Guid storeId, Supplier? fileSupplier, char? decimalSeparator, List<(int LineNo, Dictionary<string, string> Values)> rows, CancellationToken cancellationToken)
    {
        var shop = (await store.GetStoreAsync(storeId, cancellationToken))!;
        var bySku = await imports.VariantsBySkuAsync([.. rows.Select(r => CatalogKeys.Normalize(r.Values.GetValueOrDefault("sku") ?? string.Empty))], cancellationToken);
        var byReference = fileSupplier is null
            ? new Dictionary<string, ProductVariant>(StringComparer.Ordinal)
            : await imports.VariantsBySupplierReferenceAsync(fileSupplier.Id, [.. rows.Select(r => r.Values.GetValueOrDefault("supplierReference")).OfType<string>().Select(CatalogKeys.Normalize)], cancellationToken);
        var variants = bySku.Concat(byReference.Where(r => !bySku.ContainsKey("ref:" + r.Key)).Select(r => KeyValuePair.Create("ref:" + r.Key, r.Value)))
            .ToDictionary(StringComparer.Ordinal);
        var prices = (await pricing.PricesAsync(storeId, [.. variants.Values.Select(v => v.Id)], cancellationToken)).ToDictionary(p => p.VariantId);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var plans = new List<RowPlan>();
        var actions = new List<ImportAction>();
        bool costs = false, manual = false;
        foreach (var (lineNo, values) in rows)
        {
            var errors = FormulaErrors(values);
            var sku = values.GetValueOrDefault("sku");
            var reference = values.GetValueOrDefault("supplierReference");
            var key = sku is not null ? CatalogKeys.Normalize(sku) : reference is not null && fileSupplier is not null ? "ref:" + CatalogKeys.Normalize(reference) : null;
            ProductVariant? variant = null;
            if (key is null)
            {
                errors.Add(new(reference is null ? "sku" : "supplierReference", reference is null ? CatalogFieldErrors.Required : "supplier_required"));
            }
            else if (!seen.Add(key))
            {
                errors.Add(new("sku", CatalogFieldErrors.Duplicate));
            }
            else if (!variants.TryGetValue(key, out variant))
            {
                errors.Add(new("sku", CatalogFieldErrors.Unknown));
            }
            else if (variant.IsArchived)
            {
                errors.Add(new("sku", CatalogFieldErrors.Archived));
            }

            if (values.GetValueOrDefault("currency") is { } currency && !string.Equals(currency, shop.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                // Aucune conversion : la devise du fichier doit être celle du magasin cible.
                errors.Add(new("currency", "currency_mismatch"));
            }

            var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            // Montants d'un fichier fournisseur : virgule ou point, milliers, symbole de devise (COR-05-04) ; ambigu ⇒ erreur.
            string? Amount(string field, string? expectedCurrency)
            {
                if (!values.TryGetValue(field, out var raw))
                {
                    return null;
                }

                var (normalized, error) = NumberText.Normalize(raw, decimalSeparator, expectedCurrency);
                if (error is not null)
                {
                    errors.Add(new(field, error));
                }

                return normalized;
            }

            var purchaseText = Amount("purchasePriceHt", shop.CurrencyCode);
            var coefficientText = Amount("coefficient", null);
            var saleText = Amount("salePriceHt", shop.CurrencyCode);
            var purchase = purchaseText is not null ? PricingService.Purchase(purchaseText, fieldErrors, required: false) : null;
            var coefficient = coefficientText is not null ? PricingService.Coefficient(coefficientText, fieldErrors) : null;
            var sale = saleText is not null ? PricingService.Sale(saleText, "salePriceHt", fieldErrors) : null;
            errors.AddRange(fieldErrors.Select(e => new ImportError(e.Key, e.Value[0])));
            if (purchase is null && coefficient is null && sale is null && fieldErrors.Count == 0 && !errors.Any(e => MoneyFields.Contains(e.Field)))
            {
                errors.Add(new("salePriceHt", CatalogFieldErrors.Required));
            }

            var current = variant is null ? null : prices.GetValueOrDefault(variant.Id);
            var effectivePurchase = purchase ?? current?.PurchasePriceHt;
            decimal? computed = null;
            if (coefficient is { } coef)
            {
                if (coef < 1)
                {
                    // Vente sous le coût : confirmation explicite exigée, impossible par import (écran de tarif).
                    errors.Add(new("coefficient", "coefficient_below_one"));
                }

                if (effectivePurchase is null)
                {
                    errors.Add(new("purchasePriceHt", CatalogFieldErrors.Required));
                }
                else
                {
                    computed = SalePriceCalculator.Compute(effectivePurchase.Value, coef);
                    if (sale is not null && sale != computed)
                    {
                        errors.Add(new("salePriceHt", "sale_price_conflicts_coefficient"));
                    }
                }
            }

            if (errors.Count > 0 || variant is null)
            {
                plans.Add(new(lineNo, values, ImportOutcomes.Error, errors, key));
                continue;
            }

            var setsManual = coefficient is null && sale is not null;
            costs |= purchase is not null || coefficient is not null;
            manual |= setsManual;
            var unchanged = (purchase is null || purchase == current?.PurchasePriceHt)
                && (coefficient is null || (coefficient == current?.Coefficient && current.Mode == PricingMode.Coefficient && current.SalePriceHt == computed))
                && (!setsManual || (current?.Mode == PricingMode.Manual && current.SalePriceHt == sale));
            plans.Add(new(lineNo, values, unchanged ? ImportOutcomes.Unchanged : current is null ? ImportOutcomes.Create : ImportOutcomes.Update, [], key));
            if (unchanged)
            {
                continue;
            }

            var variantId = variant.Id;
            actions.Add(async (userId, now, requestId, ct) =>
            {
                var (price, _) = await pricing.LockPriceAsync(variantId, storeId, shop.CurrencyCode, userId, ct);
                var before = (Purchase: price.PurchasePriceHt, Coefficient: price.Coefficient, Mode: price.PricingModeCode, Sale: price.SalePriceHt);
                if (coefficient is { } c)
                {
                    price.SetCoefficientPrice(effectivePurchase, c, SalePriceCalculator.Compute(effectivePurchase!.Value, c), userId, now, requestId);
                }
                else
                {
                    if (purchase is not null)
                    {
                        // Prix d'achat seul : le prix de vente ne change pas (recalcul en attente si mode coefficient).
                        price.SetPurchasePrice(purchase, userId, now, requestId);
                    }

                    if (sale is { } manualSale)
                    {
                        price.SetManualPrice(manualSale, userId, now, requestId);
                    }
                }

                var fields = new List<AuditChange> { CatalogAudit.Field("storeId", null, storeId.ToString()) };
                void History(string field, string auditName, decimal? old, decimal? value)
                {
                    if (old != value)
                    {
                        pricing.Add(PriceHistoryEntry.Value(Guid.CreateVersion7(now), variantId, storeId, null, shop.CurrencyCode, field, old, value, PriceOrigins.Import, null, userId, now, requestId));
                        fields.Add(CatalogAudit.Field(auditName));
                    }
                }

                History(PriceFields.PurchasePriceHt, "purchasePriceHt", before.Purchase, price.PurchasePriceHt);
                History(PriceFields.Coefficient, "coefficient", before.Coefficient, price.Coefficient);
                if (before.Mode != price.PricingModeCode)
                {
                    pricing.Add(PriceHistoryEntry.ModeChange(Guid.CreateVersion7(now), variantId, storeId, shop.CurrencyCode, before.Mode, price.PricingModeCode, PriceOrigins.Import, null, userId, now, requestId));
                    fields.Add(CatalogAudit.Field("pricingMode", before.Mode, price.PricingModeCode));
                }

                History(PriceFields.SalePriceHt, "salePriceHt", before.Sale, price.SalePriceHt);
                CatalogAudit.Record(audit, userId, now, AuditActions.PricingModule, AuditActions.PriceChanged, AuditActions.VariantEntity, variantId, fields);
            });
        }

        return new(plans, actions, costs, manual);
    }

    /// <summary>
    /// Plan du catalogue. Parcours actuel : inchangé. Nouveau parcours (CSV-IMPORT-AI-01) : clés préfixées SEULEMENT
    /// produites par <c>CatalogMaterializer</c> — attributs de variante <c>attr:</c>, attributs de modèle <c>modelattr:</c>
    /// (modèle créé), montants confirmés <c>price:</c> écrits au tarif du magasin <paramref name="priceStoreId"/>.
    /// </summary>
    private async Task<Plan> PlanCatalogAsync(
        Supplier? fileSupplier, List<(int LineNo, Dictionary<string, string> Values)> rows, CancellationToken cancellationToken, Guid? priceStoreId = null)
    {
        var shop = priceStoreId is { } psid ? await store.GetStoreAsync(psid, cancellationToken) : null;
        var costs = false;
        var manual = false;
        var variants = (await imports.VariantsBySkuAsync([.. rows.Select(r => CatalogKeys.Normalize(r.Values.GetValueOrDefault("sku") ?? string.Empty))], cancellationToken))
            .ToDictionary(StringComparer.Ordinal);
        // Lignes sans SKU (COR-05-04) : rapprochement par référence fournisseur (fournisseur du fichier ou de la ligne), puis par
        // code-barres ; sinon création avec un SKU généré par le serveur.
        var unkeyed = rows.Where(r => r.Values.GetValueOrDefault("sku") is null).ToList();
        var byReference = fileSupplier is null
            ? new Dictionary<string, ProductVariant>(StringComparer.Ordinal)
            : await imports.VariantsBySupplierReferenceAsync(fileSupplier.Id, [.. unkeyed.Select(r => r.Values.GetValueOrDefault("supplierReference")).OfType<string>().Select(CatalogKeys.Normalize)], cancellationToken);
        var byBarcode = await imports.VariantsByBarcodeAsync([.. unkeyed.Select(r => r.Values.GetValueOrDefault("barcode")).OfType<string>().Distinct()], cancellationToken);
        foreach (var (lineNo, values) in unkeyed)
        {
            var matched = values.GetValueOrDefault("supplierReference") is { } reference && values.GetValueOrDefault("supplierCode") is null
                && byReference.TryGetValue(CatalogKeys.Normalize(reference), out var viaReference)
                ? viaReference
                : values.GetValueOrDefault("barcode") is { } code && byBarcode.TryGetValue(code, out var viaBarcode) ? viaBarcode : null;
            if (matched is not null)
            {
                values["sku"] = matched.Sku;
                variants.TryAdd(CatalogKeys.Normalize(matched.Sku), matched);
            }
        }

        var families = rows.Select(r => FamilyCode(r.Values.GetValueOrDefault("familyCode"))).OfType<string>().Distinct().ToList();
        var models = await imports.ModelsByKeyAsync(families, cancellationToken);
        var suppliers = await imports.SuppliersByCodeAsync([.. rows.Select(r => CatalogKeys.Normalize(r.Values.GetValueOrDefault("supplierCode") ?? string.Empty))], cancellationToken);
        var lens = await store.ListLensReferencesAsync(cancellationToken);
        var existingModels = variants.Values.Select(v => v.ModelId).Distinct().ToList();
        var modelById = new Dictionary<Guid, ProductModel>();
        foreach (var id in existingModels)
        {
            if (await store.FindModelAsync(id, cancellationToken) is { } model)
            {
                modelById[id] = model;
            }
        }

        var barcodes = rows.Select(r => r.Values.GetValueOrDefault("barcode")).OfType<string>().Distinct().ToList();
        var takenBarcodes = barcodes.Count == 0 ? new HashSet<string>() : await store.TakenBarcodesAsync(barcodes, null, cancellationToken);
        var seenSku = new HashSet<string>(StringComparer.Ordinal);
        var seenBarcode = new HashSet<string>(StringComparer.Ordinal);
        // UX-IA : une référence identifie UN article chez un fournisseur — répétée dans le même fichier, la ligne est refusée.
        var seenSupplierReference = new HashSet<string>(StringComparer.Ordinal);
        var newModels = new Dictionary<string, (ProductModelDetails Details, Guid? Id)>(StringComparer.Ordinal);
        var plans = new List<RowPlan>();
        var actions = new List<ImportAction>();
        foreach (var (lineNo, values) in rows)
        {
            var errors = FormulaErrors(values);
            var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var familyCode = FamilyCode(values.GetValueOrDefault("familyCode"));
            var family = Families.Find(familyCode);
            Guid? rangeId = null;
            Guid? indexId = null;
            var treatmentIds = new List<Guid>();
            if (family is { IsOphthalmicLens: true })
            {
                var manufacturer = lens.FirstOrDefault(l => l.Kind == LensReferenceKind.Manufacturer && !l.IsArchived && Same(l.Name, values.GetValueOrDefault("lensManufacturer")));
                rangeId = lens.FirstOrDefault(l => l.Kind == LensReferenceKind.Range && !l.IsArchived && l.ManufacturerId == manufacturer?.Id && Same(l.Name, values.GetValueOrDefault("lensRange")))?.Id;
                if (rangeId is null)
                {
                    errors.Add(new("lensRange", CatalogFieldErrors.Unknown));
                }

                var indexValue = MoneyText.TryParse(values.GetValueOrDefault("lensIndex")?.Replace(',', '.'), 3);
                indexId = lens.FirstOrDefault(l => l.Kind == LensReferenceKind.Index && !l.IsArchived && l.IndexValue == indexValue)?.Id;
                if (indexId is null)
                {
                    errors.Add(new("lensIndex", CatalogFieldErrors.Unknown));
                }

                foreach (var name in (values.GetValueOrDefault("lensTreatments") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var treatment = lens.FirstOrDefault(l => l.Kind == LensReferenceKind.Treatment && !l.IsArchived && (l.ManufacturerId is null || l.ManufacturerId == manufacturer?.Id) && Same(l.Name, name));
                    if (treatment is null)
                    {
                        errors.Add(new("lensTreatments", CatalogFieldErrors.Unknown));
                    }
                    else
                    {
                        treatmentIds.Add(treatment.Id);
                    }
                }
            }

            // SKU toujours absent : nouvelle variante, SKU généré (provisoire au contrôle, définitif à l'application).
            var generatedSku = values.GetValueOrDefault("sku") is null;
            var skuInput = generatedSku ? SkuGenerator.Propose(familyCode) : values.GetValueOrDefault("sku");
            var attributeInput = values.GetValueOrDefault("color") is { } color && family?.VariantAttributes.Any(a => a.Key == "color") == true
                ? new Dictionary<string, string?>(StringComparer.Ordinal) { ["color"] = color }
                : null;
            if (values.ContainsKey("color") && attributeInput is null)
            {
                errors.Add(new("color", CatalogFieldErrors.NotAllowed));
            }

            // Nouveau parcours : attributs de famille (valeurs dérivées des cellules), contrôlés par CatalogInputValidator.
            foreach (var (key, value) in values.Where(v => v.Key.StartsWith("attr:", StringComparison.Ordinal)))
            {
                attributeInput ??= new Dictionary<string, string?>(StringComparer.Ordinal);
                attributeInput[key[5..]] = value;
            }

            var modelAttributes = values.Where(v => v.Key.StartsWith("modelattr:", StringComparison.Ordinal)).ToDictionary(v => v.Key[10..], v => (string?)v.Value, StringComparer.Ordinal);
            var modelDetails = CatalogInputValidator.Model(
                new ModelInput(familyCode ?? values.GetValueOrDefault("familyCode"), values.GetValueOrDefault("brand"), values.GetValueOrDefault("modelName"),
                    values.GetValueOrDefault("modelDescription"), rangeId, modelAttributes.Count > 0 ? modelAttributes : null),
                fieldErrors);
            var (purchase, sale) = AssistedPrices(values, shop, priceStoreId, errors);
            var policy = Policy(values.GetValueOrDefault("stockPolicy"));
            var (variantDetails, variantPolicy) = family is null
                ? (null, null)
                : CatalogInputValidator.Variant(
                    new VariantInput(skuInput, values.GetValueOrDefault("manufacturerReference"), values.GetValueOrDefault("barcode"),
                        values.GetValueOrDefault("variantLabel"), attributeInput, indexId, treatmentIds, policy ?? values.GetValueOrDefault("stockPolicy")),
                    family, requirePolicy: true, fieldErrors);
            errors.AddRange(fieldErrors.Where(e => !(family is { IsOphthalmicLens: true } && e.Key is "lensRangeId" or "lensIndexId"))
                .Select(e => new ImportError(Rename(e.Key), e.Value[0])));

            var skuKey = variantDetails is null ? null : generatedSku ? $"new:{lineNo}" : CatalogKeys.Normalize(variantDetails.Sku);
            if (skuKey is not null && !seenSku.Add(skuKey))
            {
                errors.Add(new("sku", CatalogFieldErrors.Duplicate));
            }

            if (variantDetails?.Barcode is { } barcode && !seenBarcode.Add(barcode))
            {
                errors.Add(new("barcode", CatalogFieldErrors.Duplicate));
            }

            Supplier? supplier = null;
            var primary = Boolean(values.GetValueOrDefault("primarySupplier"));
            if (values.GetValueOrDefault("primarySupplier") is not null && primary is null)
            {
                errors.Add(new("primarySupplier", CatalogFieldErrors.InvalidFormat));
            }

            var supplierReference = CatalogText.Clean(values.GetValueOrDefault("supplierReference"), "supplierReference", CatalogLimits.ReferenceMaxLength, required: false, fieldErrors);
            if (values.GetValueOrDefault("supplierCode") is { } supplierCode)
            {
                if (!suppliers.TryGetValue(CatalogKeys.Normalize(supplierCode), out supplier))
                {
                    errors.Add(new("supplierCode", CatalogFieldErrors.Unknown));
                }
                else if (supplier.IsArchived)
                {
                    errors.Add(new("supplierCode", CatalogFieldErrors.Archived));
                }
            }
            else if (fileSupplier is not null)
            {
                // Fichier d'un fournisseur désigné au dépôt : chaque ligne lui est rattachée (référence de la ligne).
                supplier = fileSupplier;
            }
            else if (supplierReference is not null || primary == true)
            {
                errors.Add(new("supplierCode", CatalogFieldErrors.Required));
            }

            if (supplierReference is not null && supplier is not null && !generatedSku && values.GetValueOrDefault("supplierCode") is null
                && byReference.TryGetValue(CatalogKeys.Normalize(supplierReference), out var referenced) && !string.Equals(referenced.Sku, values["sku"], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new("supplierReference", CatalogFieldErrors.Duplicate));
            }

            if (supplierReference is not null && supplier is not null && !seenSupplierReference.Add($"{supplier.Id:N}|{CatalogKeys.Normalize(supplierReference)}"))
            {
                errors.Add(new("supplierReference", CatalogFieldErrors.Duplicate));
            }

            if (errors.Count > 0 || modelDetails is null || variantDetails is null)
            {
                plans.Add(new(lineNo, values, ImportOutcomes.Error, errors, skuKey));
                continue;
            }

            var modelKey = ModelKey(modelDetails);
            if (!generatedSku && variants.TryGetValue(skuKey!, out var existing))
            {
                var owner = modelById[existing.ModelId];
                if (ModelKey(owner.Snapshot()) != modelKey)
                {
                    plans.Add(new(lineNo, values, ImportOutcomes.Error, [new("sku", "sku_belongs_to_other_model")], skuKey));
                    continue;
                }

                if (existing.IsArchived)
                {
                    plans.Add(new(lineNo, values, ImportOutcomes.Error, [new("sku", CatalogFieldErrors.Archived)], skuKey));
                    continue;
                }

                if (existing.StockPolicy != variantPolicy)
                {
                    plans.Add(new(lineNo, values, ImportOutcomes.Error, [new("stockPolicy", "policy_change_not_supported")], skuKey));
                    continue;
                }

                if (variantDetails.Barcode is { } code && code != existing.Barcode && takenBarcodes.Contains(code))
                {
                    plans.Add(new(lineNo, values, ImportOutcomes.Error, [new("barcode", CatalogFieldErrors.Duplicate)], skuKey));
                    continue;
                }

                // Attributs non importés : ceux de la fiche sont conservés (le coloris importé remplace le coloris).
                var mergedAttributes = existing.Attributes.ToDictionary(StringComparer.Ordinal);
                foreach (var (attribute, value) in variantDetails.Attributes)
                {
                    mergedAttributes[attribute] = value;
                }

                // GATE D (07/11/2026, décision « aucun effacement d'une valeur existante lorsqu'une colonne est absente ») :
                // référence fabricant, code-barres et traitements que le fichier ne fournit pas conservent la valeur de la fiche
                // (avant correction, une colonne absente effaçait la valeur existante lors d'une mise à jour).
                var current = existing.Snapshot();
                var merged = variantDetails with
                {
                    Attributes = mergedAttributes,
                    ManufacturerReference = values.ContainsKey("manufacturerReference") ? variantDetails.ManufacturerReference : current.ManufacturerReference,
                    Barcode = values.ContainsKey("barcode") ? variantDetails.Barcode : current.Barcode,
                    TreatmentIds = values.ContainsKey("lensTreatments") ? variantDetails.TreatmentIds : current.TreatmentIds,
                };
                var same = current.SameAs(merged);
                // Rattachement inchangé (même fournisseur, même référence, même rôle) : la ligne reste « inchangée ».
                var linkChange = supplier is not null && !(await store.LinksForVariantAsync(existing.Id, cancellationToken)).Any(l =>
                    l.SupplierId == supplier.Id && !l.IsRemoved && l.SupplierReference == (supplierReference ?? l.SupplierReference) && (primary is null || l.IsPrimary == primary));
                var priceChange = shop is not null && (purchase is not null || sale is not null) && await AssistedPriceDiffersAsync(existing.Id, shop.Id, purchase, sale, cancellationToken);
                plans.Add(new(lineNo, values, same && !linkChange && !priceChange ? ImportOutcomes.Unchanged : ImportOutcomes.Update, [], skuKey));
                var existingId = existing.Id;
                if (priceChange)
                {
                    costs |= purchase is not null;
                    manual |= sale is not null;
                    actions.Add(AssistedPriceAction(existingId, shop!, purchase, sale));
                }

                if (!same)
                {
                    actions.Add(async (userId, now, requestId, ct) =>
                    {
                        var tracked = (await store.FindVariantAsync(existingId, ct))!;
                        tracked.Update(merged, userId, now, requestId);
                        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantUpdated, AuditActions.VariantEntity, existingId, [CatalogAudit.Field("import")]);
                    });
                }

                if (linkChange)
                {
                    actions.Add(LinkAction(existingId, supplier!.Id, supplierReference, primary));
                }

                continue;
            }

            if (takenBarcodes.Contains(variantDetails.Barcode ?? string.Empty))
            {
                plans.Add(new(lineNo, values, ImportOutcomes.Error, [new("barcode", CatalogFieldErrors.Duplicate)], skuKey));
                continue;
            }

            plans.Add(new(lineNo, generatedSku ? new Dictionary<string, string>(values) { ["sku"] = "(généré à l'application)" } : values, ImportOutcomes.Create, [], skuKey));
            var supplierId = supplier?.Id;
            var details = variantDetails;
            var generate = generatedSku;
            var familyForSku = familyCode;
            var policyValue = variantPolicy!.Value;
            var priceShop = shop is not null && (purchase is not null || sale is not null) ? shop : null;
            costs |= priceShop is not null && purchase is not null;
            manual |= priceShop is not null && sale is not null;
            actions.Add(async (userId, now, requestId, ct) =>
            {
                Guid modelId;
                if (models.TryGetValue(modelKey, out var model))
                {
                    modelId = model.Id;
                }
                else if (newModels.TryGetValue(modelKey, out var created) && created.Id is { } createdId)
                {
                    modelId = createdId;
                }
                else
                {
                    var newModel = ProductModel.Create(Guid.CreateVersion7(now), modelDetails, Guid.CreateVersion7(now), userId, now);
                    store.Add(newModel);
                    newModels[modelKey] = (modelDetails, newModel.Id);
                    modelId = newModel.Id;
                    CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.ModelCreated, AuditActions.ModelEntity, newModel.Id, [CatalogAudit.Field("import")]);
                }

                if (generate)
                {
                    // SKU définitif généré par le serveur, distinct des SKU existants (contrainte d'unicité en dernier recours).
                    var sku = SkuGenerator.Propose(familyForSku);
                    for (var attempt = 0; attempt < 5 && (await store.TakenSkusAsync([CatalogKeys.Normalize(sku)], null, ct)).Count > 0; attempt++)
                    {
                        sku = SkuGenerator.Propose(familyForSku);
                    }

                    details = details with { Sku = sku };
                }

                var variant = ProductVariant.Create(Guid.CreateVersion7(now), modelId, details, policyValue, null, userId, now);
                store.Add(variant);
                CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantCreated, AuditActions.VariantEntity, variant.Id,
                    [CatalogAudit.Field("stockPolicy", null, variant.StockPolicyCode), CatalogAudit.Field("import")]);
                if (supplierId is { } sid)
                {
                    // Variante nouvelle : le rattachement est ajouté après l'insertion de la variante (même transaction).
                    var save = await unitOfWork.SaveAsync(ct);
                    if (!save.Saved)
                    {
                        throw new InvalidOperationException("Import : enregistrement intermédiaire refusé.");
                    }

                    await LinkAction(variant.Id, sid, supplierReference, primary)(userId, now, requestId, ct);
                }

                if (priceShop is not null)
                {
                    // Tarif du magasin : la variante doit exister avant son prix (même transaction).
                    var saved = await unitOfWork.SaveAsync(ct);
                    if (!saved.Saved)
                    {
                        throw new InvalidOperationException("Import : enregistrement intermédiaire refusé.");
                    }

                    await AssistedPriceAction(variant.Id, priceShop, purchase, sale)(userId, now, requestId, ct);
                }
            });
        }

        return new(plans, actions, costs, manual);
    }

    /// <summary>
    /// Rattachement importé : référence et rôle principal seulement (aucun prix fournisseur par import de catalogue).
    /// Rôle non précisé par le fichier : celui du lien existant, sinon principal si la variante n'en a aucun.
    /// </summary>
    private ImportAction LinkAction(Guid variantId, Guid supplierId, string? reference, bool? requestedPrimary) => async (userId, now, requestId, ct) =>
    {
        var links = await store.LinksForVariantAsync(variantId, ct);
        var link = links.FirstOrDefault(l => l.SupplierId == supplierId);
        var primary = requestedPrimary ?? (link is { IsRemoved: false } ? link.IsPrimary : !links.Any(l => l.IsPrimary && !l.IsRemoved));
        var details = new VariantSupplierDetails(reference ?? link?.SupplierReference, link?.SupplierPriceHt, link?.SupplierCurrency, link?.LeadTimeDays, primary);
        if (link is { IsRemoved: false } && link.Snapshot() == details)
        {
            return;
        }

        if (primary)
        {
            foreach (var other in links.Where(l => l.SupplierId != supplierId && l.IsPrimary))
            {
                other.ClearPrimary(userId, now, requestId);
            }

            var cleared = await unitOfWork.SaveAsync(ct);
            if (!cleared.Saved)
            {
                throw new InvalidOperationException("Import : changement de fournisseur principal refusé.");
            }
        }

        if (link is null)
        {
            store.Add(VariantSupplier.Create(variantId, supplierId, details, userId, now, requestId));
        }
        else
        {
            link.Apply(details, userId, now, requestId);
        }

        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, link is null ? AuditActions.SupplierLinkAdded : AuditActions.SupplierLinkUpdated,
            AuditActions.VariantEntity, variantId, [CatalogAudit.Field("supplierId", null, supplierId.ToString()), CatalogAudit.Field("isPrimary", null, primary ? "true" : "false")]);
    };

    // ---------- outils ----------

    private async Task<ImportPreview> PreviewAsync(
        ImportBatch batch, CsvTable table, CancellationToken cancellationToken, IReadOnlyList<MappingSuggestion>? merged = null, ImportAnalysis? analysis = null)
    {
        var profile = batch.SupplierId is { } supplierId ? await profiles.FindAsync(supplierId, batch.Kind, cancellationToken) : null;
        var suggestions = merged ?? Propose(batch.Kind, table, batch.SupplierId is not null, profile).Suggestions;
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson, Json) ?? [];
        var (detected, ambiguous) = DetectDecimal(table, mapping);
        return new ImportPreview(
            batch.Id, table.Headers, [.. table.Rows.Take(10).Select(r => (IReadOnlyList<string>)[.. r.Cells])], mapping, batch.RowCount,
            table.Delimiter == '\t' ? "tab" : table.Delimiter.ToString(), suggestions,
            mapping.GetValueOrDefault(ImportOptions.DecimalSeparator) ?? detected?.ToString(), ambiguous && !mapping.ContainsKey(ImportOptions.DecimalSeparator),
            suggestions.Any(s => s.Confidence == MatchConfidence.Profile), batch.SupplierId,
            analysis, await imports.AppliedFileExistsAsync(batch.FileSha256, batch.Kind, batch.StoreId, cancellationToken));
    }

    private static ImportSummary Summary(ImportBatch batch, IReadOnlyList<RowPlan> rows) =>
        new(batch.Id, batch.Status, rows.Count,
            rows.Count(r => r.Outcome == ImportOutcomes.Create), rows.Count(r => r.Outcome == ImportOutcomes.Update),
            rows.Count(r => r.Outcome == ImportOutcomes.Unchanged), rows.Count(r => r.Outcome == ImportOutcomes.Error));

    /// <summary>Correspondance proposée : en-tête normalisé égal à un alias du champ.</summary>
    public static Dictionary<string, string> Suggest(string kind, IReadOnlyList<string> headers)
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Fields[kind])
        {
            var header = headers.FirstOrDefault(h => field.Aliases.Contains(CsvTable.NormalizeHeader(h), StringComparer.Ordinal) && !mapping.ContainsValue(h));
            if (header is not null)
            {
                mapping[field.Key] = header;
            }
        }

        return mapping;
    }

    /// <summary>Correspondance valide : champs connus, en-têtes existants, chacun au plus une fois, champs obligatoires présents.</summary>
    public static Dictionary<string, string[]> CheckMapping(string kind, IReadOnlyDictionary<string, string> mapping, IReadOnlyList<string> headers, bool requireFields = true)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var fields = Fields[kind];
        foreach (var (option, value) in mapping.Where(m => ImportOptions.IsOption(m.Key)))
        {
            var valid = option switch
            {
                ImportOptions.DecimalSeparator => value is "," or ".",
                ImportOptions.FamilyCode => kind == ImportKinds.Catalog && Families.Find(value) is not null,
                ImportOptions.StockPolicy => kind == ImportKinds.Catalog && StockPolicies.TryParse(value) is not null,
                _ => false,
            };
            if (!valid)
            {
                errors[$"mapping.{option}"] = [CatalogFieldErrors.InvalidFormat];
            }
        }

        var options = mapping.Where(m => ImportOptions.IsOption(m.Key)).ToDictionary(StringComparer.Ordinal);
        mapping = mapping.Where(m => !ImportOptions.IsOption(m.Key)).ToDictionary(StringComparer.Ordinal);
        foreach (var (key, header) in mapping)
        {
            if (fields.All(f => f.Key != key))
            {
                errors[$"mapping.{key}"] = [CatalogFieldErrors.Unknown];
            }
            else if (!headers.Contains(header, StringComparer.Ordinal))
            {
                errors[$"mapping.{key}"] = [CatalogFieldErrors.Unknown];
            }
        }

        // Chaque colonne au plus une fois, sauf libellé de variante ET coloris (même colonne admise).
        var reused = mapping.Where(m => m.Key is not ("color" or "variantLabel")).Select(m => m.Value)
            .Concat(mapping.Where(m => m.Key is "color" or "variantLabel").Select(m => m.Value).Distinct(StringComparer.Ordinal)).ToList();
        if (reused.Distinct(StringComparer.Ordinal).Count() != reused.Count)
        {
            errors["mapping"] = [CatalogFieldErrors.Duplicate];
        }

        if (!requireFields)
        {
            return errors;
        }

        // Champ obligatoire absent du fichier mais valeur par défaut choisie pour tout le fichier : admis.
        foreach (var field in fields.Where(f => f.Required && !mapping.ContainsKey(f.Key)
            && !(f.Key == "familyCode" && options.ContainsKey(ImportOptions.FamilyCode)) && !(f.Key == "stockPolicy" && options.ContainsKey(ImportOptions.StockPolicy))))
        {
            errors[$"mapping.{field.Key}"] = [CatalogFieldErrors.Required];
        }

        if (kind == ImportKinds.Prices && !mapping.ContainsKey("sku") && !mapping.ContainsKey("supplierReference"))
        {
            errors["mapping.sku"] = [CatalogFieldErrors.Required];
        }

        return errors;
    }

    private static Dictionary<string, string> Values(CsvTable table, string[] cells, IReadOnlyDictionary<string, string> mapping)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, header) in mapping.Where(m => !ImportOptions.IsOption(m.Key)))
        {
            var index = table.Headers.ToList().IndexOf(header);
            var value = index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
            if (value.Length > 0)
            {
                values[key] = value;
            }
        }

        // Valeurs par défaut CHOISIES par l'utilisateur pour tout le fichier (cellule vide ou colonne absente).
        foreach (var (option, field) in new[] { (ImportOptions.FamilyCode, "familyCode"), (ImportOptions.StockPolicy, "stockPolicy") })
        {
            if (!values.ContainsKey(field) && mapping.GetValueOrDefault(option) is { Length: > 0 } fallback)
            {
                values[field] = fallback;
            }
        }

        return values;
    }

    private static List<ImportError> FormulaErrors(Dictionary<string, string> values) =>
        [.. values.Where(v => CsvTable.LooksLikeFormula(v.Value) && v.Key is not ("phone")).Select(v => new ImportError(v.Key, "formula_not_allowed"))];

    internal static string? FamilyCode(string? value) =>
        value is null ? null : Families.All.FirstOrDefault(f => f.Code == value.Trim() || CsvTable.NormalizeHeader(f.Label) == CsvTable.NormalizeHeader(value))?.Code;

    internal static string? Policy(string? value) => CsvTable.NormalizeHeader(value ?? string.Empty) switch
    {
        "stocked" or "stocke" or "stock" => StockPolicies.Stocked,
        "orderonly" or "surcommande" or "commande" or "commandealademande" => StockPolicies.OrderOnly,
        "service" or "prestation" => StockPolicies.Service,
        _ => null,
    };

    internal static bool? Boolean(string? value) => CsvTable.NormalizeHeader(value ?? string.Empty) switch
    {
        "oui" or "true" or "1" or "yes" or "o" => true,
        "non" or "false" or "0" or "no" or "n" => false,
        _ => null,
    };

    private static bool Same(string left, string? right) => right is not null && CatalogKeys.Normalize(left) == CatalogKeys.Normalize(right);

    /// <summary>Clé d'un modèle pour l'import (famille, marque, nom normalisés, gamme) ; même calcul côté MMV.Infrastructure.</summary>
    public static string ModelKey(ProductModelDetails details) =>
        $"{details.FamilyCode}|{CatalogKeys.Normalize(details.Brand ?? string.Empty)}|{CatalogKeys.Normalize(details.Name)}|{details.LensRangeId}";

    private static string Rename(string field) => field switch
    {
        "name" => "modelName",
        "description" => "modelDescription",
        "label" => "variantLabel",
        _ => field,
    };
}
