using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;

namespace MMV.Application.Catalog;

/// <summary>Ligne d'une réception simple (quantité annoncée facultative : réception partielle si reçue &lt; annoncée).</summary>
public sealed record ReceiptLineInput(Guid? VariantId, int? AnnouncedQuantity, int? ReceivedQuantity, string? UnitCostHt);

/// <summary>Réception simple d'un fournisseur (aucune commande fournisseur, BL-05-01).</summary>
public sealed record ReceiptInput(
    Guid? LocationId, Guid? SupplierId, string? Currency, string? SupplierDocumentReference, string? Notes, Guid? CompletesReceiptId, IReadOnlyList<ReceiptLineInput>? Lines);

/// <summary>
/// Stock par emplacement (05-B) : sorties et entrées manuelles motivées, ajustements (quantité constatée + attendue),
/// réceptions simples et partielles, seuils d'alerte. Chaque écriture : transaction, verrous dans l'ORDRE UNIQUE
/// magasin (FOR SHARE) → fournisseur (FOR SHARE) → variantes (FOR SHARE, ordre des identifiants) → soldes (FOR UPDATE,
/// ordre (variante, emplacement)) ; contrôle du stock négatif et du réservé SOUS VERROU, mouvement et solde écrits
/// ensemble, audit dans la même transaction. Contrainte SQL <c>on_hand &gt;= 0</c> en garantie. Aucun stock pour
/// <c>order_only</c> ni <c>service</c>.
/// </summary>
public sealed class StockService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IStockStore stock, TimeProvider clock, IAuditRecorder audit)
{
    public const int MaxQuantity = 1_000_000;

    /// <summary>Entrée ou sortie manuelle motivée (<c>manual_in</c> / <c>manual_out</c>).</summary>
    public async Task<Outcome<Guid>> RecordMovementAsync(
        Guid variantId, Guid locationId, string? movementType, int? quantity, string? reasonCode, string? comment, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (movementType is null || !MovementTypes.Manual.Contains(movementType))
        {
            errors["type"] = [movementType is null ? CatalogFieldErrors.Required : CatalogFieldErrors.NotAllowed];
        }

        if (quantity is not (> 0 and <= MaxQuantity))
        {
            errors["quantity"] = [quantity is null ? CatalogFieldErrors.Required : CatalogFieldErrors.OutOfRange];
        }

        var text = Reason(movementType ?? MovementTypes.ManualIn, reasonCode, comment, errors);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var delta = movementType == MovementTypes.ManualOut ? -quantity!.Value : quantity!.Value;
        if (await ReplayAsync(requestId, variantId, locationId, movementType!, delta, cancellationToken) is { } replay)
        {
            return replay;
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (location, refusal) = await LockTargetAsync(locationId, [variantId], cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        var balance = await stock.LockBalanceAsync(variantId, locationId, cancellationToken);
        switch (StockRules.CheckDelta(balance.OnHand, balance.Reserved, delta))
        {
            case StockCheck.Insufficient:
                return Outcome.Refuse<Guid>("stock_insufficient", new { onHand = balance.OnHand });
            case StockCheck.Reserved:
                return Outcome.Refuse<Guid>("stock_reserved", new { onHand = balance.OnHand, reserved = balance.Reserved });
        }

        var now = CatalogClock.Now(clock);
        var movement = StockMovement.Record(Guid.CreateVersion7(now), balance, movementType!, delta, reasonCode!, text, null, null, null, null, null, userId, now, requestId);
        stock.Add(movement);
        RecordMovementAudit(AuditActions.StockMovementRecorded, movement, location!.StoreId, userId, now);
        return await CommitMovementAsync(transaction, movement.Id, requestId, variantId, locationId, movementType!, delta, cancellationToken);
    }

    /// <summary>
    /// Ajustement (D-05-09 option A, QR-03 appliquée) : quantité CONSTATÉE + quantité ATTENDUE (celle affichée à
    /// l'utilisateur). Stock modifié entre-temps (autre poste) → 409 <c>stock_changed</c>, rien n'est écrit. Écart nul :
    /// aucun mouvement.
    /// </summary>
    public async Task<Outcome<Guid?>> AdjustAsync(
        Guid variantId, Guid locationId, int? expectedOnHand, int? countedQuantity, string? reasonCode, string? comment, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (expectedOnHand is not (>= 0 and <= MaxQuantity))
        {
            errors["expectedOnHand"] = [expectedOnHand is null ? CatalogFieldErrors.Required : CatalogFieldErrors.OutOfRange];
        }

        if (countedQuantity is not (>= 0 and <= MaxQuantity))
        {
            errors["countedQuantity"] = [countedQuantity is null ? CatalogFieldErrors.Required : CatalogFieldErrors.OutOfRange];
        }

        var text = Reason(MovementTypes.Adjustment, reasonCode, comment, errors);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid?>(errors);
        }

        if ((await stock.MovementsByRequestAsync(requestId, cancellationToken)) is { Count: > 0 } previous)
        {
            return previous is [{ MovementType: MovementTypes.Adjustment } single] && single.VariantId == variantId && single.LocationId == locationId
                && single.PerformedByUserId == userId
                ? Outcome.Ok<Guid?>(single.Id, replayed: true)
                : Outcome.Conflict<Guid?>("request_id_reused");
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (location, refusal) = await LockTargetAsync(locationId, [variantId], cancellationToken);
        if (refusal is not null)
        {
            return refusal.As<Guid?>();
        }

        var balance = await stock.LockBalanceAsync(variantId, locationId, cancellationToken);
        if (balance.OnHand != expectedOnHand)
        {
            return Outcome.Conflict<Guid?>("stock_changed", new { currentOnHand = balance.OnHand });
        }

        var delta = countedQuantity!.Value - balance.OnHand;
        if (delta == 0)
        {
            return Outcome.Ok<Guid?>(null);
        }

        if (StockRules.CheckDelta(balance.OnHand, balance.Reserved, delta) == StockCheck.Reserved)
        {
            return Outcome.Refuse<Guid?>("stock_reserved", new { onHand = balance.OnHand, reserved = balance.Reserved });
        }

        var now = CatalogClock.Now(clock);
        var movement = StockMovement.Record(Guid.CreateVersion7(now), balance, MovementTypes.Adjustment, delta, reasonCode!, text, null, null, null, null, null, userId, now, requestId);
        stock.Add(movement);
        RecordMovementAudit(AuditActions.StockAdjustmentRecorded, movement, location!.StoreId, userId, now);
        var outcome = await CommitMovementAsync(transaction, movement.Id, requestId, variantId, locationId, MovementTypes.Adjustment, delta, cancellationToken);
        return outcome.IsSuccess ? Outcome.Ok<Guid?>(outcome.Value, outcome.Replayed) : outcome.As<Guid?>();
    }

    /// <summary>
    /// Réception simple (Q12) d'un fournisseur dans un emplacement : toutes les lignes ou aucune. Coût unitaire HT
    /// facultatif, PROTÉGÉ (<c>costs.write</c>), dans la devise du magasin (aucune conversion), conservé sans modifier le
    /// prix d'achat de référence ni le prix de vente. Réception partielle : quantité annoncée &gt; reçue ; le reliquat se
    /// reçoit par une réception de complément (<see cref="ReceiptInput.CompletesReceiptId"/>).
    /// </summary>
    public async Task<Outcome<Guid>> ReceiveAsync(ReceiptInput input, Guid requestId, CatalogActor actor, CancellationToken cancellationToken)
    {
        var lines = input.Lines ?? [];
        if (lines.Any(l => l.UnitCostHt is not null) && !actor.Has(Permissions.CostsWrite))
        {
            return Outcome.Forbidden<Guid>("costs_forbidden");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (input.LocationId is null)
        {
            errors["locationId"] = [CatalogFieldErrors.Required];
        }

        if (input.SupplierId is null)
        {
            errors["supplierId"] = [CatalogFieldErrors.Required];
        }

        var document = CatalogText.Clean(input.SupplierDocumentReference, "supplierDocumentReference", Receipt.DocumentReferenceMaxLength, required: false, errors);
        var notes = CatalogText.Clean(input.Notes, "notes", Receipt.NotesMaxLength, required: false, errors, multiline: true);
        if (lines.Count is 0 or > Receipt.MaxLines)
        {
            errors["lines"] = [lines.Count == 0 ? CatalogFieldErrors.Required : CatalogFieldErrors.OutOfRange];
        }

        var parsed = new List<(Guid VariantId, int? Announced, int Received, decimal? Cost)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.VariantId is not { } variantId)
            {
                errors[$"lines[{i}].variantId"] = [CatalogFieldErrors.Required];
                continue;
            }

            var received = line.ReceivedQuantity ?? 0;
            if (received is < 0 or > MaxQuantity)
            {
                errors[$"lines[{i}].receivedQuantity"] = [CatalogFieldErrors.OutOfRange];
            }

            if (line.AnnouncedQuantity is < 0 or > MaxQuantity)
            {
                errors[$"lines[{i}].announcedQuantity"] = [CatalogFieldErrors.OutOfRange];
            }

            if (received == 0 && line.AnnouncedQuantity is not > 0)
            {
                errors[$"lines[{i}].receivedQuantity"] = [CatalogFieldErrors.Required];
            }

            decimal? cost = null;
            if (line.UnitCostHt is not null)
            {
                cost = MoneyText.TryParse(line.UnitCostHt, MoneyText.PurchaseScale);
                if (cost is null or > MoneyText.MaxPurchasePrice)
                {
                    errors[$"lines[{i}].unitCostHt"] = [CatalogFieldErrors.InvalidFormat];
                }
            }

            if (parsed.Any(p => p.VariantId == variantId))
            {
                errors[$"lines[{i}].variantId"] = [CatalogFieldErrors.Duplicate];
            }

            parsed.Add((variantId, line.AnnouncedQuantity, received, cost));
        }

        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        if (await stock.FindReceiptByRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.ReceivedByUserId == actor.UserId && existing.LocationId == input.LocationId && existing.SupplierId == input.SupplierId
                ? Outcome.Ok<Guid>(existing.Id, replayed: true)
                : Outcome.Conflict<Guid>("request_id_reused");
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var location = await store.GetLocationAsync(input.LocationId!.Value, cancellationToken);
        if (location is null)
        {
            return Outcome.Missing<Guid>("location_not_found");
        }

        if (!await store.LockStoreAsync(location.StoreId, cancellationToken))
        {
            return Outcome.Missing<Guid>("store_not_found");
        }

        var shop = (await store.GetStoreAsync(location.StoreId, cancellationToken))!;
        if (shop.IsArchived || location.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        if (input.Currency is not null && input.Currency != shop.CurrencyCode)
        {
            return Outcome.Refuse<Guid>("currency_mismatch");
        }

        if (!await store.LockSupplierAsync(input.SupplierId!.Value, RowLock.Share, cancellationToken))
        {
            return Outcome.Missing<Guid>("supplier_not_found");
        }

        if ((await store.FindSupplierAsync(input.SupplierId.Value, cancellationToken))!.IsArchived)
        {
            return Outcome.Invalid<Guid>("supplierId", CatalogFieldErrors.Archived);
        }

        if (input.CompletesReceiptId is { } rootId)
        {
            var root = await stock.FindReceiptAsync(rootId, cancellationToken);
            if (root is null || root.SupplierId != input.SupplierId || root.StoreId != shop.Id)
            {
                return Outcome.Invalid<Guid>("completesReceiptId", CatalogFieldErrors.Unknown);
            }
        }

        var variantIds = parsed.Select(p => p.VariantId).ToList();
        if (await VariantsRefusalAsync(variantIds, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var now = CatalogClock.Now(clock);
        var receipt = Receipt.Create(Guid.CreateVersion7(now), shop.Id, location.Id, input.SupplierId.Value, shop.CurrencyCode, document, notes, input.CompletesReceiptId, actor.UserId, now, requestId);
        // Soldes verrouillés en une fois, dans l'ordre (variante, emplacement) de PostgreSQL : le même que l'inventaire.
        var balances = (await stock.LockBalancesAsync([.. parsed.Where(p => p.Received > 0).Select(p => (p.VariantId, location.Id))], cancellationToken))
            .ToDictionary(b => b.VariantId);
        var lineNo = 0;
        foreach (var line in parsed)
        {
            Guid? movementId = null;
            if (line.Received > 0)
            {
                var balance = balances[line.VariantId];
                var movement = StockMovement.Record(
                    Guid.CreateVersion7(now), balance, MovementTypes.Receipt, line.Received, StockReasons.SupplierReceipt, null, input.SupplierId, line.Cost,
                    shop.CurrencyCode, StockSources.Receipt, receipt.Id, actor.UserId, now, requestId);
                stock.Add(movement);
                movementId = movement.Id;
            }

            receipt.AddLine(ReceiptLine.Create(receipt.Id, ++lineNo, line.VariantId, line.Announced, line.Received, line.Cost, movementId));
        }

        stock.Add(receipt);
        CatalogAudit.Record(audit, actor.UserId, now, AuditActions.StockModule, AuditActions.ReceiptRecorded, AuditActions.ReceiptEntity, receipt.Id,
        [
            CatalogAudit.Field("supplierId", null, input.SupplierId.Value.ToString()),
            CatalogAudit.Field("locationId", null, location.Id.ToString()),
            CatalogAudit.Field("lineCount", null, parsed.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            .. parsed.Any(p => p.Cost is not null) ? [CatalogAudit.Field("unitCostHt")] : Array.Empty<AuditChange>(),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            if (commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_receipts_request_id" or "ux_movements_request_variant_location" }
                && await stock.FindReceiptByRequestAsync(requestId, cancellationToken) is { } replay && replay.ReceivedByUserId == actor.UserId)
            {
                return Outcome.Ok<Guid>(replay.Id, replayed: true);
            }

            return CatalogService.UniqueRefusal<Guid>(commit);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Created<Guid>(receipt.Id);
    }

    /// <summary>Seuil d'alerte par (variante, magasin), facultatif (D-05-19) ; variantes <c>stocked</c> seulement.</summary>
    public async Task<Outcome<Guid>> SetThresholdAsync(
        Guid variantId, Guid storeId, int? threshold, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (threshold is < 0 or > VariantStoreSetting.ThresholdMax)
        {
            return Outcome.Invalid<Guid>("lowStockThreshold", CatalogFieldErrors.OutOfRange);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await store.LockStoreAsync(storeId, cancellationToken))
        {
            return Outcome.Missing<Guid>("store_not_found");
        }

        if (!await store.LockVariantAsync(variantId, RowLock.Share, cancellationToken))
        {
            return Outcome.Missing<Guid>();
        }

        var variant = (await store.FindVariantAsync(variantId, cancellationToken))!;
        var (setting, created) = await stock.LockSettingAsync(variantId, storeId, userId, cancellationToken);
        if (!created && setting.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(variantId, replayed: true);
        }

        if (created ? version is not (null or 0) : version != setting.Version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (variant.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        if (variant.StockPolicy != StockPolicy.Stocked)
        {
            return Outcome.Refuse<Guid>("not_stocked");
        }

        if (!created && setting.LowStockThreshold == threshold)
        {
            return Outcome.Ok<Guid>(variantId);
        }

        var now = CatalogClock.Now(clock);
        var previous = setting.LowStockThreshold;
        setting.SetThreshold(threshold, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.StockModule, AuditActions.StockThresholdChanged, AuditActions.VariantEntity, variantId,
        [
            CatalogAudit.Field("storeId", null, storeId.ToString()),
            CatalogAudit.Field("lowStockThreshold", previous?.ToString(System.Globalization.CultureInfo.InvariantCulture), threshold?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return CatalogService.Commit(commit, variantId);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Ok<Guid>(variantId);
    }

    // ---------- outils ----------

    /// <summary>Motif codé du type, commentaire borné, obligatoire pour <c>other</c>.</summary>
    internal static string? Reason(string movementType, string? reasonCode, string? comment, IDictionary<string, string[]> errors)
    {
        if (!StockReasons.IsAllowed(movementType, reasonCode))
        {
            errors["reasonCode"] = [reasonCode is null ? CatalogFieldErrors.Required : CatalogFieldErrors.NotAllowed];
        }

        var text = CatalogText.Clean(comment, "comment", StockReasons.CommentMaxLength, required: reasonCode == StockReasons.Other, errors, multiline: true);
        return text;
    }

    /// <summary>Emplacement et magasin actifs (magasin FOR SHARE), puis variantes FOR SHARE (actives, <c>stocked</c>).</summary>
    private async Task<(Location? Location, Outcome<Guid>? Refusal)> LockTargetAsync(Guid locationId, IReadOnlyList<Guid> variantIds, CancellationToken cancellationToken)
    {
        var location = await store.GetLocationAsync(locationId, cancellationToken);
        if (location is null)
        {
            return (null, Outcome.Missing<Guid>("location_not_found"));
        }

        if (!await store.LockStoreAsync(location.StoreId, cancellationToken))
        {
            return (null, Outcome.Missing<Guid>("store_not_found"));
        }

        var shop = (await store.GetStoreAsync(location.StoreId, cancellationToken))!;
        if (shop.IsArchived || location.IsArchived)
        {
            return (null, Outcome.Conflict<Guid>("archived"));
        }

        return (location, await VariantsRefusalAsync(variantIds, cancellationToken));
    }

    private async Task<Outcome<Guid>?> VariantsRefusalAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        await store.LockVariantsAsync(variantIds, RowLock.Share, cancellationToken);
        var variants = (await store.FindVariantsAsync(variantIds, cancellationToken)).ToDictionary(v => v.Id);
        foreach (var id in variantIds.Order())
        {
            if (!variants.TryGetValue(id, out var variant))
            {
                return Outcome.Missing<Guid>("variant_not_found");
            }

            if (variant.IsArchived)
            {
                return Outcome.Conflict<Guid>("archived");
            }

            if (variant.StockPolicy != StockPolicy.Stocked)
            {
                // Aucun stock inventé pour une variante commandée à la demande ou une prestation.
                return Outcome.Refuse<Guid>("not_stocked", new { variantId = id });
            }
        }

        return null;
    }

    private async Task<Outcome<Guid>?> ReplayAsync(Guid requestId, Guid variantId, Guid locationId, string movementType, int delta, CancellationToken cancellationToken)
    {
        var previous = await stock.MovementsByRequestAsync(requestId, cancellationToken);
        if (previous.Count == 0)
        {
            return null;
        }

        return previous is [var single] && single.VariantId == variantId && single.LocationId == locationId && single.MovementType == movementType && single.QuantityDelta == delta
            ? Outcome.Ok<Guid>(single.Id, replayed: true)
            : Outcome.Conflict<Guid>("request_id_reused");
    }

    private async Task<Outcome<Guid>> CommitMovementAsync(
        ITransactionScope transaction, Guid movementId, Guid requestId, Guid variantId, Guid locationId, string movementType, int delta, CancellationToken cancellationToken)
    {
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (commit.Saved)
        {
            await transaction.CommitAsync(cancellationToken);
            return Outcome.Created<Guid>(movementId);
        }

        // Réémission concurrente (même requête sur deux connexions) : la première l'a emporté, son résultat est relu.
        return commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_movements_request_variant_location" }
            ? await ReplayAsync(requestId, variantId, locationId, movementType, delta, cancellationToken) ?? Outcome.Conflict<Guid>()
            : CatalogService.UniqueRefusal<Guid>(commit);
    }

    private void RecordMovementAudit(string action, StockMovement movement, Guid storeId, Guid userId, DateTimeOffset now) =>
        CatalogAudit.Record(audit, userId, now, AuditActions.StockModule, action, AuditActions.StockMovementEntity, movement.Id,
        [
            CatalogAudit.Field("movementType", null, movement.MovementType),
            CatalogAudit.Field("quantityDelta", null, movement.QuantityDelta.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            CatalogAudit.Field("reasonCode", null, movement.ReasonCode),
            CatalogAudit.Field("locationId", null, movement.LocationId.ToString()),
            CatalogAudit.Field("storeId", null, storeId.ToString()),
            .. movement.Comment is null ? Array.Empty<AuditChange>() : [CatalogAudit.Field("comment")],
        ]);
}
