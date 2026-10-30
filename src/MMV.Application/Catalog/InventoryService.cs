using System.Globalization;
using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Catalog;
using MMV.Domain.Stock;

namespace MMV.Application.Catalog;

/// <summary>Ouverture d'une session : emplacements (défaut : tous les emplacements actifs du magasin), périmètre, aveugle.</summary>
public sealed record InventoryOpenInput(
    Guid? StoreId, IReadOnlyList<Guid>? LocationIds, string? ScopeType, string? ScopeFamilyCode, IReadOnlyList<Guid>? VariantIds, bool Blind);

/// <summary>Ligne refusée à la validation (aucune valeur de coût).</summary>
public sealed record InventoryLineIssue(Guid VariantId, Guid LocationId, string Issue, int? CurrentOnHand, int? ExpectedAtCount);

/// <summary>
/// Inventaires (05-C, Q10, D-05-10 — QR-04 appliquée) : sessions complètes ou partielles par magasin et emplacements,
/// comptage par ligne (plusieurs postes, version par ligne), comparaison avec la quantité théorique lue SOUS VERROU au
/// moment du comptage, écarts justifiés (motif obligatoire), validation atomique par une permission DISTINCTE du
/// comptage, abandon motivé. Pas de gel du stock : la validation applique l'ÉCART constaté au stock courant et signale
/// toute ligne dont le stock a bougé depuis le comptage (recompter ou accepter explicitement) — jamais d'écrasement
/// silencieux d'un mouvement intervenu pendant le comptage.
/// Verrous : ouverture = magasin (FOR SHARE) → verrou consultatif d'ouverture ; comptage = session (FOR SHARE) → ligne
/// (FOR UPDATE) → solde (FOR UPDATE) ; validation = session (FOR UPDATE) → soldes (FOR UPDATE, ordre (variante, emplacement)).
/// </summary>
public sealed class InventoryService(
    ICatalogUnitOfWork unitOfWork, ICatalogStore store, IStockStore stock, IInventoryStore inventories, TimeProvider clock, IAuditRecorder audit)
{
    /// <summary>Clé du verrou consultatif d'ouverture (« MMV05INV »).</summary>
    public const long OpeningLockKey = 0x4D4D_5630_3549_4E56;

    public const string ExcludeUncounted = "exclude";
    public const string SetUncountedToZero = "set_to_zero";

    public async Task<Outcome<Guid>> OpenAsync(InventoryOpenInput input, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (await inventories.FindSessionByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.OpenedByUserId == userId ? Outcome.Ok<Guid>(existing.Id, replayed: true) : Outcome.Conflict<Guid>("request_id_reused");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (input.StoreId is null)
        {
            errors["storeId"] = [CatalogFieldErrors.Required];
        }

        var scope = input.ScopeType ?? InventoryScopes.Full;
        if (scope is not (InventoryScopes.Full or InventoryScopes.Family or InventoryScopes.Selection))
        {
            errors["scopeType"] = [CatalogFieldErrors.Unknown];
        }

        string? family = null;
        if (scope == InventoryScopes.Family)
        {
            family = Families.Find(input.ScopeFamilyCode) is { } definition && definition.AllowedPolicies.Contains(StockPolicy.Stocked) ? definition.Code : null;
            if (family is null)
            {
                errors["scopeFamilyCode"] = [input.ScopeFamilyCode is null ? CatalogFieldErrors.Required : CatalogFieldErrors.NotAllowed];
            }
        }
        else if (input.ScopeFamilyCode is not null)
        {
            errors["scopeFamilyCode"] = [CatalogFieldErrors.NotAllowed];
        }

        var selection = (input.VariantIds ?? []).Distinct().ToList();
        if (scope == InventoryScopes.Selection ? selection.Count is 0 or > InventorySession.MaxLines : selection.Count > 0)
        {
            errors["variantIds"] = [scope == InventoryScopes.Selection ? (selection.Count == 0 ? CatalogFieldErrors.Required : CatalogFieldErrors.OutOfRange) : CatalogFieldErrors.NotAllowed];
        }

        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var storeId = input.StoreId!.Value;
        if (!await store.LockStoreAsync(storeId, cancellationToken))
        {
            return Outcome.Missing<Guid>("store_not_found");
        }

        if ((await store.GetStoreAsync(storeId, cancellationToken))!.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var storeLocations = (await store.ListLocationsAsync(storeId, cancellationToken)).Where(l => !l.IsArchived).ToList();
        var locationIds = input.LocationIds is { Count: > 0 } requested ? requested.Distinct().ToList() : storeLocations.Select(l => l.Id).ToList();
        if (locationIds.Count == 0 || locationIds.Any(id => storeLocations.All(l => l.Id != id)))
        {
            return Outcome.Invalid<Guid>("locationIds", CatalogFieldErrors.Unknown);
        }

        // Un seul inventaire ouvert par (variante, emplacement) : les ouvertures se sérialisent sur ce verrou.
        await unitOfWork.AdvisoryLockAsync(OpeningLockKey, cancellationToken);
        var candidates = await inventories.CandidateVariantsAsync(scope, family, scope == InventoryScopes.Selection ? selection : null, cancellationToken);
        if (scope == InventoryScopes.Selection && candidates.Count != selection.Count)
        {
            return Outcome.Invalid<Guid>("variantIds", CatalogFieldErrors.NotAllowed);
        }

        var keys = candidates.SelectMany(v => locationIds.Select(l => (VariantId: v, LocationId: l))).ToList();
        if (keys.Count == 0)
        {
            return Outcome.Refuse<Guid>("empty_scope");
        }

        if (keys.Count > InventorySession.MaxLines)
        {
            return Outcome.Refuse<Guid>("scope_too_large", new { lines = keys.Count, max = InventorySession.MaxLines });
        }

        var open = (await inventories.OpenLinesAsync(locationIds, cancellationToken)).ToHashSet();
        var conflicts = keys.Count(open.Contains);
        if (conflicts > 0)
        {
            return Outcome.Conflict<Guid>("inventory_open", new { lines = conflicts });
        }

        var balances = (await stock.BalancesAsync(candidates, locationIds, cancellationToken)).ToDictionary(b => (b.VariantId, b.LocationId));
        var now = CatalogClock.Now(clock);
        var session = InventorySession.Open(Guid.CreateVersion7(now), storeId, [.. locationIds], scope, family, input.Blind, userId, now, requestId);
        foreach (var key in keys.OrderBy(k => k.VariantId).ThenBy(k => k.LocationId))
        {
            session.AddLine(InventoryLine.Create(session.Id, key.VariantId, key.LocationId, balances.TryGetValue(key, out var b) ? b.OnHand : 0));
        }

        inventories.Add(session);
        CatalogAudit.Record(audit, userId, now, AuditActions.InventoryModule, AuditActions.InventoryOpened, AuditActions.InventoryEntity, session.Id,
        [
            CatalogAudit.Field("storeId", null, storeId.ToString()),
            CatalogAudit.Field("kind", null, scope),
            CatalogAudit.Field("lineCount", null, keys.Count.ToString(CultureInfo.InvariantCulture)),
        ]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return commit is { Status: CommitStatus.UniqueViolation, Constraint: "ux_inventory_sessions_creation_request_id" }
                && await inventories.FindSessionByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.OpenedByUserId == userId
                ? Outcome.Ok<Guid>(replay.Id, replayed: true)
                : CatalogService.UniqueRefusal<Guid>(commit);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Created<Guid>(session.Id);
    }

    /// <summary>
    /// Comptage (ou recomptage) d'une ligne : quantité constatée ; la quantité théorique est relue sous verrou du solde au
    /// même instant. Version de LIGNE : deux postes qui comptent la même ligne → le second reçoit 409 (jamais d'écrasement).
    /// </summary>
    public async Task<Outcome<Guid>> CountAsync(
        Guid sessionId, Guid variantId, Guid locationId, int? counted, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (counted is not (>= 0 and <= InventoryLine.MaxQuantity))
        {
            return Outcome.Invalid<Guid>("countedQuantity", counted is null ? CatalogFieldErrors.Required : CatalogFieldErrors.OutOfRange);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (line, refusal) = await OpenLineAsync(sessionId, variantId, locationId, version, requestId, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        if (line is null)
        {
            return Outcome.Ok<Guid>(sessionId, replayed: true);
        }

        var balance = await stock.LockBalanceAsync(variantId, locationId, cancellationToken);
        var now = CatalogClock.Now(clock);
        line.Count(counted.Value, balance.OnHand, userId, now, requestId);
        return await CommitAsync(transaction, sessionId, cancellationToken);
    }

    /// <summary>Justification d'un écart (motif de la liste des corrections d'inventaire ; commentaire obligatoire pour <c>other</c>).</summary>
    public async Task<Outcome<Guid>> JustifyAsync(
        Guid sessionId, Guid variantId, Guid locationId, string? reasonCode, string? comment, uint? version, Guid requestId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = StockService.Reason(MovementTypes.InventoryAdjustment, reasonCode, comment, errors);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var (line, refusal) = await OpenLineAsync(sessionId, variantId, locationId, version, requestId, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        if (line is null)
        {
            return Outcome.Ok<Guid>(sessionId, replayed: true);
        }

        line.Justify(reasonCode!, text, requestId);
        return await CommitAsync(transaction, sessionId, cancellationToken);
    }

    /// <summary>
    /// Validation atomique (<c>inventory.validate</c>) : toutes les lignes ou aucune. Refus 409 <c>stock_changed</c> si des
    /// lignes ont bougé depuis leur comptage (sauf <paramref name="acceptChangedLines"/>) ; 422 <c>reason_required</c> pour
    /// un écart non justifié ; 422 <c>stock_insufficient</c> si un écart rendrait le stock négatif ou entamerait le réservé.
    /// Lignes non comptées : exclues, ou mises à 0 sur choix explicite (écart justifié exigé).
    /// </summary>
    public async Task<Outcome<Guid>> ValidateAsync(
        Guid sessionId, string? uncounted, bool acceptChangedLines, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (uncounted is not (ExcludeUncounted or SetUncountedToZero))
        {
            return Outcome.Invalid<Guid>("uncounted", uncounted is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var session = await inventories.FindSessionAsync(sessionId, lockForUpdate: true, cancellationToken);
        if (session is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (session.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(sessionId, replayed: true);
        }

        if (version != session.Version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (!session.IsOpen)
        {
            return Outcome.Conflict<Guid>("inventory_closed");
        }

        var balances = (await stock.LockBalancesAsync([.. session.Lines.Select(l => (l.VariantId, l.LocationId))], cancellationToken))
            .ToDictionary(b => (b.VariantId, b.LocationId));
        var issues = new List<InventoryLineIssue>();
        var decisions = new List<(InventoryLine Line, InventoryLineOutcome Outcome, int Delta)>();
        foreach (var line in session.Lines.OrderBy(l => l.VariantId).ThenBy(l => l.LocationId))
        {
            var balance = balances[(line.VariantId, line.LocationId)];
            var (outcome, delta) = InventoryRules.Decide(line, balance.OnHand, balance.Reserved, uncounted == SetUncountedToZero, acceptChangedLines);
            switch (outcome)
            {
                case InventoryLineOutcome.ChangedSinceCount:
                    issues.Add(new(line.VariantId, line.LocationId, "stock_changed", balance.OnHand, line.ExpectedAtCount));
                    break;
                case InventoryLineOutcome.ReasonMissing:
                    issues.Add(new(line.VariantId, line.LocationId, "reason_required", balance.OnHand, line.ExpectedAtCount));
                    break;
                case InventoryLineOutcome.StockInsufficient:
                    issues.Add(new(line.VariantId, line.LocationId, "stock_insufficient", balance.OnHand, line.ExpectedAtCount));
                    break;
            }

            decisions.Add((line, outcome, delta));
        }

        if (issues.Count > 0)
        {
            var details = new { lines = issues };
            return issues.Any(i => i.Issue == "stock_changed")
                ? Outcome.Conflict<Guid>("stock_changed", details)
                : Outcome.Refuse<Guid>(issues.Any(i => i.Issue == "reason_required") ? "reason_required" : "stock_insufficient", details);
        }

        var now = CatalogClock.Now(clock);
        var applied = 0;
        foreach (var (line, outcome, delta) in decisions)
        {
            switch (outcome)
            {
                case InventoryLineOutcome.Apply:
                    var balance = balances[(line.VariantId, line.LocationId)];
                    var movement = StockMovement.Record(
                        Guid.CreateVersion7(now), balance, MovementTypes.InventoryAdjustment, delta, line.ReasonCode!, line.Comment, null, null, null,
                        StockSources.InventorySession, session.Id, userId, now, requestId);
                    stock.Add(movement);
                    line.Resolve(line.CountedQuantity is null ? InventoryResolutions.SetToZero : InventoryResolutions.Applied, delta, movement.Id);
                    applied++;
                    break;
                case InventoryLineOutcome.NoDifference:
                    line.Resolve(line.CountedQuantity is null ? InventoryResolutions.SetToZero : InventoryResolutions.NoDifference, 0, null);
                    break;
                default:
                    line.Resolve(InventoryResolutions.Excluded, null, null);
                    break;
            }
        }

        session.MarkValidated(uncounted, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.InventoryModule, AuditActions.InventoryValidated, AuditActions.InventoryEntity, session.Id,
        [
            CatalogAudit.Field("storeId", null, session.StoreId.ToString()),
            CatalogAudit.Field("lineCount", null, applied.ToString(CultureInfo.InvariantCulture)),
            CatalogAudit.Field("status", InventoryStatuses.Open, InventoryStatuses.Validated),
        ]);
        return await CommitAsync(transaction, sessionId, cancellationToken);
    }

    /// <summary>Abandon motivé, irréversible, sans effet sur le stock ; session conservée.</summary>
    public async Task<Outcome<Guid>> AbandonAsync(Guid sessionId, string? reason, uint? version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = CatalogText.Clean(reason, "reason", InventorySession.ReasonMaxLength, required: true, errors, multiline: true);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var session = await inventories.FindSessionAsync(sessionId, lockForUpdate: true, cancellationToken);
        if (session is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (session.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(sessionId, replayed: true);
        }

        if (version != session.Version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (!session.IsOpen)
        {
            return Outcome.Conflict<Guid>("inventory_closed");
        }

        var now = CatalogClock.Now(clock);
        session.Abandon(text!, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.InventoryModule, AuditActions.InventoryAbandoned, AuditActions.InventoryEntity, session.Id,
            [CatalogAudit.Field("status", InventoryStatuses.Open, InventoryStatuses.Abandoned), CatalogAudit.Field("reason")]);
        return await CommitAsync(transaction, sessionId, cancellationToken);
    }

    /// <summary>Session ouverte (FOR SHARE) puis ligne (FOR UPDATE) ; réémission → ligne <c>null</c> sans refus.</summary>
    private async Task<(InventoryLine? Line, Outcome<Guid>? Refusal)> OpenLineAsync(
        Guid sessionId, Guid variantId, Guid locationId, uint? version, Guid requestId, CancellationToken cancellationToken)
    {
        var status = await inventories.ShareSessionAsync(sessionId, cancellationToken);
        if (status is null)
        {
            return (null, Outcome.Missing<Guid>());
        }

        var line = await inventories.LockLineAsync(sessionId, variantId, locationId, cancellationToken);
        if (line is null)
        {
            return (null, Outcome.Missing<Guid>("line_not_found"));
        }

        if (line.LastRequestId == requestId)
        {
            return (null, null);
        }

        if (status != InventoryStatuses.Open)
        {
            return (null, Outcome.Conflict<Guid>("inventory_closed"));
        }

        return version != line.Version ? (null, Outcome.Conflict<Guid>()) : (line, null);
    }

    private async Task<Outcome<Guid>> CommitAsync(ITransactionScope transaction, Guid id, CancellationToken cancellationToken)
    {
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return CatalogService.UniqueRefusal<Guid>(commit);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Ok<Guid>(id);
    }
}
