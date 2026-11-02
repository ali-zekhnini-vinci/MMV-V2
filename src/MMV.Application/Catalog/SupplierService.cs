using MMV.Application.Audit;
using MMV.Domain.Audit;
using MMV.Domain.Suppliers;

namespace MMV.Application.Catalog;

/// <summary>
/// Fiches fournisseurs (05-A, D-05-06) : création, modification, archivage, réactivation ; jamais de suppression.
/// L'audit cite les NOMS des champs modifiés seulement (contact = données personnelles minimales).
/// </summary>
public sealed class SupplierService(ICatalogUnitOfWork unitOfWork, ICatalogStore store, TimeProvider clock, IAuditRecorder audit)
{
    /// <param name="allowDuplicateName">
    /// Confirmation explicite qu'un fournisseur ACTIF de même nom est bien distinct (sinon refus <c>name: duplicate</c>,
    /// COR-05-08 : la création rapide ne l'envoie jamais, aucun doublon involontaire).
    /// </param>
    public async Task<Outcome<Guid>> CreateAsync(SupplierInput input, Guid requestId, Guid userId, CancellationToken cancellationToken, bool allowDuplicateName = false)
    {
        if (await store.FindSupplierByCreationRequestAsync(requestId, cancellationToken) is { } existing)
        {
            return existing.CreatedByUserId == userId ? Outcome.Ok<Guid>(existing.Id, replayed: true) : Outcome.Conflict<Guid>("request_id_reused");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var details = CatalogInputValidator.Supplier(input, errors);
        if (details is null)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        if (await store.SupplierCodeTakenAsync(Domain.Catalog.CatalogKeys.Normalize(details.Code), null, cancellationToken))
        {
            return Outcome.Invalid<Guid>("code", CatalogFieldErrors.Duplicate);
        }

        if (!allowDuplicateName && await store.SupplierNameTakenAsync(details.Name, null, cancellationToken))
        {
            return Outcome.Invalid<Guid>("name", CatalogFieldErrors.Duplicate);
        }

        var now = CatalogClock.Now(clock);
        var supplier = Supplier.Create(Guid.CreateVersion7(now), details, requestId, userId, now);
        store.Add(supplier);
        CatalogAudit.Record(audit, userId, now, AuditActions.SuppliersModule, AuditActions.SupplierCreated, AuditActions.SupplierEntity, supplier.Id,
            CatalogAudit.Fields(details.ChangedFields(null)));
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        return commit.Status switch
        {
            CommitStatus.Saved => Outcome.Created<Guid>(supplier.Id),
            CommitStatus.UniqueViolation when commit.Constraint == "ux_suppliers_creation_request_id" =>
                await store.FindSupplierByCreationRequestAsync(requestId, cancellationToken) is { } replay && replay.CreatedByUserId == userId
                    ? Outcome.Ok<Guid>(replay.Id, replayed: true)
                    : Outcome.Conflict<Guid>("request_id_reused"),
            _ => CatalogService.UniqueRefusal<Guid>(commit, "code"),
        };
    }

    public async Task<Outcome<Guid>> UpdateAsync(Guid id, SupplierInput input, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var supplier = await store.FindSupplierAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (supplier.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (supplier.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (supplier.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var details = CatalogInputValidator.Supplier(input, errors);
        if (details is null)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        var changed = details.ChangedFields(supplier.Snapshot());
        if (changed.Count == 0)
        {
            return Outcome.Ok<Guid>(id);
        }

        if (changed.Contains("code") && await store.SupplierCodeTakenAsync(Domain.Catalog.CatalogKeys.Normalize(details.Code), id, cancellationToken))
        {
            return Outcome.Invalid<Guid>("code", CatalogFieldErrors.Duplicate);
        }

        var now = CatalogClock.Now(clock);
        supplier.Update(details, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.SuppliersModule, AuditActions.SupplierUpdated, AuditActions.SupplierEntity, id, CatalogAudit.Fields(changed));
        return CatalogService.Commit(await unitOfWork.SaveAsync(cancellationToken), id, "code");
    }

    /// <summary>
    /// Archivage : les variantes <c>order_only</c> actives dont il est le principal deviennent « incomplètes » (QR-01) ;
    /// sans confirmation explicite (<paramref name="acceptIncompleteVariants"/>), refus 422 listant ces variantes.
    /// </summary>
    public async Task<Outcome<Guid>> ArchiveAsync(Guid id, string? reason, bool acceptIncompleteVariants, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = CatalogText.Clean(reason, "reason", Domain.Catalog.CatalogLimits.ReasonMaxLength, required: false, errors, multiline: true);
        if (errors.Count > 0)
        {
            return Outcome.Invalid<Guid>(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await store.LockSupplierAsync(id, RowLock.NoKeyUpdate, cancellationToken))
        {
            return Outcome.Missing<Guid>();
        }

        var supplier = (await store.FindSupplierAsync(id, cancellationToken))!;
        if (supplier.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (supplier.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (supplier.IsArchived)
        {
            return Outcome.Conflict<Guid>("archived");
        }

        var affected = await store.OrderOnlyVariantsWithPrimaryAsync(id, cancellationToken);
        if (affected.Count > 0 && !acceptIncompleteVariants)
        {
            return Outcome.Refuse<Guid>(
                "incomplete_variants_unconfirmed",
                new { variants = affected.Select(v => new { variantId = v.Id, sku = v.Sku, label = v.Label }).ToList() });
        }

        var now = CatalogClock.Now(clock);
        supplier.Archive(text, userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.SuppliersModule, AuditActions.SupplierArchived, AuditActions.SupplierEntity, id,
            [CatalogAudit.Field("lineCount", null, affected.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)), .. text is null ? Array.Empty<AuditChange>() : [CatalogAudit.Field("reason")]]);
        var commit = await unitOfWork.SaveAsync(cancellationToken);
        if (!commit.Saved)
        {
            return CatalogService.Commit(commit, id);
        }

        await transaction.CommitAsync(cancellationToken);
        return Outcome.Ok<Guid>(id);
    }

    public async Task<Outcome<Guid>> ReactivateAsync(Guid id, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var supplier = await store.FindSupplierAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Outcome.Missing<Guid>();
        }

        if (supplier.LastRequestId == requestId)
        {
            return Outcome.Ok<Guid>(id, replayed: true);
        }

        if (supplier.Version != version)
        {
            return Outcome.Conflict<Guid>();
        }

        if (!supplier.IsArchived)
        {
            return Outcome.Conflict<Guid>("not_archived");
        }

        var now = CatalogClock.Now(clock);
        supplier.Reactivate(userId, now, requestId);
        CatalogAudit.Record(audit, userId, now, AuditActions.SuppliersModule, AuditActions.SupplierReactivated, AuditActions.SupplierEntity, id, []);
        return CatalogService.Commit(await unitOfWork.SaveAsync(cancellationToken), id);
    }
}
