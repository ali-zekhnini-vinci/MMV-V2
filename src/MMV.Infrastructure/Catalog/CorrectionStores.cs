using Microsoft.EntityFrameworkCore;
using MMV.Application.Catalog;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure.Catalog;

/// <summary>Offres de verres (0008) : lectures suivies pour écriture, listes sans suivi, révisions en ajout seul.</summary>
internal sealed class LensOfferStore(MmvDbContext context) : ILensOfferStore
{
    public Task<LensOffer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.LensOffers.Include(o => o.Treatments).SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<LensOffer?> FindByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.LensOffers.AsNoTracking().SingleOrDefaultAsync(o => o.CreationRequestId == requestId, cancellationToken);

    public async Task<IReadOnlyList<LensOffer>> ListAsync(Guid? rangeId, Guid? supplierId, bool includeArchived, CancellationToken cancellationToken) =>
        await context.LensOffers.AsNoTracking().Include(o => o.Treatments)
            .Where(o => (rangeId == null || o.RangeId == rangeId) && (supplierId == null || o.SupplierId == supplierId) && (includeArchived || o.ArchivedAtUtc == null))
            .OrderBy(o => o.RangeId).ThenBy(o => o.SupplierId).ThenBy(o => o.LensTypeCode).ThenBy(o => o.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LensOfferRevision>> RevisionsAsync(Guid offerId, CancellationToken cancellationToken) =>
        await context.LensOfferRevisions.AsNoTracking().Where(r => r.OfferId == offerId).OrderByDescending(r => r.Revision).Take(50).ToListAsync(cancellationToken);

    public void Add(LensOffer offer) => context.LensOffers.Add(offer);

    public void Add(LensOfferRevision revision) => context.LensOfferRevisions.Add(revision);
}

/// <summary>Modèles d'import fournisseur (0008).</summary>
internal sealed class ImportProfileStore(MmvDbContext context) : IImportProfileStore
{
    public Task<SupplierImportProfile?> FindAsync(Guid supplierId, string kind, CancellationToken cancellationToken) =>
        context.SupplierImportProfiles.SingleOrDefaultAsync(p => p.SupplierId == supplierId && p.Kind == kind, cancellationToken);

    public void Add(SupplierImportProfile profile) => context.SupplierImportProfiles.Add(profile);
}
