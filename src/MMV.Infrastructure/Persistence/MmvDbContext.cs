using Microsoft.EntityFrameworkCore;
using MMV.Domain.Appointments;
using MMV.Domain.Audit;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Domain.Optical;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;
using MMV.Domain.Suppliers;
using MMV.Infrastructure.Catalog;
using MMV.Infrastructure.Appointments;
using MMV.Infrastructure.Audit;
using MMV.Infrastructure.Clients;
using MMV.Infrastructure.Identity;
using MMV.Infrastructure.Optical;

namespace MMV.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core de MMV V2, fourni par Npgsql (PostgreSQL exclusivement).
/// Phase 02 : entités techniques d'identité (schéma <c>auth</c>). Phase 03-A2 : clients (schéma <c>clients</c>).
/// Phase 03-B2 : rendez-vous et éligibilité des collaborateurs (schéma <c>appointments</c>). Phase 03-D2 : journal
/// d'audit métier (schéma <c>audit</c>). Phase 04-B : dossier optique, ordonnances et mesures magasin (schéma
/// <c>optical</c>). Phase 05 : magasins, catalogue, tarifs, fournisseurs, stock, inventaires et import CSV (schémas
/// <c>org</c>, <c>catalog</c>, <c>pricing</c>, <c>stock</c>, <c>imports</c>).
/// </summary>
public sealed class MmvDbContext(DbContextOptions<MmvDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<UserSession> Sessions => Set<UserSession>();

    public DbSet<BootstrapRecord> BootstrapRecords => Set<BootstrapRecord>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<AppointmentResponsible> AppointmentResponsibles => Set<AppointmentResponsible>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Prescription> Prescriptions => Set<Prescription>();

    public DbSet<Measurement> Measurements => Set<Measurement>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<LensReference> LensReferences => Set<LensReference>();

    public DbSet<ProductModel> ProductModels => Set<ProductModel>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<VariantSupplier> VariantSuppliers => Set<VariantSupplier>();

    public DbSet<VariantStorePrice> VariantStorePrices => Set<VariantStorePrice>();

    public DbSet<PriceHistoryEntry> PriceHistory => Set<PriceHistoryEntry>();

    public DbSet<TaxCategory> TaxCategories => Set<TaxCategory>();

    public DbSet<VariantStoreSetting> VariantStoreSettings => Set<VariantStoreSetting>();

    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<InventorySession> InventorySessions => Set<InventorySession>();

    public DbSet<InventoryLine> InventoryLines => Set<InventoryLine>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportRow> ImportRows => Set<ImportRow>();

    // Corrections Phase 05 (migration 0008, COR-05-04 / COR-05-05).
    public DbSet<SupplierImportProfile> SupplierImportProfiles => Set<SupplierImportProfile>();

    public DbSet<LensOffer> LensOffers => Set<LensOffer>();

    public DbSet<LensOfferRevision> LensOfferRevisions => Set<LensOfferRevision>();

    // CSV-IMPORT-AI-01 (migration 0009) : assistant d'import.
    public DbSet<BatchOriginal> BatchOriginals => Set<BatchOriginal>();

    public DbSet<BatchAssistant> BatchAssistants => Set<BatchAssistant>();

    public DbSet<BatchAnalysis> BatchAnalyses => Set<BatchAnalysis>();

    public DbSet<BatchAnalysisEvent> BatchAnalysisEvents => Set<BatchAnalysisEvent>();

    public DbSet<BatchDecision> BatchDecisions => Set<BatchDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        IdentityModel.Configure(modelBuilder);
        ClientModel.Configure(modelBuilder);
        AppointmentModel.Configure(modelBuilder);
        AuditModel.Configure(modelBuilder);
        OpticalModel.Configure(modelBuilder);
        CatalogStockModel.Configure(modelBuilder);
    }
}
