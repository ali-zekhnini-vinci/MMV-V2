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

/// <summary>Aperçu après dépôt : en-têtes, premières lignes, correspondance proposée.</summary>
public sealed record ImportPreview(Guid BatchId, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> SampleRows, IReadOnlyDictionary<string, string> SuggestedMapping, int RowCount);

/// <summary>Synthèse d'un contrôle à blanc ou d'une application.</summary>
public sealed record ImportSummary(Guid BatchId, string Status, int Total, int Create, int Update, int Unchanged, int Error);

/// <summary>
/// Import CSV contrôlé (05-D, D-05-18) : dépôt (zone de préparation, empreinte SHA-256, aperçu, correspondance proposée) →
/// contrôle à blanc (rapport par ligne, AUCUNE écriture au catalogue) → application explicite, tout ou rien, rejouée
/// contre l'état COURANT dans la transaction, idempotente (même requête : état courant ; même fichier déjà appliqué :
/// 409). Aucune suppression, aucun import destructif : les lignes absentes du fichier ne changent rien. Coûts importés
/// seulement avec <c>costs.write</c>, prix manuels avec <c>pricing.manual</c> ; aucune conversion de devise ; montants
/// lus en chaînes décimales ; cellules de type formule refusées.
/// </summary>
public sealed class ImportService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IPricingStore pricing, IImportStore imports, TimeProvider clock, IAuditRecorder audit)
{
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
            new("sku", true, "sku", "referenceinterne", "refinterne"),
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
            new("sku", true, "sku", "referenceinterne", "refinterne"),
            new("purchasePriceHt", false, "prixachatht", "prixachat", "purchasepriceht"),
            new("coefficient", false, "coefficient", "coef"),
            new("salePriceHt", false, "prixventeht", "prixvente", "salepriceht"),
            new("currency", false, "devise", "currency"),
        ],
    };

    // ---------- dépôt ----------

    public async Task<Outcome<ImportPreview>> UploadAsync(
        string? kind, Guid? storeId, string? fileName, string? content, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (await imports.FindBatchByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.UploadedByUserId == userId
                ? Outcome.Ok(Preview(existing, CsvTable.Parse(existing.Content, ImportBatch.MaxRows).Table!), replayed: true)
                : Outcome.Conflict<ImportPreview>("request_id_reused");
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

        var (table, parseError) = CsvTable.Parse(content!, ImportBatch.MaxRows);
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
        var mapping = Suggest(kind!, table.Headers);
        var batch = ImportBatch.Upload(Guid.CreateVersion7(now), kind!, storeId, name!, sha, content!, table.Rows.Count, JsonSerializer.Serialize(mapping, Json), userId, now, requestId);
        imports.Add(batch);
        CatalogAudit.Record(audit, userId, now, AuditActions.ImportsModule, AuditActions.ImportUploaded, AuditActions.ImportEntity, batch.Id,
            [CatalogAudit.Field("kind", null, kind), CatalogAudit.Field("lineCount", null, table.Rows.Count.ToString(CultureInfo.InvariantCulture))]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_batches_creation_request_id" }
                && await imports.FindBatchByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.UploadedByUserId == userId
                ? Outcome.Ok(Preview(replay, table), replayed: true)
                : Outcome.Conflict<ImportPreview>();
        }

        return Outcome.Created(Preview(batch, table));
    }

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

        var table = CsvTable.Parse(batch.Content, ImportBatch.MaxRows).Table!;
        var effective = mapping ?? JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson, Json) ?? [];
        if (CheckMapping(batch.Kind, effective, table.Headers) is { Count: > 0 } mappingErrors)
        {
            return Outcome.Invalid<ImportSummary>(mappingErrors);
        }

        var plan = await PlanAsync(batch, table, effective, cancellationToken);
        var now = CatalogClock.Now(clock);
        await imports.ReplaceRowsAsync(batch.Id, [.. plan.Rows.Select(r => ImportRow.Create(
            batch.Id, r.LineNo, JsonSerializer.Serialize(r.Values, Json), r.Outcome, JsonSerializer.Serialize(r.Errors, Json), r.TargetKey))], cancellationToken);
        var errorCount = plan.Rows.Count(r => r.Outcome == ImportOutcomes.Error);
        batch.MarkChecked(JsonSerializer.Serialize(effective, Json), errorCount, plan.ContainsCosts, plan.ContainsManualPrices, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.ImportsModule, AuditActions.ImportChecked, AuditActions.ImportEntity, batch.Id,
            [CatalogAudit.Field("lineCount", null, plan.Rows.Count.ToString(CultureInfo.InvariantCulture)), CatalogAudit.Field("status", null, ImportStatuses.Checked)]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Saved ? Outcome.Ok(Summary(batch, plan.Rows)) : Outcome.Conflict<ImportSummary>();
    }

    // ---------- application ----------

    /// <summary>
    /// Application explicite, TOUT OU RIEN : le plan est recalculé contre l'état courant dans la transaction ; une seule
    /// ligne en erreur → 422 <c>import_has_errors</c>, rien n'est écrit. Coûts : <c>costs.write</c> ; prix manuels :
    /// <c>pricing.manual</c> (403 sinon). Même fichier déjà appliqué → 409 <c>import_already_applied</c>.
    /// </summary>
    public async Task<Outcome<ImportSummary>> ApplyAsync(Guid batchId, uint version, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
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
        return Outcome.Ok(summary);
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
        return batch.Kind switch
        {
            ImportKinds.Suppliers => await PlanSuppliersAsync(rows, cancellationToken),
            ImportKinds.Prices => await PlanPricesAsync(batch.StoreId!.Value, rows, cancellationToken),
            _ => await PlanCatalogAsync(rows, cancellationToken),
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

    private async Task<Plan> PlanPricesAsync(Guid storeId, List<(int LineNo, Dictionary<string, string> Values)> rows, CancellationToken cancellationToken)
    {
        var shop = (await store.GetStoreAsync(storeId, cancellationToken))!;
        var variants = await imports.VariantsBySkuAsync([.. rows.Select(r => CatalogKeys.Normalize(r.Values.GetValueOrDefault("sku") ?? string.Empty))], cancellationToken);
        var prices = (await pricing.PricesAsync(storeId, [.. variants.Values.Select(v => v.Id)], cancellationToken)).ToDictionary(p => p.VariantId);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var plans = new List<RowPlan>();
        var actions = new List<ImportAction>();
        bool costs = false, manual = false;
        foreach (var (lineNo, values) in rows)
        {
            var errors = FormulaErrors(values);
            var sku = values.GetValueOrDefault("sku");
            var key = sku is null ? null : CatalogKeys.Normalize(sku);
            ProductVariant? variant = null;
            if (key is null)
            {
                errors.Add(new("sku", CatalogFieldErrors.Required));
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
            var purchase = values.TryGetValue("purchasePriceHt", out var purchaseText) ? PricingService.Purchase(purchaseText, fieldErrors, required: false) : null;
            var coefficient = values.TryGetValue("coefficient", out var coefficientText) ? PricingService.Coefficient(coefficientText, fieldErrors) : null;
            var sale = values.TryGetValue("salePriceHt", out var saleText) ? PricingService.Sale(saleText, "salePriceHt", fieldErrors) : null;
            errors.AddRange(fieldErrors.Select(e => new ImportError(e.Key, e.Value[0])));
            if (purchase is null && coefficient is null && sale is null && fieldErrors.Count == 0)
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

    private async Task<Plan> PlanCatalogAsync(List<(int LineNo, Dictionary<string, string> Values)> rows, CancellationToken cancellationToken)
    {
        var variants = await imports.VariantsBySkuAsync([.. rows.Select(r => CatalogKeys.Normalize(r.Values.GetValueOrDefault("sku") ?? string.Empty))], cancellationToken);
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

            var modelDetails = CatalogInputValidator.Model(
                new ModelInput(familyCode ?? values.GetValueOrDefault("familyCode"), values.GetValueOrDefault("brand"), values.GetValueOrDefault("modelName"),
                    values.GetValueOrDefault("modelDescription"), rangeId, null),
                fieldErrors);
            var policy = Policy(values.GetValueOrDefault("stockPolicy"));
            var (variantDetails, variantPolicy) = family is null
                ? (null, null)
                : CatalogInputValidator.Variant(
                    new VariantInput(values.GetValueOrDefault("sku"), values.GetValueOrDefault("manufacturerReference"), values.GetValueOrDefault("barcode"),
                        values.GetValueOrDefault("variantLabel"), null, indexId, treatmentIds, policy ?? values.GetValueOrDefault("stockPolicy")),
                    family, requirePolicy: true, fieldErrors);
            errors.AddRange(fieldErrors.Where(e => !(family is { IsOphthalmicLens: true } && e.Key is "lensRangeId" or "lensIndexId"))
                .Select(e => new ImportError(Rename(e.Key), e.Value[0])));

            var skuKey = variantDetails is null ? null : CatalogKeys.Normalize(variantDetails.Sku);
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
            else if (supplierReference is not null || primary == true)
            {
                errors.Add(new("supplierCode", CatalogFieldErrors.Required));
            }

            if (errors.Count > 0 || modelDetails is null || variantDetails is null)
            {
                plans.Add(new(lineNo, values, ImportOutcomes.Error, errors, skuKey));
                continue;
            }

            var modelKey = ModelKey(modelDetails);
            if (variants.TryGetValue(skuKey!, out var existing))
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

                // Attributs non importés : ceux de la fiche sont conservés.
                var merged = variantDetails with { Attributes = existing.Attributes };
                var same = existing.Snapshot().SameAs(merged);
                var linkChange = supplier is not null;
                plans.Add(new(lineNo, values, same && !linkChange ? ImportOutcomes.Unchanged : ImportOutcomes.Update, [], skuKey));
                var existingId = existing.Id;
                if (!same)
                {
                    actions.Add(async (userId, now, requestId, ct) =>
                    {
                        var tracked = (await store.FindVariantAsync(existingId, ct))!;
                        tracked.Update(merged, userId, now, requestId);
                        CatalogAudit.Record(audit, userId, now, AuditActions.CatalogModule, AuditActions.VariantUpdated, AuditActions.VariantEntity, existingId, [CatalogAudit.Field("import")]);
                    });
                }

                if (supplier is not null)
                {
                    actions.Add(LinkAction(existingId, supplier.Id, supplierReference, primary ?? false));
                }

                continue;
            }

            if (takenBarcodes.Contains(variantDetails.Barcode ?? string.Empty))
            {
                plans.Add(new(lineNo, values, ImportOutcomes.Error, [new("barcode", CatalogFieldErrors.Duplicate)], skuKey));
                continue;
            }

            plans.Add(new(lineNo, values, ImportOutcomes.Create, [], skuKey));
            var supplierId = supplier?.Id;
            var details = variantDetails;
            var policyValue = variantPolicy!.Value;
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

                    await LinkAction(variant.Id, sid, supplierReference, primary ?? false)(userId, now, requestId, ct);
                }
            });
        }

        return new(plans, actions, false, false);
    }

    /// <summary>Rattachement importé : référence et rôle principal seulement (aucun prix fournisseur par import de catalogue).</summary>
    private ImportAction LinkAction(Guid variantId, Guid supplierId, string? reference, bool primary) => async (userId, now, requestId, ct) =>
    {
        var links = await store.LinksForVariantAsync(variantId, ct);
        var link = links.FirstOrDefault(l => l.SupplierId == supplierId);
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

    private static ImportPreview Preview(ImportBatch batch, CsvTable table) =>
        new(batch.Id, table.Headers, [.. table.Rows.Take(10).Select(r => (IReadOnlyList<string>)[.. r.Cells])],
            JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson, Json) ?? [], batch.RowCount);

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
    public static Dictionary<string, string[]> CheckMapping(string kind, IReadOnlyDictionary<string, string> mapping, IReadOnlyList<string> headers)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var fields = Fields[kind];
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

        if (mapping.Values.Distinct(StringComparer.Ordinal).Count() != mapping.Count)
        {
            errors["mapping"] = [CatalogFieldErrors.Duplicate];
        }

        foreach (var field in fields.Where(f => f.Required && !mapping.ContainsKey(f.Key)))
        {
            errors[$"mapping.{field.Key}"] = [CatalogFieldErrors.Required];
        }

        return errors;
    }

    private static Dictionary<string, string> Values(CsvTable table, string[] cells, IReadOnlyDictionary<string, string> mapping)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, header) in mapping)
        {
            var index = table.Headers.ToList().IndexOf(header);
            var value = index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
            if (value.Length > 0)
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static List<ImportError> FormulaErrors(Dictionary<string, string> values) =>
        [.. values.Where(v => CsvTable.LooksLikeFormula(v.Value) && v.Key is not ("phone")).Select(v => new ImportError(v.Key, "formula_not_allowed"))];

    private static string? FamilyCode(string? value) =>
        value is null ? null : Families.All.FirstOrDefault(f => f.Code == value.Trim() || CsvTable.NormalizeHeader(f.Label) == CsvTable.NormalizeHeader(value))?.Code;

    private static string? Policy(string? value) => CsvTable.NormalizeHeader(value ?? string.Empty) switch
    {
        "stocked" or "stocke" or "stock" => StockPolicies.Stocked,
        "orderonly" or "surcommande" or "commande" or "commandealademande" => StockPolicies.OrderOnly,
        "service" or "prestation" => StockPolicies.Service,
        _ => null,
    };

    private static bool? Boolean(string? value) => CsvTable.NormalizeHeader(value ?? string.Empty) switch
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
