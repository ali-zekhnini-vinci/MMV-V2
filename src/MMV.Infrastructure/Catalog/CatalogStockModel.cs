using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;
using MMV.Domain.Stores;
using MMV.Domain.Suppliers;

namespace MMV.Infrastructure.Catalog;

/// <summary>Famille (liste fermée de six, Q01) : lignes insérées par la migration 0007, aucune écriture par l'API.</summary>
internal sealed class FamilyRow
{
    public string Code { get; set; } = string.Empty;

    public string LabelFr { get; set; } = string.Empty;

    public string[] AllowedPolicies { get; set; } = [];

    public int SortOrder { get; set; }
}

/// <summary>
/// Schémas de la Phase 05 (migration 0007_CatalogStockModule, une seule migration pour les sous-lots 05-A à 05-D —
/// décision technique documentée dans docs/phase/05/DECISIONS_TECHNIQUES_05.md) :
/// <list type="bullet">
/// <item><c>org</c> : magasins (devise ISO 4217, profil fiscal) et emplacements ;</item>
/// <item><c>catalog</c> : familles, référentiel verres, modèles, variantes, fournisseurs, rattachements ;</item>
/// <item><c>pricing</c> : tarifs par (variante, magasin), historique en ajout seul, catégories fiscales (structure) ;</item>
/// <item><c>stock</c> : seuils, soldes, mouvements en ajout seul, réceptions, sessions et lignes d'inventaire ;</item>
/// <item><c>imports</c> : lots et lignes d'import CSV.</item>
/// </list>
/// Montants en <c>numeric</c> à échelle fixe (jamais de flottant), devise garantie par clé étrangère composite
/// (magasin, devise), concurrence <c>xmin</c>, idempotence par identifiants de requête, aucune suppression physique
/// (déclencheurs créés par la migration, hors modèle EF).
/// </summary>
internal static class CatalogStockModel
{
    public const string Org = "org";
    public const string CatalogSchema = "catalog";
    public const string PricingSchema = "pricing";
    public const string StockSchema = "stock";
    public const string ImportsSchema = "imports";
    public const string ReceiptNumberSequence = "receipt_number_seq";
    public const string InventoryNumberSequence = "inventory_number_seq";

    private const string CodeFormat = "^[a-z][a-z_]{0,31}$";

    public static IReadOnlyList<string> Schemas { get; } = [Org, CatalogSchema, PricingSchema, StockSchema, ImportsSchema];

    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureOrg(modelBuilder);
        ConfigureCatalog(modelBuilder);
        ConfigureSuppliers(modelBuilder);
        ConfigurePricing(modelBuilder);
        ConfigureStock(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureImports(modelBuilder);
        ApplySnakeCase(modelBuilder);
    }

    // ---------- org ----------

    private static void ConfigureOrg(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>(store =>
        {
            store.ToTable("stores", Org, t =>
            {
                t.HasCheckConstraint("ck_stores_code_format", "code ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'");
                t.HasCheckConstraint("ck_stores_country_format", "country_code ~ '^[A-Z]{2}$'");
                t.HasCheckConstraint("ck_stores_currency", "currency_code IN ('EUR', 'MAD')");
                t.HasCheckConstraint("ck_stores_fiscal_status", "fiscal_profile_status IN ('not_validated', 'validated')");
                t.HasCheckConstraint("ck_stores_fiscal_validation", "(fiscal_profile_status = 'validated') = (fiscal_validated_at_utc IS NOT NULL)");
            });
            store.HasKey(s => s.Id).HasName("pk_stores");
            store.Property(s => s.Id).ValueGeneratedNever();
            store.Property(s => s.Code).HasMaxLength(Store.CodeMaxLength).IsRequired();
            store.Property(s => s.Name).HasMaxLength(Store.NameMaxLength).IsRequired();
            store.Property(s => s.CountryCode).HasColumnType("character(2)").IsRequired();
            store.Property(s => s.CurrencyCode).HasColumnType("character(3)").IsRequired();
            store.Property(s => s.FiscalProfileStatus).HasMaxLength(16).IsRequired();
            store.Ignore(s => s.IsArchived);
            store.Ignore(s => s.IsFiscalProfileValidated);
            store.HasIndex(s => s.Code).IsUnique().HasDatabaseName("ux_stores_code");
            // Cible des clés étrangères composites (magasin, devise) : aucun montant dans une autre devise que celle du magasin.
            store.HasAlternateKey(s => new { s.Id, s.CurrencyCode }).HasName("ak_stores_id_currency");
        });

        modelBuilder.Entity<Location>(location =>
        {
            location.ToTable("locations", Org, t => t.HasCheckConstraint("ck_locations_code_format", "code ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'"));
            location.HasKey(l => l.Id).HasName("pk_locations");
            location.Property(l => l.Id).ValueGeneratedNever();
            location.Property(l => l.Code).HasMaxLength(Location.CodeMaxLength).IsRequired();
            location.Property(l => l.Name).HasMaxLength(Location.NameMaxLength).IsRequired();
            location.Ignore(l => l.IsArchived);
            location.HasOne<Store>().WithMany().HasForeignKey(l => l.StoreId).HasConstraintName("fk_locations_stores").OnDelete(DeleteBehavior.Restrict);
            location.HasIndex(l => new { l.StoreId, l.Code }).IsUnique().HasDatabaseName("ux_locations_store_code");
            location.HasIndex(l => l.StoreId).IsUnique().HasFilter("is_default AND archived_at_utc IS NULL").HasDatabaseName("ux_locations_one_default");
        });
    }

    // ---------- catalog ----------

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FamilyRow>(family =>
        {
            family.ToTable("families", CatalogSchema, t =>
            {
                t.HasCheckConstraint("ck_families_code_format", $"code ~ '{CodeFormat}'");
                t.HasCheckConstraint("ck_families_policies", "allowed_policies <@ ARRAY['stocked', 'order_only', 'service']::text[] AND cardinality(allowed_policies) >= 1");
            });
            family.HasKey(f => f.Code).HasName("pk_families");
            family.Property(f => f.Code).HasMaxLength(32);
            family.Property(f => f.LabelFr).HasMaxLength(60).IsRequired();
            family.Property(f => f.AllowedPolicies).HasColumnType("text[]").IsRequired();
            family.HasData(Families.All.Select(f => new FamilyRow
            {
                Code = f.Code,
                LabelFr = f.Label,
                AllowedPolicies = [.. f.AllowedPolicies.Select(StockPolicies.ToCode)],
                SortOrder = f.SortOrder,
            }));
        });

        modelBuilder.Entity<LensReference>(reference =>
        {
            reference.ToTable("lens_references", CatalogSchema, t =>
            {
                t.HasCheckConstraint("ck_lens_references_kind", "kind IN ('manufacturer', 'range', 'index', 'treatment')");
                t.HasCheckConstraint(
                    "ck_lens_references_parent",
                    "(kind = 'range' AND manufacturer_id IS NOT NULL) OR kind = 'treatment' OR (kind IN ('manufacturer', 'index') AND manufacturer_id IS NULL)");
                t.HasCheckConstraint("ck_lens_references_index_value", "(kind = 'index') = (index_value IS NOT NULL) AND (index_value IS NULL OR (index_value > 1 AND index_value < 2.5))");
                t.HasCheckConstraint("ck_lens_references_name", "btrim(name) <> ''");
            });
            ConfigureArchivable(reference, "lens_references");
            reference.HasKey(r => r.Id).HasName("pk_lens_references");
            reference.Property(r => r.Id).ValueGeneratedNever();
            reference.Property(r => r.KindCode).HasColumnName("kind").HasMaxLength(16).IsRequired();
            reference.Property(r => r.Name).HasMaxLength(CatalogLimits.LensNameMaxLength).IsRequired();
            reference.Property(r => r.NameKey).HasMaxLength(CatalogLimits.LensNameMaxLength).IsRequired();
            reference.Property(r => r.IndexValue).HasColumnType("numeric(4,3)");
            reference.Ignore(r => r.Kind);
            reference.HasOne<LensReference>().WithMany().HasForeignKey(r => r.ManufacturerId)
                .HasConstraintName("fk_lens_references_manufacturer").OnDelete(DeleteBehavior.Restrict);
            // Unicité (type, fabricant, nom normalisé) ; fabricant absent compté comme une valeur (NULLS NOT DISTINCT).
            reference.HasIndex(r => r.ManufacturerId).HasDatabaseName("ix_lens_references_manufacturer_id");
            reference.HasIndex(r => new { r.KindCode, r.ManufacturerId, r.NameKey }).IsUnique().AreNullsDistinct(false)
                .HasDatabaseName("ux_lens_references_kind_parent_name");
        });

        modelBuilder.Entity<ProductModel>(model =>
        {
            model.ToTable("product_models", CatalogSchema, t =>
            {
                t.HasCheckConstraint("ck_product_models_name", "btrim(name) <> ''");
                t.HasCheckConstraint("ck_product_models_attributes", "jsonb_typeof(attributes) = 'object'");
                t.HasCheckConstraint("ck_product_models_lens_range", "(family_code = 'ophthalmic_lenses') = (lens_range_id IS NOT NULL)");
            });
            ConfigureArchivable(model, "product_models");
            model.HasKey(m => m.Id).HasName("pk_product_models");
            model.Property(m => m.Id).ValueGeneratedNever();
            model.Property(m => m.FamilyCode).HasMaxLength(32).IsRequired();
            model.Property(m => m.Brand).HasMaxLength(CatalogLimits.BrandMaxLength);
            model.Property(m => m.Name).HasMaxLength(CatalogLimits.NameMaxLength).IsRequired();
            model.Property(m => m.Description).HasMaxLength(CatalogLimits.DescriptionMaxLength);
            model.Property(m => m.AttributesJson).HasColumnName("attributes").HasColumnType("jsonb").IsRequired();
            model.Ignore(m => m.Attributes);
            model.HasOne<FamilyRow>().WithMany().HasForeignKey(m => m.FamilyCode).HasConstraintName("fk_product_models_families").OnDelete(DeleteBehavior.Restrict);
            model.HasOne<LensReference>().WithMany().HasForeignKey(m => m.LensRangeId).HasConstraintName("fk_product_models_lens_range").OnDelete(DeleteBehavior.Restrict);
            model.HasMany(m => m.Variants).WithOne().HasForeignKey(v => v.ModelId).HasConstraintName("fk_product_variants_models").OnDelete(DeleteBehavior.Restrict);
            model.Navigation(m => m.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
            model.HasIndex(m => m.CreationRequestId).IsUnique().HasDatabaseName("ux_product_models_creation_request_id");
            model.HasIndex(m => new { m.FamilyCode, m.Name }).HasDatabaseName("ix_product_models_family_name");
            model.HasIndex(m => m.LensRangeId).HasDatabaseName("ix_product_models_lens_range_id");
        });

        modelBuilder.Entity<ProductVariant>(variant =>
        {
            variant.ToTable("product_variants", CatalogSchema, t =>
            {
                t.HasCheckConstraint("ck_product_variants_sku", "btrim(sku) <> '' AND sku_key = lower(btrim(sku))");
                t.HasCheckConstraint("ck_product_variants_barcode", "barcode IS NULL OR barcode ~ '^([0-9]{8}|[0-9]{12,14})$'");
                t.HasCheckConstraint("ck_product_variants_label", "btrim(label) <> ''");
                t.HasCheckConstraint("ck_product_variants_attributes", "jsonb_typeof(attributes) = 'object'");
                t.HasCheckConstraint("ck_product_variants_policy", "stock_policy IN ('stocked', 'order_only', 'service')");
                t.HasCheckConstraint("ck_product_variants_tax_category_format", $"tax_category_code IS NULL OR tax_category_code ~ '{CodeFormat}'");
            });
            ConfigureArchivable(variant, "product_variants");
            variant.HasKey(v => v.Id).HasName("pk_product_variants");
            variant.Property(v => v.Id).ValueGeneratedNever();
            variant.Property(v => v.Sku).HasMaxLength(CatalogLimits.SkuMaxLength).IsRequired();
            variant.Property(v => v.SkuKey).HasMaxLength(CatalogLimits.SkuMaxLength).IsRequired();
            variant.Property(v => v.ManufacturerReference).HasMaxLength(CatalogLimits.ReferenceMaxLength);
            variant.Property(v => v.Barcode).HasMaxLength(CatalogLimits.BarcodeMaxLength);
            variant.Property(v => v.Label).HasMaxLength(CatalogLimits.LabelMaxLength).IsRequired();
            variant.Property(v => v.AttributesJson).HasColumnName("attributes").HasColumnType("jsonb").IsRequired();
            variant.Property(v => v.StockPolicyCode).HasColumnName("stock_policy").HasMaxLength(16).IsRequired();
            variant.Property(v => v.TaxCategoryCode).HasMaxLength(32);
            variant.Ignore(v => v.StockPolicy);
            variant.Ignore(v => v.Attributes);
            variant.HasOne<LensReference>().WithMany().HasForeignKey(v => v.LensIndexId).HasConstraintName("fk_product_variants_lens_index").OnDelete(DeleteBehavior.Restrict);
            variant.HasMany(v => v.Treatments).WithOne().HasForeignKey(t => t.VariantId).HasConstraintName("fk_variant_lens_treatments_variants").OnDelete(DeleteBehavior.Cascade);
            variant.Navigation(v => v.Treatments).UsePropertyAccessMode(PropertyAccessMode.Field);
            variant.HasIndex(v => v.SkuKey).IsUnique().HasDatabaseName("ux_product_variants_sku");
            variant.HasIndex(v => v.Barcode).IsUnique().HasFilter("barcode IS NOT NULL").HasDatabaseName("ux_product_variants_barcode");
            variant.HasIndex(v => v.CreationRequestId).IsUnique().HasFilter("creation_request_id IS NOT NULL").HasDatabaseName("ux_product_variants_creation_request_id");
            variant.HasIndex(v => v.ModelId).HasDatabaseName("ix_product_variants_model_id");
            variant.HasIndex(v => v.LensIndexId).HasDatabaseName("ix_product_variants_lens_index_id");
        });

        modelBuilder.Entity<VariantLensTreatment>(treatment =>
        {
            treatment.ToTable("variant_lens_treatments", CatalogSchema);
            treatment.HasKey(t => new { t.VariantId, t.TreatmentId }).HasName("pk_variant_lens_treatments");
            treatment.HasOne<LensReference>().WithMany().HasForeignKey(t => t.TreatmentId).HasConstraintName("fk_variant_lens_treatments_treatment").OnDelete(DeleteBehavior.Restrict);
            treatment.HasIndex(t => t.TreatmentId).HasDatabaseName("ix_variant_lens_treatments_treatment_id");
        });
    }

    // ---------- fournisseurs ----------

    private static void ConfigureSuppliers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(supplier =>
        {
            supplier.ToTable("suppliers", CatalogSchema, t =>
            {
                t.HasCheckConstraint("ck_suppliers_code", "btrim(code) <> '' AND code_key = lower(btrim(code))");
                t.HasCheckConstraint("ck_suppliers_name", "btrim(name) <> ''");
                t.HasCheckConstraint("ck_suppliers_country_format", "country_code ~ '^[A-Z]{2}$'");
                t.HasCheckConstraint("ck_suppliers_legal_identifiers", "jsonb_typeof(legal_identifiers) = 'object'");
            });
            ConfigureArchivable(supplier, "suppliers");
            supplier.HasKey(s => s.Id).HasName("pk_suppliers");
            supplier.Property(s => s.Id).ValueGeneratedNever();
            supplier.Property(s => s.Code).HasMaxLength(Supplier.CodeMaxLength).IsRequired();
            supplier.Property(s => s.CodeKey).HasMaxLength(Supplier.CodeMaxLength).IsRequired();
            supplier.Property(s => s.Name).HasMaxLength(Supplier.NameMaxLength).IsRequired();
            supplier.Property(s => s.CountryCode).HasColumnType("character(2)").IsRequired();
            supplier.Property(s => s.ContactName).HasMaxLength(Supplier.ContactMaxLength);
            supplier.Property(s => s.Email).HasMaxLength(Supplier.EmailMaxLength);
            supplier.Property(s => s.Phone).HasMaxLength(Supplier.PhoneMaxLength);
            supplier.Property(s => s.AddressLine).HasMaxLength(Supplier.AddressLineMaxLength);
            supplier.Property(s => s.PostalCode).HasMaxLength(Supplier.PostalCodeMaxLength);
            supplier.Property(s => s.City).HasMaxLength(Supplier.CityMaxLength);
            supplier.Property(s => s.LegalIdentifiersJson).HasColumnName("legal_identifiers").HasColumnType("jsonb").IsRequired();
            supplier.Property(s => s.Notes).HasMaxLength(Supplier.NotesMaxLength);
            supplier.Ignore(s => s.LegalIdentifiers);
            supplier.HasIndex(s => s.CodeKey).IsUnique().HasDatabaseName("ux_suppliers_code");
            supplier.HasIndex(s => s.CreationRequestId).IsUnique().HasDatabaseName("ux_suppliers_creation_request_id");
            supplier.HasIndex(s => s.Name).HasDatabaseName("ix_suppliers_name");
        });

        modelBuilder.Entity<VariantSupplier>(link =>
        {
            link.ToTable("variant_suppliers", CatalogSchema, t =>
            {
                t.HasCheckConstraint("ck_variant_suppliers_price", "supplier_price_ht IS NULL OR supplier_price_ht >= 0");
                t.HasCheckConstraint("ck_variant_suppliers_currency", "(supplier_price_ht IS NULL) = (supplier_currency IS NULL) AND (supplier_currency IS NULL OR supplier_currency ~ '^[A-Z]{3}$')");
                t.HasCheckConstraint("ck_variant_suppliers_lead_time", $"lead_time_days IS NULL OR (lead_time_days >= 0 AND lead_time_days <= {VariantSupplier.LeadTimeMaxDays})");
                t.HasCheckConstraint("ck_variant_suppliers_removed", "(removed_at_utc IS NULL) = (removed_by_user_id IS NULL) AND (removed_at_utc IS NULL OR NOT is_primary)");
            });
            link.HasKey(l => new { l.VariantId, l.SupplierId }).HasName("pk_variant_suppliers");
            link.Property(l => l.SupplierReference).HasMaxLength(CatalogLimits.ReferenceMaxLength);
            link.Property(l => l.SupplierPriceHt).HasColumnType("numeric(14,4)");
            link.Property(l => l.SupplierCurrency).HasColumnType("character(3)");
            link.Property(l => l.Version).IsRowVersion();
            link.Ignore(l => l.IsRemoved);
            link.HasOne<ProductVariant>().WithMany().HasForeignKey(l => l.VariantId).HasConstraintName("fk_variant_suppliers_variants").OnDelete(DeleteBehavior.Restrict);
            link.HasOne<Supplier>().WithMany().HasForeignKey(l => l.SupplierId).HasConstraintName("fk_variant_suppliers_suppliers").OnDelete(DeleteBehavior.Restrict);
            link.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.CreatedByUserId).HasConstraintName("fk_variant_suppliers_created_by_users").OnDelete(DeleteBehavior.Restrict);
            link.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.UpdatedByUserId).HasConstraintName("fk_variant_suppliers_updated_by_users").OnDelete(DeleteBehavior.Restrict);
            link.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.RemovedByUserId).HasConstraintName("fk_variant_suppliers_removed_by_users").OnDelete(DeleteBehavior.Restrict);
            // Au plus un fournisseur principal par variante.
            link.HasIndex(l => l.VariantId).IsUnique().HasFilter("is_primary").HasDatabaseName("ux_variant_suppliers_one_primary");
            link.HasIndex(l => l.SupplierId).HasDatabaseName("ix_variant_suppliers_supplier_id");
            link.HasIndex(l => l.CreatedByUserId).HasDatabaseName("ix_variant_suppliers_created_by_user_id");
            link.HasIndex(l => l.UpdatedByUserId).HasDatabaseName("ix_variant_suppliers_updated_by_user_id");
            link.HasIndex(l => l.RemovedByUserId).HasDatabaseName("ix_variant_suppliers_removed_by_user_id");
        });
    }

    // ---------- pricing ----------

    private static void ConfigurePricing(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VariantStorePrice>(price =>
        {
            price.ToTable("variant_store_prices", PricingSchema, t =>
            {
                t.HasCheckConstraint("ck_variant_store_prices_purchase", "purchase_price_ht IS NULL OR purchase_price_ht >= 0");
                t.HasCheckConstraint("ck_variant_store_prices_coefficient", "coefficient IS NULL OR (coefficient > 0 AND coefficient <= 100)");
                t.HasCheckConstraint("ck_variant_store_prices_sale", "sale_price_ht IS NULL OR sale_price_ht >= 0");
                t.HasCheckConstraint("ck_variant_store_prices_mode", "pricing_mode IN ('coefficient', 'manual')");
                t.HasCheckConstraint(
                    "ck_variant_store_prices_coefficient_mode",
                    "pricing_mode = 'manual' OR sale_price_ht IS NULL OR (purchase_price_ht IS NOT NULL AND coefficient IS NOT NULL)");
            });
            price.HasKey(p => new { p.VariantId, p.StoreId }).HasName("pk_variant_store_prices");
            price.Property(p => p.CurrencyCode).HasColumnType("character(3)").IsRequired();
            price.Property(p => p.PurchasePriceHt).HasColumnType("numeric(14,4)");
            price.Property(p => p.Coefficient).HasColumnType("numeric(8,4)");
            price.Property(p => p.PricingModeCode).HasColumnName("pricing_mode").HasMaxLength(16).IsRequired();
            price.Property(p => p.SalePriceHt).HasColumnType("numeric(12,2)");
            price.Property(p => p.Version).IsRowVersion();
            price.Ignore(p => p.Mode);
            price.Ignore(p => p.Status);
            price.Ignore(p => p.ComputedSalePriceHt);
            price.HasOne<ProductVariant>().WithMany().HasForeignKey(p => p.VariantId).HasConstraintName("fk_variant_store_prices_variants").OnDelete(DeleteBehavior.Restrict);
            // Clé étrangère composite : la devise du prix EST celle du magasin (aucun prix EUR pour un magasin MAD).
            price.HasOne<Store>().WithMany().HasForeignKey(p => new { p.StoreId, p.CurrencyCode }).HasPrincipalKey(s => new { s.Id, s.CurrencyCode })
                .HasConstraintName("fk_variant_store_prices_store_currency").OnDelete(DeleteBehavior.Restrict);
            price.HasOne<UserAccount>().WithMany().HasForeignKey(p => p.UpdatedByUserId).HasConstraintName("fk_variant_store_prices_updated_by_users").OnDelete(DeleteBehavior.Restrict);
            price.HasIndex(p => new { p.StoreId, p.CurrencyCode }).HasDatabaseName("ix_variant_store_prices_store_currency");
            price.HasIndex(p => p.UpdatedByUserId).HasDatabaseName("ix_variant_store_prices_updated_by_user_id");
        });

        modelBuilder.Entity<PriceHistoryEntry>(entry =>
        {
            entry.ToTable("price_history", PricingSchema, t =>
            {
                t.HasCheckConstraint("ck_price_history_field", "field IN ('purchase_price_ht', 'coefficient', 'pricing_mode', 'sale_price_ht', 'supplier_price_ht')");
                t.HasCheckConstraint("ck_price_history_origin", "origin IN ('edit', 'recalculation', 'manual_price', 'import', 'receipt')");
                t.HasCheckConstraint("ck_price_history_mode", "(field = 'pricing_mode') = (new_mode IS NOT NULL) AND (old_mode IS NULL OR field = 'pricing_mode')");
                t.HasCheckConstraint("ck_price_history_scope", "(field = 'supplier_price_ht') = (supplier_id IS NOT NULL) AND (field = 'supplier_price_ht' OR store_id IS NOT NULL)");
                t.HasCheckConstraint("ck_price_history_currency", "currency_code ~ '^[A-Z]{3}$'");
            });
            entry.HasKey(e => e.Id).HasName("pk_price_history");
            entry.Property(e => e.Id).ValueGeneratedNever();
            entry.Property(e => e.CurrencyCode).HasColumnType("character(3)").IsRequired();
            entry.Property(e => e.Field).HasMaxLength(32).IsRequired();
            entry.Property(e => e.OldValue).HasColumnType("numeric(14,4)");
            entry.Property(e => e.NewValue).HasColumnType("numeric(14,4)");
            entry.Property(e => e.OldMode).HasMaxLength(16);
            entry.Property(e => e.NewMode).HasMaxLength(16);
            entry.Property(e => e.Origin).HasMaxLength(16).IsRequired();
            entry.Property(e => e.Reason).HasMaxLength(CatalogLimits.ReasonMaxLength);
            entry.HasOne<ProductVariant>().WithMany().HasForeignKey(e => e.VariantId).HasConstraintName("fk_price_history_variants").OnDelete(DeleteBehavior.Restrict);
            entry.HasOne<Store>().WithMany().HasForeignKey(e => e.StoreId).HasConstraintName("fk_price_history_stores").OnDelete(DeleteBehavior.Restrict);
            entry.HasOne<Supplier>().WithMany().HasForeignKey(e => e.SupplierId).HasConstraintName("fk_price_history_suppliers").OnDelete(DeleteBehavior.Restrict);
            entry.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.ChangedByUserId).HasConstraintName("fk_price_history_changed_by_users").OnDelete(DeleteBehavior.Restrict);
            entry.HasIndex(e => new { e.VariantId, e.StoreId, e.ChangedAtUtc }).HasDatabaseName("ix_price_history_variant_store_changed");
            entry.HasIndex(e => e.StoreId).HasDatabaseName("ix_price_history_store_id");
            entry.HasIndex(e => e.SupplierId).HasDatabaseName("ix_price_history_supplier_id");
            entry.HasIndex(e => e.ChangedByUserId).HasDatabaseName("ix_price_history_changed_by_user_id");
        });

        modelBuilder.Entity<TaxCategory>(category =>
        {
            category.ToTable("tax_categories", PricingSchema, t =>
            {
                t.HasCheckConstraint("ck_tax_categories_code_format", $"code ~ '{CodeFormat}'");
                t.HasCheckConstraint("ck_tax_categories_rate", "rate IS NULL OR (rate >= 0 AND rate < 1)");
                t.HasCheckConstraint("ck_tax_categories_validation", "(rate IS NULL) = (validated_at_utc IS NULL) AND (validated_at_utc IS NULL) = (validated_by_user_id IS NULL)");
            });
            category.HasKey(c => new { c.StoreId, c.Code }).HasName("pk_tax_categories");
            category.Property(c => c.Code).HasMaxLength(32);
            category.Property(c => c.Label).HasMaxLength(120).IsRequired();
            category.Property(c => c.Rate).HasColumnType("numeric(7,4)");
            category.Property(c => c.LegalReference).HasMaxLength(200);
            category.Ignore(c => c.IsValidated);
            category.HasOne<Store>().WithMany().HasForeignKey(c => c.StoreId).HasConstraintName("fk_tax_categories_stores").OnDelete(DeleteBehavior.Restrict);
            category.HasOne<UserAccount>().WithMany().HasForeignKey(c => c.ValidatedByUserId).HasConstraintName("fk_tax_categories_validated_by_users").OnDelete(DeleteBehavior.Restrict);
            category.HasIndex(c => c.ValidatedByUserId).HasDatabaseName("ix_tax_categories_validated_by_user_id");
        });
    }

    // ---------- stock ----------

    private static void ConfigureStock(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(ReceiptNumberSequence, StockSchema).StartsAt(1).IncrementsBy(1).HasMax(999_999_999_999_999_999).IsCyclic(false);
        modelBuilder.HasSequence<long>(InventoryNumberSequence, StockSchema).StartsAt(1).IncrementsBy(1).HasMax(999_999_999_999_999_999).IsCyclic(false);

        modelBuilder.Entity<VariantStoreSetting>(setting =>
        {
            setting.ToTable("variant_store_settings", StockSchema, t =>
                t.HasCheckConstraint("ck_variant_store_settings_threshold", $"low_stock_threshold IS NULL OR (low_stock_threshold >= 0 AND low_stock_threshold <= {VariantStoreSetting.ThresholdMax})"));
            setting.HasKey(s => new { s.VariantId, s.StoreId }).HasName("pk_variant_store_settings");
            setting.Property(s => s.Version).IsRowVersion();
            setting.HasOne<ProductVariant>().WithMany().HasForeignKey(s => s.VariantId).HasConstraintName("fk_variant_store_settings_variants").OnDelete(DeleteBehavior.Restrict);
            setting.HasOne<Store>().WithMany().HasForeignKey(s => s.StoreId).HasConstraintName("fk_variant_store_settings_stores").OnDelete(DeleteBehavior.Restrict);
            setting.HasOne<UserAccount>().WithMany().HasForeignKey(s => s.UpdatedByUserId).HasConstraintName("fk_variant_store_settings_updated_by_users").OnDelete(DeleteBehavior.Restrict);
            setting.HasIndex(s => s.StoreId).HasDatabaseName("ix_variant_store_settings_store_id");
            setting.HasIndex(s => s.UpdatedByUserId).HasDatabaseName("ix_variant_store_settings_updated_by_user_id");
        });

        modelBuilder.Entity<StockBalance>(balance =>
        {
            balance.ToTable("balances", StockSchema, t =>
            {
                // D-05-03 : stock négatif interdit, garanti par la base. D-05-17 : réservé ≤ en main (0 en Phase 05).
                t.HasCheckConstraint("ck_balances_on_hand", "on_hand >= 0");
                t.HasCheckConstraint("ck_balances_reserved", "reserved >= 0 AND reserved <= on_hand");
            });
            balance.HasKey(b => new { b.VariantId, b.LocationId }).HasName("pk_balances");
            balance.Property(b => b.Reserved).HasDefaultValue(0);
            balance.Property(b => b.Version).IsRowVersion();
            balance.Ignore(b => b.Available);
            balance.HasOne<ProductVariant>().WithMany().HasForeignKey(b => b.VariantId).HasConstraintName("fk_balances_variants").OnDelete(DeleteBehavior.Restrict);
            balance.HasOne<Location>().WithMany().HasForeignKey(b => b.LocationId).HasConstraintName("fk_balances_locations").OnDelete(DeleteBehavior.Restrict);
            balance.HasIndex(b => b.LocationId).HasDatabaseName("ix_balances_location_id");
        });

        modelBuilder.Entity<StockMovement>(movement =>
        {
            movement.ToTable("movements", StockSchema, t =>
            {
                t.HasCheckConstraint("ck_movements_type", "movement_type IN ('receipt', 'manual_in', 'manual_out', 'adjustment', 'inventory_adjustment')");
                t.HasCheckConstraint("ck_movements_delta", "quantity_delta <> 0 AND quantity_after = quantity_before + quantity_delta AND quantity_before >= 0 AND quantity_after >= 0");
                t.HasCheckConstraint("ck_movements_sign", "(movement_type NOT IN ('receipt', 'manual_in') OR quantity_delta > 0) AND (movement_type <> 'manual_out' OR quantity_delta < 0)");
                t.HasCheckConstraint("ck_movements_reason", $"reason_code ~ '{CodeFormat}' AND (reason_code <> 'other' OR comment IS NOT NULL)");
                t.HasCheckConstraint(
                    "ck_movements_receipt",
                    "(movement_type = 'receipt') = (supplier_id IS NOT NULL) AND (unit_cost_ht IS NULL OR (movement_type = 'receipt' AND unit_cost_ht >= 0))"
                    + " AND (unit_cost_ht IS NULL) = (currency_code IS NULL)");
                t.HasCheckConstraint("ck_movements_source", "(source_type IS NULL) = (source_id IS NULL) AND (source_type IS NULL OR source_type IN ('receipt', 'inventory_session'))");
            });
            movement.HasKey(m => m.Id).HasName("pk_movements");
            movement.Property(m => m.Id).ValueGeneratedNever();
            movement.Property(m => m.Seq).UseIdentityAlwaysColumn();
            movement.Property(m => m.MovementType).HasMaxLength(32).IsRequired();
            movement.Property(m => m.ReasonCode).HasMaxLength(32).IsRequired();
            movement.Property(m => m.Comment).HasMaxLength(StockReasons.CommentMaxLength);
            movement.Property(m => m.UnitCostHt).HasColumnType("numeric(14,4)");
            movement.Property(m => m.CurrencyCode).HasColumnType("character(3)");
            movement.Property(m => m.SourceType).HasMaxLength(32);
            movement.HasOne<ProductVariant>().WithMany().HasForeignKey(m => m.VariantId).HasConstraintName("fk_movements_variants").OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<Location>().WithMany().HasForeignKey(m => m.LocationId).HasConstraintName("fk_movements_locations").OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<Supplier>().WithMany().HasForeignKey(m => m.SupplierId).HasConstraintName("fk_movements_suppliers").OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<UserAccount>().WithMany().HasForeignKey(m => m.PerformedByUserId).HasConstraintName("fk_movements_performed_by_users").OnDelete(DeleteBehavior.Restrict);
            movement.HasIndex(m => m.Seq).IsUnique().HasDatabaseName("ux_movements_seq");
            movement.HasIndex(m => new { m.RequestId, m.VariantId, m.LocationId }).IsUnique().HasDatabaseName("ux_movements_request_variant_location");
            movement.HasIndex(m => new { m.VariantId, m.LocationId, m.Seq }).IsDescending(false, false, true).HasDatabaseName("ix_movements_variant_location_seq");
            movement.HasIndex(m => m.PerformedAtUtc).HasDatabaseName("ix_movements_performed_at");
            movement.HasIndex(m => new { m.SourceType, m.SourceId }).HasDatabaseName("ix_movements_source");
            movement.HasIndex(m => m.LocationId).HasDatabaseName("ix_movements_location_id");
            movement.HasIndex(m => m.SupplierId).HasDatabaseName("ix_movements_supplier_id");
            movement.HasIndex(m => m.PerformedByUserId).HasDatabaseName("ix_movements_performed_by_user_id");
        });

        modelBuilder.Entity<Receipt>(receipt =>
        {
            receipt.ToTable("receipts", StockSchema, t => t.HasCheckConstraint("ck_receipts_number_format", "number ~ '^REC-[0-9]{6,18}$'"));
            receipt.HasKey(r => r.Id).HasName("pk_receipts");
            receipt.Property(r => r.Id).ValueGeneratedNever();
            var number = receipt.Property(r => r.Number).HasMaxLength(24).IsRequired()
                .HasDefaultValueSql($"'REC-' || to_char(nextval('{StockSchema}.{ReceiptNumberSequence}'), 'FM999999999999000000')");
            number.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            number.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            receipt.Property(r => r.CurrencyCode).HasColumnType("character(3)").IsRequired();
            receipt.Property(r => r.SupplierDocumentReference).HasMaxLength(Receipt.DocumentReferenceMaxLength);
            receipt.Property(r => r.Notes).HasMaxLength(Receipt.NotesMaxLength);
            receipt.HasOne<Store>().WithMany().HasForeignKey(r => new { r.StoreId, r.CurrencyCode }).HasPrincipalKey(s => new { s.Id, s.CurrencyCode })
                .HasConstraintName("fk_receipts_store_currency").OnDelete(DeleteBehavior.Restrict);
            receipt.HasOne<Location>().WithMany().HasForeignKey(r => r.LocationId).HasConstraintName("fk_receipts_locations").OnDelete(DeleteBehavior.Restrict);
            receipt.HasOne<Supplier>().WithMany().HasForeignKey(r => r.SupplierId).HasConstraintName("fk_receipts_suppliers").OnDelete(DeleteBehavior.Restrict);
            receipt.HasOne<Receipt>().WithMany().HasForeignKey(r => r.CompletesReceiptId).HasConstraintName("fk_receipts_completes").OnDelete(DeleteBehavior.Restrict);
            receipt.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.ReceivedByUserId).HasConstraintName("fk_receipts_received_by_users").OnDelete(DeleteBehavior.Restrict);
            receipt.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.ReceiptId).HasConstraintName("fk_receipt_lines_receipts").OnDelete(DeleteBehavior.Restrict);
            receipt.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            receipt.HasIndex(r => r.Number).IsUnique().HasDatabaseName("ux_receipts_number");
            receipt.HasIndex(r => r.RequestId).IsUnique().HasDatabaseName("ux_receipts_request_id");
            receipt.HasIndex(r => new { r.StoreId, r.ReceivedAtUtc }).HasDatabaseName("ix_receipts_store_received");
            receipt.HasIndex(r => new { r.StoreId, r.CurrencyCode }).HasDatabaseName("ix_receipts_store_currency");
            receipt.HasIndex(r => r.LocationId).HasDatabaseName("ix_receipts_location_id");
            receipt.HasIndex(r => r.SupplierId).HasDatabaseName("ix_receipts_supplier_id");
            receipt.HasIndex(r => r.CompletesReceiptId).HasDatabaseName("ix_receipts_completes_receipt_id");
            receipt.HasIndex(r => r.ReceivedByUserId).HasDatabaseName("ix_receipts_received_by_user_id");
        });

        modelBuilder.Entity<ReceiptLine>(line =>
        {
            line.ToTable("receipt_lines", StockSchema, t =>
            {
                t.HasCheckConstraint("ck_receipt_lines_quantities", "received_quantity >= 0 AND (announced_quantity IS NULL OR announced_quantity >= 0) AND (received_quantity > 0 OR COALESCE(announced_quantity, 0) > 0)");
                t.HasCheckConstraint("ck_receipt_lines_movement", "(received_quantity > 0) = (movement_id IS NOT NULL)");
                t.HasCheckConstraint("ck_receipt_lines_cost", "unit_cost_ht IS NULL OR unit_cost_ht >= 0");
                t.HasCheckConstraint("ck_receipt_lines_line_no", "line_no >= 1");
            });
            line.HasKey(l => new { l.ReceiptId, l.LineNo }).HasName("pk_receipt_lines");
            line.Property(l => l.UnitCostHt).HasColumnType("numeric(14,4)");
            line.HasOne<ProductVariant>().WithMany().HasForeignKey(l => l.VariantId).HasConstraintName("fk_receipt_lines_variants").OnDelete(DeleteBehavior.Restrict);
            line.HasOne<StockMovement>().WithMany().HasForeignKey(l => l.MovementId).HasConstraintName("fk_receipt_lines_movements").OnDelete(DeleteBehavior.Restrict);
            line.HasIndex(l => l.VariantId).HasDatabaseName("ix_receipt_lines_variant_id");
            line.HasIndex(l => l.MovementId).IsUnique().HasFilter("movement_id IS NOT NULL").HasDatabaseName("ux_receipt_lines_movement_id");
        });
    }

    // ---------- inventaires ----------

    private static void ConfigureInventory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventorySession>(session =>
        {
            session.ToTable("inventory_sessions", StockSchema, t =>
            {
                t.HasCheckConstraint("ck_inventory_sessions_number_format", "number ~ '^INV-[0-9]{6,18}$'");
                t.HasCheckConstraint("ck_inventory_sessions_status", "status IN ('open', 'validated', 'abandoned')");
                t.HasCheckConstraint("ck_inventory_sessions_scope", "scope_type IN ('full', 'family', 'selection') AND (scope_type = 'family') = (scope_family_code IS NOT NULL)");
                t.HasCheckConstraint("ck_inventory_sessions_locations", "cardinality(location_ids) >= 1");
                t.HasCheckConstraint(
                    "ck_inventory_sessions_lifecycle",
                    "(status = 'validated') = (validated_at_utc IS NOT NULL) AND (validated_at_utc IS NULL) = (validated_by_user_id IS NULL)"
                    + " AND (validated_at_utc IS NULL) = (uncounted_resolution IS NULL)"
                    + " AND (status = 'abandoned') = (abandoned_at_utc IS NOT NULL) AND (abandoned_at_utc IS NULL) = (abandoned_by_user_id IS NULL)"
                    + " AND (abandoned_at_utc IS NULL) = (abandon_reason IS NULL)");
                t.HasCheckConstraint("ck_inventory_sessions_uncounted", "uncounted_resolution IS NULL OR uncounted_resolution IN ('exclude', 'set_to_zero')");
            });
            session.HasKey(s => s.Id).HasName("pk_inventory_sessions");
            session.Property(s => s.Id).ValueGeneratedNever();
            var number = session.Property(s => s.Number).HasMaxLength(24).IsRequired()
                .HasDefaultValueSql($"'INV-' || to_char(nextval('{StockSchema}.{InventoryNumberSequence}'), 'FM999999999999000000')");
            number.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            number.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            session.Property(s => s.LocationIds).HasColumnType("uuid[]").IsRequired();
            session.Property(s => s.ScopeType).HasMaxLength(16).IsRequired();
            session.Property(s => s.ScopeFamilyCode).HasMaxLength(32);
            session.Property(s => s.Status).HasMaxLength(16).IsRequired();
            session.Property(s => s.UncountedResolution).HasMaxLength(16);
            session.Property(s => s.AbandonReason).HasMaxLength(InventorySession.ReasonMaxLength);
            session.Property(s => s.Version).IsRowVersion();
            session.Ignore(s => s.IsOpen);
            session.HasOne<Store>().WithMany().HasForeignKey(s => s.StoreId).HasConstraintName("fk_inventory_sessions_stores").OnDelete(DeleteBehavior.Restrict);
            session.HasOne<FamilyRow>().WithMany().HasForeignKey(s => s.ScopeFamilyCode).HasConstraintName("fk_inventory_sessions_families").OnDelete(DeleteBehavior.Restrict);
            session.HasOne<UserAccount>().WithMany().HasForeignKey(s => s.OpenedByUserId).HasConstraintName("fk_inventory_sessions_opened_by_users").OnDelete(DeleteBehavior.Restrict);
            session.HasOne<UserAccount>().WithMany().HasForeignKey(s => s.ValidatedByUserId).HasConstraintName("fk_inventory_sessions_validated_by_users").OnDelete(DeleteBehavior.Restrict);
            session.HasOne<UserAccount>().WithMany().HasForeignKey(s => s.AbandonedByUserId).HasConstraintName("fk_inventory_sessions_abandoned_by_users").OnDelete(DeleteBehavior.Restrict);
            session.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SessionId).HasConstraintName("fk_inventory_lines_sessions").OnDelete(DeleteBehavior.Restrict);
            session.Navigation(s => s.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            session.HasIndex(s => s.Number).IsUnique().HasDatabaseName("ux_inventory_sessions_number");
            session.HasIndex(s => s.CreationRequestId).IsUnique().HasDatabaseName("ux_inventory_sessions_creation_request_id");
            session.HasIndex(s => new { s.StoreId, s.Status }).HasDatabaseName("ix_inventory_sessions_store_status");
            session.HasIndex(s => s.ScopeFamilyCode).HasDatabaseName("ix_inventory_sessions_scope_family_code");
            session.HasIndex(s => s.OpenedByUserId).HasDatabaseName("ix_inventory_sessions_opened_by_user_id");
            session.HasIndex(s => s.ValidatedByUserId).HasDatabaseName("ix_inventory_sessions_validated_by_user_id");
            session.HasIndex(s => s.AbandonedByUserId).HasDatabaseName("ix_inventory_sessions_abandoned_by_user_id");
        });

        modelBuilder.Entity<InventoryLine>(line =>
        {
            line.ToTable("inventory_lines", StockSchema, t =>
            {
                t.HasCheckConstraint("ck_inventory_lines_quantities", $"expected_at_open >= 0 AND (counted_quantity IS NULL OR (counted_quantity >= 0 AND counted_quantity <= {InventoryLine.MaxQuantity}))");
                t.HasCheckConstraint(
                    "ck_inventory_lines_count",
                    "(counted_quantity IS NULL) = (expected_at_count IS NULL) AND (counted_quantity IS NULL) = (counted_at_utc IS NULL) AND (counted_at_utc IS NULL) = (counted_by_user_id IS NULL)");
                t.HasCheckConstraint("ck_inventory_lines_reason", $"(reason_code IS NULL OR reason_code ~ '{CodeFormat}') AND (comment IS NULL OR reason_code IS NOT NULL) AND (reason_code IS DISTINCT FROM 'other' OR comment IS NOT NULL)");
                t.HasCheckConstraint("ck_inventory_lines_resolution", "resolution IS NULL OR resolution IN ('applied', 'no_difference', 'excluded', 'set_to_zero')");
                t.HasCheckConstraint("ck_inventory_lines_applied", "(movement_id IS NOT NULL) = (applied_delta IS NOT NULL AND applied_delta <> 0) AND (movement_id IS NULL OR resolution IN ('applied', 'set_to_zero'))");
            });
            line.HasKey(l => new { l.SessionId, l.VariantId, l.LocationId }).HasName("pk_inventory_lines");
            line.Property(l => l.ReasonCode).HasMaxLength(32);
            line.Property(l => l.Comment).HasMaxLength(InventoryLine.CommentMaxLength);
            line.Property(l => l.Resolution).HasMaxLength(16);
            line.Property(l => l.Version).IsRowVersion();
            line.Ignore(l => l.Difference);
            line.HasOne<ProductVariant>().WithMany().HasForeignKey(l => l.VariantId).HasConstraintName("fk_inventory_lines_variants").OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Location>().WithMany().HasForeignKey(l => l.LocationId).HasConstraintName("fk_inventory_lines_locations").OnDelete(DeleteBehavior.Restrict);
            line.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.CountedByUserId).HasConstraintName("fk_inventory_lines_counted_by_users").OnDelete(DeleteBehavior.Restrict);
            line.HasOne<StockMovement>().WithMany().HasForeignKey(l => l.MovementId).HasConstraintName("fk_inventory_lines_movements").OnDelete(DeleteBehavior.Restrict);
            line.HasIndex(l => new { l.VariantId, l.LocationId }).HasDatabaseName("ix_inventory_lines_variant_location");
            line.HasIndex(l => l.LocationId).HasDatabaseName("ix_inventory_lines_location_id");
            line.HasIndex(l => l.CountedByUserId).HasDatabaseName("ix_inventory_lines_counted_by_user_id");
            line.HasIndex(l => l.MovementId).IsUnique().HasFilter("movement_id IS NOT NULL").HasDatabaseName("ux_inventory_lines_movement_id");
        });
    }

    // ---------- imports ----------

    private static void ConfigureImports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportBatch>(batch =>
        {
            batch.ToTable("batches", ImportsSchema, t =>
            {
                t.HasCheckConstraint("ck_batches_kind", "kind IN ('catalog', 'suppliers', 'prices')");
                t.HasCheckConstraint("ck_batches_store", "(kind = 'prices') = (store_id IS NOT NULL)");
                t.HasCheckConstraint("ck_batches_status", "status IN ('uploaded', 'checked', 'applied', 'cancelled')");
                t.HasCheckConstraint("ck_batches_sha256", "file_sha256 ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_batches_size", $"octet_length(content) <= {ImportBatch.MaxBytes} AND row_count >= 0 AND row_count <= {ImportBatch.MaxRows}");
                t.HasCheckConstraint("ck_batches_mapping", "jsonb_typeof(mapping) = 'object'");
                t.HasCheckConstraint("ck_batches_lifecycle", "(status = 'applied') = (applied_at_utc IS NOT NULL) AND (applied_at_utc IS NULL) = (applied_by_user_id IS NULL) AND (checked_at_utc IS NULL) = (checked_by_user_id IS NULL) AND (applied_at_utc IS NULL OR checked_at_utc IS NOT NULL)");
            });
            batch.HasKey(b => b.Id).HasName("pk_batches");
            batch.Property(b => b.Id).ValueGeneratedNever();
            batch.Property(b => b.Kind).HasMaxLength(16).IsRequired();
            batch.Property(b => b.FileName).HasMaxLength(ImportBatch.FileNameMaxLength).IsRequired();
            batch.Property(b => b.FileSha256).HasColumnType("character(64)").IsRequired();
            batch.Property(b => b.Content).IsRequired();
            batch.Property(b => b.MappingJson).HasColumnName("mapping").HasColumnType("jsonb").IsRequired();
            batch.Property(b => b.Status).HasMaxLength(16).IsRequired();
            batch.Property(b => b.Version).IsRowVersion();
            batch.HasOne<Store>().WithMany().HasForeignKey(b => b.StoreId).HasConstraintName("fk_batches_stores").OnDelete(DeleteBehavior.Restrict);
            batch.HasOne<UserAccount>().WithMany().HasForeignKey(b => b.UploadedByUserId).HasConstraintName("fk_batches_uploaded_by_users").OnDelete(DeleteBehavior.Restrict);
            batch.HasOne<UserAccount>().WithMany().HasForeignKey(b => b.CheckedByUserId).HasConstraintName("fk_batches_checked_by_users").OnDelete(DeleteBehavior.Restrict);
            batch.HasOne<UserAccount>().WithMany().HasForeignKey(b => b.AppliedByUserId).HasConstraintName("fk_batches_applied_by_users").OnDelete(DeleteBehavior.Restrict);
            batch.HasIndex(b => b.CreationRequestId).IsUnique().HasDatabaseName("ux_batches_creation_request_id");
            // Un même fichier (empreinte, type, magasin) n'est appliqué qu'une fois (réémission, double import).
            batch.HasIndex(b => new { b.FileSha256, b.Kind, b.StoreId }).IsUnique().AreNullsDistinct(false).HasFilter("status = 'applied'")
                .HasDatabaseName("ux_batches_applied_file");
            batch.HasIndex(b => b.UploadedAtUtc).HasDatabaseName("ix_batches_uploaded_at");
            batch.HasIndex(b => b.StoreId).HasDatabaseName("ix_batches_store_id");
            batch.HasIndex(b => b.UploadedByUserId).HasDatabaseName("ix_batches_uploaded_by_user_id");
            batch.HasIndex(b => b.CheckedByUserId).HasDatabaseName("ix_batches_checked_by_user_id");
            batch.HasIndex(b => b.AppliedByUserId).HasDatabaseName("ix_batches_applied_by_user_id");
        });

        modelBuilder.Entity<ImportRow>(row =>
        {
            row.ToTable("rows", ImportsSchema, t =>
            {
                t.HasCheckConstraint("ck_rows_outcome", "outcome IN ('create', 'update', 'unchanged', 'error')");
                t.HasCheckConstraint("ck_rows_json", "jsonb_typeof(values_json) = 'object' AND jsonb_typeof(errors) = 'array'");
                t.HasCheckConstraint("ck_rows_line_no", "line_no >= 1");
            });
            row.HasKey(r => new { r.BatchId, r.LineNo }).HasName("pk_rows");
            row.Property(r => r.ValuesJson).HasColumnName("values_json").HasColumnType("jsonb").IsRequired();
            row.Property(r => r.Outcome).HasMaxLength(16).IsRequired();
            row.Property(r => r.ErrorsJson).HasColumnName("errors").HasColumnType("jsonb").IsRequired();
            row.Property(r => r.TargetKey).HasMaxLength(200);
            row.HasOne<ImportBatch>().WithMany().HasForeignKey(r => r.BatchId).HasConstraintName("fk_rows_batches").OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ---------- outils ----------

    private static void ConfigureArchivable<T>(EntityTypeBuilder<T> entity, string table)
        where T : ArchivableEntity
    {
        entity.Property(e => e.Version).IsRowVersion();
        entity.Ignore(e => e.IsArchived);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.CreatedByUserId).HasConstraintName($"fk_{table}_created_by_users").OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.UpdatedByUserId).HasConstraintName($"fk_{table}_updated_by_users").OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.ArchivedByUserId).HasConstraintName($"fk_{table}_archived_by_users").OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.CreatedByUserId).HasDatabaseName($"ix_{table}_created_by_user_id");
        entity.HasIndex(e => e.UpdatedByUserId).HasDatabaseName($"ix_{table}_updated_by_user_id");
        entity.HasIndex(e => e.ArchivedByUserId).HasDatabaseName($"ix_{table}_archived_by_user_id");
        entity.Property(e => e.ArchiveReason).HasMaxLength(CatalogLimits.ReasonMaxLength);
    }

    /// <summary>Colonnes en snake_case pour les entités de la Phase 05 (sauf nom explicite déjà posé, et xmin).</summary>
    private static void ApplySnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => Schemas.Contains(e.GetSchema() ?? string.Empty)))
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.IsConcurrencyToken && property.ClrType == typeof(uint))
                {
                    continue;
                }

                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(SnakeCase(property.Name));
                }
            }
        }
    }

    internal static string SnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
