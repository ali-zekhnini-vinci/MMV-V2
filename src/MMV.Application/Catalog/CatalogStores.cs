using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;
using MMV.Domain.Suppliers;

namespace MMV.Application.Catalog;

/// <summary>
/// Unité de travail partagée par les modules de la Phase 05 (même contexte EF que l'audit : une transaction). Toute
/// écriture de stock, de tarif ou d'inventaire suit : transaction → verrous dans l'ordre documenté → lectures → décision
/// → écritures → audit → <see cref="SaveAsync"/> → validation. Un refus après verrouillage libère la transaction sans rien écrire.
/// </summary>
public interface ICatalogUnitOfWork
{
    /// <summary>Transaction READ COMMITTED (sauf transaction déjà ouverte par l'appelant : portée neutre).</summary>
    Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Enregistre (une transaction) ; en cas d'échec, suivi vidé et cause bornée renvoyée.</summary>
    Task<CommitResult> SaveAsync(CancellationToken cancellationToken);

    /// <summary>Verrou consultatif de transaction (ouverture d'inventaire : un seul inventaire ouvert par ligne).</summary>
    Task AdvisoryLockAsync(long key, CancellationToken cancellationToken);
}

/// <summary>Magasins, emplacements, référentiel verres, modèles, variantes, fournisseurs et rattachements.</summary>
public interface ICatalogStore
{
    Task<Store?> GetStoreAsync(Guid storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Store>> ListStoresAsync(bool includeArchived, CancellationToken cancellationToken);

    Task<IReadOnlyList<Location>> ListLocationsAsync(Guid storeId, CancellationToken cancellationToken);

    Task<Location?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken);

    /// <summary><c>SELECT … FOR SHARE</c> du magasin (archivage concurrent) ; faux s'il n'existe pas.</summary>
    Task<bool> LockStoreAsync(Guid storeId, CancellationToken cancellationToken);

    Task<LensReference?> FindLensReferenceAsync(Guid id, CancellationToken cancellationToken);

    Task<LensReference?> FindLensReferenceByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LensReference>> ListLensReferencesAsync(CancellationToken cancellationToken);

    Task<bool> LensReferenceTakenAsync(string kindCode, Guid? manufacturerId, string nameKey, Guid? excludeId, CancellationToken cancellationToken);

    void Add(LensReference reference);

    Task<ProductModel?> FindModelAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductModel?> FindModelByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<ProductVariant?> FindVariantAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductVariant?> FindVariantByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductVariant>> FindVariantsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>SKU (clés normalisées) déjà pris par une AUTRE variante.</summary>
    Task<IReadOnlySet<string>> TakenSkusAsync(IReadOnlyCollection<string> skuKeys, Guid? excludeVariantId, CancellationToken cancellationToken);

    /// <summary>Codes-barres déjà pris par une AUTRE variante.</summary>
    Task<IReadOnlySet<string>> TakenBarcodesAsync(IReadOnlyCollection<string> barcodes, Guid? excludeVariantId, CancellationToken cancellationToken);

    void Add(ProductModel model);

    void Add(ProductVariant variant);

    /// <summary>Verrou de la variante (voir <see cref="RowLock"/>) ; faux si elle n'existe pas.</summary>
    Task<bool> LockVariantAsync(Guid variantId, RowLock strength, CancellationToken cancellationToken);

    /// <summary>Verrou des variantes dans l'ordre croissant des identifiants (ordre unique documenté).</summary>
    Task LockVariantsAsync(IReadOnlyCollection<Guid> variantIds, RowLock strength, CancellationToken cancellationToken);

    /// <summary>Somme des quantités en main et réservées de la variante, tous emplacements.</summary>
    Task<(int OnHand, int Reserved)> StockTotalsAsync(Guid variantId, CancellationToken cancellationToken);

    Task<bool> InOpenInventoryAsync(Guid variantId, CancellationToken cancellationToken);

    Task<Supplier?> FindSupplierAsync(Guid id, CancellationToken cancellationToken);

    Task<Supplier?> FindSupplierByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<bool> SupplierCodeTakenAsync(string codeKey, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Fournisseur NON archivé de même nom (casse, accents et espaces ignorés) — COR-05-08, aucun doublon.</summary>
    Task<bool> SupplierNameTakenAsync(string name, Guid? excludeId, CancellationToken cancellationToken);

    void Add(Supplier supplier);

    Task<bool> LockSupplierAsync(Guid supplierId, RowLock strength, CancellationToken cancellationToken);

    Task<VariantSupplier?> FindLinkAsync(Guid variantId, Guid supplierId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VariantSupplier>> LinksForVariantAsync(Guid variantId, CancellationToken cancellationToken);

    void Add(VariantSupplier link);

    /// <summary>Variantes <c>order_only</c> actives dont ce fournisseur est le principal (deviendraient « incomplètes »).</summary>
    Task<IReadOnlyList<ProductVariant>> OrderOnlyVariantsWithPrimaryAsync(Guid supplierId, CancellationToken cancellationToken);
}

/// <summary>Tarifs, historique, fiscalité (structure).</summary>
public interface IPricingStore
{
    /// <summary>
    /// Ligne de tarif (variante, magasin) VERROUILLÉE <c>FOR UPDATE</c>, créée vide si absente ; <c>Created</c> vrai si elle
    /// n'existait pas (version attendue : 0).
    /// </summary>
    Task<(VariantStorePrice Price, bool Created)> LockPriceAsync(Guid variantId, Guid storeId, string currencyCode, Guid userId, CancellationToken cancellationToken);

    /// <summary>Tarifs verrouillés dans l'ordre croissant des variantes (recalcul par lot).</summary>
    Task<IReadOnlyList<VariantStorePrice>> LockPricesAsync(Guid storeId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);

    Task<VariantStorePrice?> GetPriceAsync(Guid variantId, Guid storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VariantStorePrice>> PricesAsync(Guid storeId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);

    /// <summary>Tarifs du magasin en mode coefficient dont le prix stocké diffère du calcul courant (recalcul en attente).</summary>
    Task<IReadOnlyList<VariantStorePrice>> PendingRecalculationsAsync(Guid storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceHistoryEntry>> HistoryAsync(Guid variantId, Guid? storeId, CancellationToken cancellationToken);

    Task<bool> RequestAppliedAsync(Guid requestId, CancellationToken cancellationToken);

    void Add(PriceHistoryEntry entry);

    Task<TaxCategory?> TaxCategoryAsync(Guid storeId, string code, CancellationToken cancellationToken);
}

/// <summary>Soldes, mouvements, réceptions, seuils.</summary>
public interface IStockStore
{
    /// <summary>Solde (variante, emplacement) VERROUILLÉ <c>FOR UPDATE</c>, créé à 0 si absent.</summary>
    Task<StockBalance> LockBalanceAsync(Guid variantId, Guid locationId, CancellationToken cancellationToken);

    /// <summary>Soldes VERROUILLÉS <c>FOR UPDATE</c> dans l'ordre (variante, emplacement), créés à 0 si absents.</summary>
    Task<IReadOnlyList<StockBalance>> LockBalancesAsync(IReadOnlyCollection<(Guid VariantId, Guid LocationId)> keys, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockBalance>> BalancesAsync(IReadOnlyCollection<Guid> variantIds, IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken);

    void Add(StockMovement movement);

    void Add(Receipt receipt);

    Task<IReadOnlyList<StockMovement>> MovementsByRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<Receipt?> FindReceiptAsync(Guid id, CancellationToken cancellationToken);

    Task<Receipt?> FindReceiptByRequestAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>Quantités annoncées et reçues sur la chaîne d'une réception partielle (réception d'origine et compléments).</summary>
    Task<IReadOnlyList<ReceiptLine>> ReceiptChainLinesAsync(Guid rootReceiptId, CancellationToken cancellationToken);

    /// <summary>Seuil (variante, magasin) VERROUILLÉ, créé vide si absent.</summary>
    Task<(VariantStoreSetting Setting, bool Created)> LockSettingAsync(Guid variantId, Guid storeId, Guid userId, CancellationToken cancellationToken);
}

/// <summary>Sessions et lignes d'inventaire.</summary>
public interface IInventoryStore
{
    void Add(InventorySession session);

    /// <summary>Session avec ses lignes ; <paramref name="lockForUpdate"/> : session verrouillée <c>FOR UPDATE</c> avant lecture.</summary>
    Task<InventorySession?> FindSessionAsync(Guid id, bool lockForUpdate, CancellationToken cancellationToken);

    /// <summary>Statut de la session verrouillée <c>FOR SHARE</c> (comptages concurrents admis, validation attendue), ou <c>null</c>.</summary>
    Task<string?> ShareSessionAsync(Guid id, CancellationToken cancellationToken);

    Task<InventorySession?> FindSessionByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>Lignes (variante, emplacement) déjà présentes dans une AUTRE session ouverte.</summary>
    Task<IReadOnlyList<(Guid VariantId, Guid LocationId)>> OpenLinesAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken);

    /// <summary>Variantes <c>stocked</c> actives candidates (périmètre complet, famille ou sélection).</summary>
    Task<IReadOnlyList<Guid>> CandidateVariantsAsync(string scopeType, string? familyCode, IReadOnlyCollection<Guid>? selection, CancellationToken cancellationToken);

    Task<InventoryLine?> LockLineAsync(Guid sessionId, Guid variantId, Guid locationId, CancellationToken cancellationToken);
}

/// <summary>Lots d'import CSV.</summary>
public interface IImportStore
{
    void Add(ImportBatch batch);

    Task<ImportBatch?> FindBatchAsync(Guid id, CancellationToken cancellationToken);

    Task<ImportBatch?> FindBatchByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<bool> AppliedFileExistsAsync(string sha256, string kind, Guid? storeId, CancellationToken cancellationToken);

    /// <summary>Remplace le rapport d'un lot non clos (nouveau contrôle à blanc).</summary>
    Task ReplaceRowsAsync(Guid batchId, IReadOnlyList<ImportRow> rows, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportRow>> RowsAsync(Guid batchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Variantes par clé de SKU (contrôle et application d'un import).</summary>
    Task<IReadOnlyDictionary<string, ProductVariant>> VariantsBySkuAsync(IReadOnlyCollection<string> skuKeys, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, Supplier>> SuppliersByCodeAsync(IReadOnlyCollection<string> codeKeys, CancellationToken cancellationToken);

    /// <summary>
    /// COR-05-04 : variantes déjà rattachées au fournisseur par sa référence (clé normalisée) — un nouveau fichier du même
    /// fournisseur sans SKU met à jour ces variantes au lieu d'en créer des doublons.
    /// </summary>
    Task<IReadOnlyDictionary<string, ProductVariant>> VariantsBySupplierReferenceAsync(Guid supplierId, IReadOnlyCollection<string> referenceKeys, CancellationToken cancellationToken);

    /// <summary>COR-05-04 : variantes par code-barres (correspondance d'une ligne sans SKU).</summary>
    Task<IReadOnlyDictionary<string, ProductVariant>> VariantsByBarcodeAsync(IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);

    /// <summary>Modèles existants par (famille, marque, nom) normalisés.</summary>
    Task<IReadOnlyDictionary<string, ProductModel>> ModelsByKeyAsync(IReadOnlyCollection<string> familyCodes, CancellationToken cancellationToken);
}
