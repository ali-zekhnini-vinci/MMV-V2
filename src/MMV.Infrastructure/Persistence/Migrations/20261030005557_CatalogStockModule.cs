using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
// Code généré par dotnet ef (tableaux de colonnes d'index composites, données des familles) : CA1861 et CA1814 sans objet ici.
#pragma warning disable CA1861
#pragma warning disable CA1814

namespace MMV.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 0007 — Phase 05 (mandat GO du 30/10/2026), UNE migration pour les sous-lots 05-A à 05-D : schémas <c>org</c>
    /// (magasins, emplacements), <c>catalog</c> (six familles insérées, référentiel verres, modèles, variantes,
    /// fournisseurs, rattachements), <c>pricing</c> (tarifs par magasin, historique en ajout seul, catégories fiscales
    /// VIDES), <c>stock</c> (seuils, soldes, mouvements en ajout seul, réceptions, inventaires), <c>imports</c> (lots CSV),
    /// seize permissions ajoutées à la contrainte des droits. Aucun magasin, aucun taux, aucune donnée pilote insérés.
    /// Jamais appliquée au démarrage : script SQL idempotent soumis à la revue G05-SQL.
    /// </summary>
    public partial class CatalogStockModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions");

            migrationBuilder.EnsureSchema(
                name: "stock");

            migrationBuilder.EnsureSchema(
                name: "imports");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "org");

            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.CreateSequence(
                name: "inventory_number_seq",
                schema: "stock",
                maxValue: 999999999999999999L);

            migrationBuilder.CreateSequence(
                name: "receipt_number_seq",
                schema: "stock",
                maxValue: 999999999999999999L);

            migrationBuilder.CreateTable(
                name: "families",
                schema: "catalog",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    allowed_policies = table.Column<string[]>(type: "text[]", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_families", x => x.code);
                    table.CheckConstraint("ck_families_code_format", "code ~ '^[a-z][a-z_]{0,31}$'");
                    table.CheckConstraint("ck_families_policies", "allowed_policies <@ ARRAY['stocked', 'order_only', 'service']::text[] AND cardinality(allowed_policies) >= 1");
                });

            migrationBuilder.CreateTable(
                name: "lens_references",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    manufacturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    index_value = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archive_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lens_references", x => x.id);
                    table.CheckConstraint("ck_lens_references_index_value", "(kind = 'index') = (index_value IS NOT NULL) AND (index_value IS NULL OR (index_value > 1 AND index_value < 2.5))");
                    table.CheckConstraint("ck_lens_references_kind", "kind IN ('manufacturer', 'range', 'index', 'treatment')");
                    table.CheckConstraint("ck_lens_references_name", "btrim(name) <> ''");
                    table.CheckConstraint("ck_lens_references_parent", "(kind = 'range' AND manufacturer_id IS NOT NULL) OR kind = 'treatment' OR (kind IN ('manufacturer', 'index') AND manufacturer_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_lens_references_archived_by_users",
                        column: x => x.archived_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_references_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_references_manufacturer",
                        column: x => x.manufacturer_id,
                        principalSchema: "catalog",
                        principalTable: "lens_references",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_references_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country_code = table.Column<string>(type: "character(2)", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", nullable: false),
                    fiscal_profile_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    fiscal_validated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stores", x => x.id);
                    table.UniqueConstraint("ak_stores_id_currency", x => new { x.id, x.currency_code });
                    table.CheckConstraint("ck_stores_code_format", "code ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'");
                    table.CheckConstraint("ck_stores_country_format", "country_code ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_stores_currency", "currency_code IN ('EUR', 'MAD')");
                    table.CheckConstraint("ck_stores_fiscal_status", "fiscal_profile_status IN ('not_validated', 'validated')");
                    table.CheckConstraint("ck_stores_fiscal_validation", "(fiscal_profile_status = 'validated') = (fiscal_validated_at_utc IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    code_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character(2)", nullable: false),
                    contact_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    address_line = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    legal_identifiers = table.Column<string>(type: "jsonb", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archive_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                    table.CheckConstraint("ck_suppliers_code", "btrim(code) <> '' AND code_key = lower(btrim(code))");
                    table.CheckConstraint("ck_suppliers_country_format", "country_code ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_suppliers_legal_identifiers", "jsonb_typeof(legal_identifiers) = 'object'");
                    table.CheckConstraint("ck_suppliers_name", "btrim(name) <> ''");
                    table.ForeignKey(
                        name: "fk_suppliers_archived_by_users",
                        column: x => x.archived_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_suppliers_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_suppliers_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_models",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    lens_range_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archive_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_models", x => x.id);
                    table.CheckConstraint("ck_product_models_attributes", "jsonb_typeof(attributes) = 'object'");
                    table.CheckConstraint("ck_product_models_lens_range", "(family_code = 'ophthalmic_lenses') = (lens_range_id IS NOT NULL)");
                    table.CheckConstraint("ck_product_models_name", "btrim(name) <> ''");
                    table.ForeignKey(
                        name: "fk_product_models_archived_by_users",
                        column: x => x.archived_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_models_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_models_families",
                        column: x => x.family_code,
                        principalSchema: "catalog",
                        principalTable: "families",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_models_lens_range",
                        column: x => x.lens_range_id,
                        principalSchema: "catalog",
                        principalTable: "lens_references",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_models_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batches",
                schema: "imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    mapping = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    contains_costs = table.Column<bool>(type: "boolean", nullable: false),
                    contains_manual_prices = table.Column<bool>(type: "boolean", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: true),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batches", x => x.id);
                    table.CheckConstraint("ck_batches_kind", "kind IN ('catalog', 'suppliers', 'prices')");
                    table.CheckConstraint("ck_batches_lifecycle", "(status = 'applied') = (applied_at_utc IS NOT NULL) AND (applied_at_utc IS NULL) = (applied_by_user_id IS NULL) AND (checked_at_utc IS NULL) = (checked_by_user_id IS NULL) AND (applied_at_utc IS NULL OR checked_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_batches_mapping", "jsonb_typeof(mapping) = 'object'");
                    table.CheckConstraint("ck_batches_sha256", "file_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_batches_size", "octet_length(content) <= 2097152 AND row_count >= 0 AND row_count <= 5000");
                    table.CheckConstraint("ck_batches_status", "status IN ('uploaded', 'checked', 'applied', 'cancelled')");
                    table.CheckConstraint("ck_batches_store", "(kind = 'prices') = (store_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_batches_applied_by_users",
                        column: x => x.applied_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batches_checked_by_users",
                        column: x => x.checked_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batches_stores",
                        column: x => x.store_id,
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batches_uploaded_by_users",
                        column: x => x.uploaded_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_sessions",
                schema: "stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValueSql: "'INV-' || to_char(nextval('stock.inventory_number_seq'), 'FM999999999999000000')"),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scope_family_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    blind = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    opened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    validated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uncounted_resolution = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    abandoned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    abandoned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    abandon_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_sessions", x => x.id);
                    table.CheckConstraint("ck_inventory_sessions_lifecycle", "(status = 'validated') = (validated_at_utc IS NOT NULL) AND (validated_at_utc IS NULL) = (validated_by_user_id IS NULL) AND (validated_at_utc IS NULL) = (uncounted_resolution IS NULL) AND (status = 'abandoned') = (abandoned_at_utc IS NOT NULL) AND (abandoned_at_utc IS NULL) = (abandoned_by_user_id IS NULL) AND (abandoned_at_utc IS NULL) = (abandon_reason IS NULL)");
                    table.CheckConstraint("ck_inventory_sessions_locations", "cardinality(location_ids) >= 1");
                    table.CheckConstraint("ck_inventory_sessions_number_format", "number ~ '^INV-[0-9]{6,18}$'");
                    table.CheckConstraint("ck_inventory_sessions_scope", "scope_type IN ('full', 'family', 'selection') AND (scope_type = 'family') = (scope_family_code IS NOT NULL)");
                    table.CheckConstraint("ck_inventory_sessions_status", "status IN ('open', 'validated', 'abandoned')");
                    table.CheckConstraint("ck_inventory_sessions_uncounted", "uncounted_resolution IS NULL OR uncounted_resolution IN ('exclude', 'set_to_zero')");
                    table.ForeignKey(
                        name: "fk_inventory_sessions_abandoned_by_users",
                        column: x => x.abandoned_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_sessions_families",
                        column: x => x.scope_family_code,
                        principalSchema: "catalog",
                        principalTable: "families",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_sessions_opened_by_users",
                        column: x => x.opened_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_sessions_stores",
                        column: x => x.store_id,
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_sessions_validated_by_users",
                        column: x => x.validated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                    table.CheckConstraint("ck_locations_code_format", "code ~ '^[A-Z0-9][A-Z0-9_-]{0,31}$'");
                    table.ForeignKey(
                        name: "fk_locations_stores",
                        column: x => x.store_id,
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_categories",
                schema: "pricing",
                columns: table => new
                {
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    legal_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    validated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_categories", x => new { x.store_id, x.code });
                    table.CheckConstraint("ck_tax_categories_code_format", "code ~ '^[a-z][a-z_]{0,31}$'");
                    table.CheckConstraint("ck_tax_categories_rate", "rate IS NULL OR (rate >= 0 AND rate < 1)");
                    table.CheckConstraint("ck_tax_categories_validation", "(rate IS NULL) = (validated_at_utc IS NULL) AND (validated_at_utc IS NULL) = (validated_by_user_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_tax_categories_stores",
                        column: x => x.store_id,
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tax_categories_validated_by_users",
                        column: x => x.validated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sku_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manufacturer_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    barcode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    lens_index_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_policy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tax_category_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archive_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_variants", x => x.id);
                    table.CheckConstraint("ck_product_variants_attributes", "jsonb_typeof(attributes) = 'object'");
                    table.CheckConstraint("ck_product_variants_barcode", "barcode IS NULL OR barcode ~ '^([0-9]{8}|[0-9]{12,14})$'");
                    table.CheckConstraint("ck_product_variants_label", "btrim(label) <> ''");
                    table.CheckConstraint("ck_product_variants_policy", "stock_policy IN ('stocked', 'order_only', 'service')");
                    table.CheckConstraint("ck_product_variants_sku", "btrim(sku) <> '' AND sku_key = lower(btrim(sku))");
                    table.CheckConstraint("ck_product_variants_tax_category_format", "tax_category_code IS NULL OR tax_category_code ~ '^[a-z][a-z_]{0,31}$'");
                    table.ForeignKey(
                        name: "fk_product_variants_archived_by_users",
                        column: x => x.archived_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_variants_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_variants_lens_index",
                        column: x => x.lens_index_id,
                        principalSchema: "catalog",
                        principalTable: "lens_references",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_variants_models",
                        column: x => x.model_id,
                        principalSchema: "catalog",
                        principalTable: "product_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_variants_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rows",
                schema: "imports",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    values_json = table.Column<string>(type: "jsonb", nullable: false),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    errors = table.Column<string>(type: "jsonb", nullable: false),
                    target_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rows", x => new { x.batch_id, x.line_no });
                    table.CheckConstraint("ck_rows_json", "jsonb_typeof(values_json) = 'object' AND jsonb_typeof(errors) = 'array'");
                    table.CheckConstraint("ck_rows_line_no", "line_no >= 1");
                    table.CheckConstraint("ck_rows_outcome", "outcome IN ('create', 'update', 'unchanged', 'error')");
                    table.ForeignKey(
                        name: "fk_rows_batches",
                        column: x => x.batch_id,
                        principalSchema: "imports",
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                schema: "stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValueSql: "'REC-' || to_char(nextval('stock.receipt_number_seq'), 'FM999999999999000000')"),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", nullable: false),
                    supplier_document_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    completes_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
                    table.CheckConstraint("ck_receipts_number_format", "number ~ '^REC-[0-9]{6,18}$'");
                    table.ForeignKey(
                        name: "fk_receipts_completes",
                        column: x => x.completes_receipt_id,
                        principalSchema: "stock",
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipts_locations",
                        column: x => x.location_id,
                        principalSchema: "org",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipts_received_by_users",
                        column: x => x.received_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipts_store_currency",
                        columns: x => new { x.store_id, x.currency_code },
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumns: new[] { "id", "currency_code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipts_suppliers",
                        column: x => x.supplier_id,
                        principalSchema: "catalog",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "balances",
                schema: "stock",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_movement_seq = table.Column<long>(type: "bigint", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_balances", x => new { x.variant_id, x.location_id });
                    table.CheckConstraint("ck_balances_on_hand", "on_hand >= 0");
                    table.CheckConstraint("ck_balances_reserved", "reserved >= 0 AND reserved <= on_hand");
                    table.ForeignKey(
                        name: "fk_balances_locations",
                        column: x => x.location_id,
                        principalSchema: "org",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_balances_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movements",
                schema: "stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    quantity_before = table.Column<int>(type: "integer", nullable: false),
                    quantity_after = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_cost_ht = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    currency_code = table.Column<string>(type: "character(3)", nullable: true),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movements", x => x.id);
                    table.CheckConstraint("ck_movements_delta", "quantity_delta <> 0 AND quantity_after = quantity_before + quantity_delta AND quantity_before >= 0 AND quantity_after >= 0");
                    table.CheckConstraint("ck_movements_reason", "reason_code ~ '^[a-z][a-z_]{0,31}$' AND (reason_code <> 'other' OR comment IS NOT NULL)");
                    table.CheckConstraint("ck_movements_receipt", "(movement_type = 'receipt') = (supplier_id IS NOT NULL) AND (unit_cost_ht IS NULL OR (movement_type = 'receipt' AND unit_cost_ht >= 0)) AND (unit_cost_ht IS NULL) = (currency_code IS NULL)");
                    table.CheckConstraint("ck_movements_sign", "(movement_type NOT IN ('receipt', 'manual_in') OR quantity_delta > 0) AND (movement_type <> 'manual_out' OR quantity_delta < 0)");
                    table.CheckConstraint("ck_movements_source", "(source_type IS NULL) = (source_id IS NULL) AND (source_type IS NULL OR source_type IN ('receipt', 'inventory_session'))");
                    table.CheckConstraint("ck_movements_type", "movement_type IN ('receipt', 'manual_in', 'manual_out', 'adjustment', 'inventory_adjustment')");
                    table.ForeignKey(
                        name: "fk_movements_locations",
                        column: x => x.location_id,
                        principalSchema: "org",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movements_performed_by_users",
                        column: x => x.performed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movements_suppliers",
                        column: x => x.supplier_id,
                        principalSchema: "catalog",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movements_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_history",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    currency_code = table.Column<string>(type: "character(3)", nullable: false),
                    field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_value = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    new_value = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    old_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    new_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_history", x => x.id);
                    table.CheckConstraint("ck_price_history_currency", "currency_code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_price_history_field", "field IN ('purchase_price_ht', 'coefficient', 'pricing_mode', 'sale_price_ht', 'supplier_price_ht')");
                    table.CheckConstraint("ck_price_history_mode", "(field = 'pricing_mode') = (new_mode IS NOT NULL) AND (old_mode IS NULL OR field = 'pricing_mode')");
                    table.CheckConstraint("ck_price_history_origin", "origin IN ('edit', 'recalculation', 'manual_price', 'import', 'receipt')");
                    table.CheckConstraint("ck_price_history_scope", "(field = 'supplier_price_ht') = (supplier_id IS NOT NULL) AND (field = 'supplier_price_ht' OR store_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_price_history_changed_by_users",
                        column: x => x.changed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_history_stores",
                        column: x => x.store_id,
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_history_suppliers",
                        column: x => x.supplier_id,
                        principalSchema: "catalog",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_history_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variant_lens_treatments",
                schema: "catalog",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    treatment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_lens_treatments", x => new { x.variant_id, x.treatment_id });
                    table.ForeignKey(
                        name: "fk_variant_lens_treatments_treatment",
                        column: x => x.treatment_id,
                        principalSchema: "catalog",
                        principalTable: "lens_references",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_lens_treatments_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variant_store_prices",
                schema: "pricing",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", nullable: false),
                    purchase_price_ht = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    coefficient = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    pricing_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sale_price_ht = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_store_prices", x => new { x.variant_id, x.store_id });
                    table.CheckConstraint("ck_variant_store_prices_coefficient", "coefficient IS NULL OR (coefficient > 0 AND coefficient <= 100)");
                    table.CheckConstraint("ck_variant_store_prices_coefficient_mode", "pricing_mode = 'manual' OR sale_price_ht IS NULL OR (purchase_price_ht IS NOT NULL AND coefficient IS NOT NULL)");
                    table.CheckConstraint("ck_variant_store_prices_mode", "pricing_mode IN ('coefficient', 'manual')");
                    table.CheckConstraint("ck_variant_store_prices_purchase", "purchase_price_ht IS NULL OR purchase_price_ht >= 0");
                    table.CheckConstraint("ck_variant_store_prices_sale", "sale_price_ht IS NULL OR sale_price_ht >= 0");
                    table.ForeignKey(
                        name: "fk_variant_store_prices_store_currency",
                        columns: x => new { x.store_id, x.currency_code },
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumns: new[] { "id", "currency_code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_store_prices_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_store_prices_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variant_store_settings",
                schema: "stock",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    low_stock_threshold = table.Column<int>(type: "integer", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_store_settings", x => new { x.variant_id, x.store_id });
                    table.CheckConstraint("ck_variant_store_settings_threshold", "low_stock_threshold IS NULL OR (low_stock_threshold >= 0 AND low_stock_threshold <= 100000)");
                    table.ForeignKey(
                        name: "fk_variant_store_settings_stores",
                        column: x => x.store_id,
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_store_settings_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_store_settings_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variant_suppliers",
                schema: "catalog",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    supplier_price_ht = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    supplier_currency = table.Column<string>(type: "character(3)", nullable: true),
                    lead_time_days = table.Column<int>(type: "integer", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    removed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_suppliers", x => new { x.variant_id, x.supplier_id });
                    table.CheckConstraint("ck_variant_suppliers_currency", "(supplier_price_ht IS NULL) = (supplier_currency IS NULL) AND (supplier_currency IS NULL OR supplier_currency ~ '^[A-Z]{3}$')");
                    table.CheckConstraint("ck_variant_suppliers_lead_time", "lead_time_days IS NULL OR (lead_time_days >= 0 AND lead_time_days <= 365)");
                    table.CheckConstraint("ck_variant_suppliers_price", "supplier_price_ht IS NULL OR supplier_price_ht >= 0");
                    table.CheckConstraint("ck_variant_suppliers_removed", "(removed_at_utc IS NULL) = (removed_by_user_id IS NULL) AND (removed_at_utc IS NULL OR NOT is_primary)");
                    table.ForeignKey(
                        name: "fk_variant_suppliers_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_suppliers_removed_by_users",
                        column: x => x.removed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_suppliers_suppliers",
                        column: x => x.supplier_id,
                        principalSchema: "catalog",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_suppliers_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_suppliers_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_lines",
                schema: "stock",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_at_open = table.Column<int>(type: "integer", nullable: false),
                    expected_at_count = table.Column<int>(type: "integer", nullable: true),
                    counted_quantity = table.Column<int>(type: "integer", nullable: true),
                    counted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resolution = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    applied_delta = table.Column<int>(type: "integer", nullable: true),
                    movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_lines", x => new { x.session_id, x.variant_id, x.location_id });
                    table.CheckConstraint("ck_inventory_lines_applied", "(movement_id IS NOT NULL) = (applied_delta IS NOT NULL AND applied_delta <> 0) AND (movement_id IS NULL OR resolution IN ('applied', 'set_to_zero'))");
                    table.CheckConstraint("ck_inventory_lines_count", "(counted_quantity IS NULL) = (expected_at_count IS NULL) AND (counted_quantity IS NULL) = (counted_at_utc IS NULL) AND (counted_at_utc IS NULL) = (counted_by_user_id IS NULL)");
                    table.CheckConstraint("ck_inventory_lines_quantities", "expected_at_open >= 0 AND (counted_quantity IS NULL OR (counted_quantity >= 0 AND counted_quantity <= 1000000))");
                    table.CheckConstraint("ck_inventory_lines_reason", "(reason_code IS NULL OR reason_code ~ '^[a-z][a-z_]{0,31}$') AND (comment IS NULL OR reason_code IS NOT NULL) AND (reason_code IS DISTINCT FROM 'other' OR comment IS NOT NULL)");
                    table.CheckConstraint("ck_inventory_lines_resolution", "resolution IS NULL OR resolution IN ('applied', 'no_difference', 'excluded', 'set_to_zero')");
                    table.ForeignKey(
                        name: "fk_inventory_lines_counted_by_users",
                        column: x => x.counted_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_lines_locations",
                        column: x => x.location_id,
                        principalSchema: "org",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_lines_movements",
                        column: x => x.movement_id,
                        principalSchema: "stock",
                        principalTable: "movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_lines_sessions",
                        column: x => x.session_id,
                        principalSchema: "stock",
                        principalTable: "inventory_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_lines_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_lines",
                schema: "stock",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    announced_quantity = table.Column<int>(type: "integer", nullable: true),
                    received_quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_cost_ht = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    movement_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_lines", x => new { x.receipt_id, x.line_no });
                    table.CheckConstraint("ck_receipt_lines_cost", "unit_cost_ht IS NULL OR unit_cost_ht >= 0");
                    table.CheckConstraint("ck_receipt_lines_line_no", "line_no >= 1");
                    table.CheckConstraint("ck_receipt_lines_movement", "(received_quantity > 0) = (movement_id IS NOT NULL)");
                    table.CheckConstraint("ck_receipt_lines_quantities", "received_quantity >= 0 AND (announced_quantity IS NULL OR announced_quantity >= 0) AND (received_quantity > 0 OR COALESCE(announced_quantity, 0) > 0)");
                    table.ForeignKey(
                        name: "fk_receipt_lines_movements",
                        column: x => x.movement_id,
                        principalSchema: "stock",
                        principalTable: "movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipt_lines_receipts",
                        column: x => x.receipt_id,
                        principalSchema: "stock",
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipt_lines_variants",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "families",
                columns: new[] { "code", "allowed_policies", "label_fr", "sort_order" },
                values: new object[,]
                {
                    { "accessories", new[] { "stocked", "order_only" }, "Accessoires", 5 },
                    { "contact_lenses_solutions", new[] { "stocked", "order_only" }, "Lentilles et solutions", 4 },
                    { "ophthalmic_lenses", new[] { "stocked", "order_only" }, "Verres correcteurs", 3 },
                    { "optical_frames", new[] { "stocked", "order_only" }, "Montures optiques", 1 },
                    { "services", new[] { "service" }, "Prestations", 6 },
                    { "sunglasses", new[] { "stocked", "order_only" }, "Lunettes de soleil", 2 }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions",
                sql: "permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel', 'audit.read', 'optical.read', 'optical.prescriptions.write', 'optical.measurements.write', 'optical.invalidate', 'catalog.read', 'catalog.write', 'catalog.archive', 'catalog.configure', 'catalog.import', 'pricing.write', 'pricing.manual', 'costs.read', 'costs.write', 'suppliers.read', 'suppliers.write', 'stock.read', 'stock.movements.write', 'stock.adjust', 'inventory.count', 'inventory.validate')");

            migrationBuilder.CreateIndex(
                name: "ix_balances_location_id",
                schema: "stock",
                table: "balances",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_batches_applied_by_user_id",
                schema: "imports",
                table: "batches",
                column: "applied_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_batches_checked_by_user_id",
                schema: "imports",
                table: "batches",
                column: "checked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_batches_store_id",
                schema: "imports",
                table: "batches",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_batches_uploaded_at",
                schema: "imports",
                table: "batches",
                column: "uploaded_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_batches_uploaded_by_user_id",
                schema: "imports",
                table: "batches",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_batches_applied_file",
                schema: "imports",
                table: "batches",
                columns: new[] { "file_sha256", "kind", "store_id" },
                unique: true,
                filter: "status = 'applied'")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_batches_creation_request_id",
                schema: "imports",
                table: "batches",
                column: "creation_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_lines_counted_by_user_id",
                schema: "stock",
                table: "inventory_lines",
                column: "counted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_lines_location_id",
                schema: "stock",
                table: "inventory_lines",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_lines_variant_location",
                schema: "stock",
                table: "inventory_lines",
                columns: new[] { "variant_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "ux_inventory_lines_movement_id",
                schema: "stock",
                table: "inventory_lines",
                column: "movement_id",
                unique: true,
                filter: "movement_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_sessions_abandoned_by_user_id",
                schema: "stock",
                table: "inventory_sessions",
                column: "abandoned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_sessions_opened_by_user_id",
                schema: "stock",
                table: "inventory_sessions",
                column: "opened_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_sessions_scope_family_code",
                schema: "stock",
                table: "inventory_sessions",
                column: "scope_family_code");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_sessions_store_status",
                schema: "stock",
                table: "inventory_sessions",
                columns: new[] { "store_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_sessions_validated_by_user_id",
                schema: "stock",
                table: "inventory_sessions",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_sessions_creation_request_id",
                schema: "stock",
                table: "inventory_sessions",
                column: "creation_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_inventory_sessions_number",
                schema: "stock",
                table: "inventory_sessions",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lens_references_archived_by_user_id",
                schema: "catalog",
                table: "lens_references",
                column: "archived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_references_created_by_user_id",
                schema: "catalog",
                table: "lens_references",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_references_manufacturer_id",
                schema: "catalog",
                table: "lens_references",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_references_updated_by_user_id",
                schema: "catalog",
                table: "lens_references",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_lens_references_kind_parent_name",
                schema: "catalog",
                table: "lens_references",
                columns: new[] { "kind", "manufacturer_id", "name_key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_locations_one_default",
                schema: "org",
                table: "locations",
                column: "store_id",
                unique: true,
                filter: "is_default AND archived_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_locations_store_code",
                schema: "org",
                table: "locations",
                columns: new[] { "store_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movements_location_id",
                schema: "stock",
                table: "movements",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_movements_performed_at",
                schema: "stock",
                table: "movements",
                column: "performed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_movements_performed_by_user_id",
                schema: "stock",
                table: "movements",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_movements_source",
                schema: "stock",
                table: "movements",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movements_supplier_id",
                schema: "stock",
                table: "movements",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_movements_variant_location_seq",
                schema: "stock",
                table: "movements",
                columns: new[] { "variant_id", "location_id", "seq" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_movements_request_variant_location",
                schema: "stock",
                table: "movements",
                columns: new[] { "request_id", "variant_id", "location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_movements_seq",
                schema: "stock",
                table: "movements",
                column: "seq",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_history_changed_by_user_id",
                schema: "pricing",
                table: "price_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_history_store_id",
                schema: "pricing",
                table: "price_history",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_history_supplier_id",
                schema: "pricing",
                table: "price_history",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_history_variant_store_changed",
                schema: "pricing",
                table: "price_history",
                columns: new[] { "variant_id", "store_id", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_product_models_archived_by_user_id",
                schema: "catalog",
                table: "product_models",
                column: "archived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_models_created_by_user_id",
                schema: "catalog",
                table: "product_models",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_models_family_name",
                schema: "catalog",
                table: "product_models",
                columns: new[] { "family_code", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_models_lens_range_id",
                schema: "catalog",
                table: "product_models",
                column: "lens_range_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_models_updated_by_user_id",
                schema: "catalog",
                table: "product_models",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_models_creation_request_id",
                schema: "catalog",
                table: "product_models",
                column: "creation_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_archived_by_user_id",
                schema: "catalog",
                table: "product_variants",
                column: "archived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_created_by_user_id",
                schema: "catalog",
                table: "product_variants",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_lens_index_id",
                schema: "catalog",
                table: "product_variants",
                column: "lens_index_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_model_id",
                schema: "catalog",
                table: "product_variants",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_updated_by_user_id",
                schema: "catalog",
                table: "product_variants",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_barcode",
                schema: "catalog",
                table: "product_variants",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_creation_request_id",
                schema: "catalog",
                table: "product_variants",
                column: "creation_request_id",
                unique: true,
                filter: "creation_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_sku",
                schema: "catalog",
                table: "product_variants",
                column: "sku_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipt_lines_variant_id",
                schema: "stock",
                table: "receipt_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ux_receipt_lines_movement_id",
                schema: "stock",
                table: "receipt_lines",
                column: "movement_id",
                unique: true,
                filter: "movement_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_completes_receipt_id",
                schema: "stock",
                table: "receipts",
                column: "completes_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_location_id",
                schema: "stock",
                table: "receipts",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_received_by_user_id",
                schema: "stock",
                table: "receipts",
                column: "received_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_store_currency",
                schema: "stock",
                table: "receipts",
                columns: new[] { "store_id", "currency_code" });

            migrationBuilder.CreateIndex(
                name: "ix_receipts_store_received",
                schema: "stock",
                table: "receipts",
                columns: new[] { "store_id", "received_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_receipts_supplier_id",
                schema: "stock",
                table: "receipts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ux_receipts_number",
                schema: "stock",
                table: "receipts",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_receipts_request_id",
                schema: "stock",
                table: "receipts",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stores_code",
                schema: "org",
                table: "stores",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_archived_by_user_id",
                schema: "catalog",
                table: "suppliers",
                column: "archived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_created_by_user_id",
                schema: "catalog",
                table: "suppliers",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_name",
                schema: "catalog",
                table: "suppliers",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_updated_by_user_id",
                schema: "catalog",
                table: "suppliers",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_suppliers_code",
                schema: "catalog",
                table: "suppliers",
                column: "code_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_suppliers_creation_request_id",
                schema: "catalog",
                table: "suppliers",
                column: "creation_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_categories_validated_by_user_id",
                schema: "pricing",
                table: "tax_categories",
                column: "validated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_lens_treatments_treatment_id",
                schema: "catalog",
                table: "variant_lens_treatments",
                column: "treatment_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_store_prices_store_currency",
                schema: "pricing",
                table: "variant_store_prices",
                columns: new[] { "store_id", "currency_code" });

            migrationBuilder.CreateIndex(
                name: "ix_variant_store_prices_updated_by_user_id",
                schema: "pricing",
                table: "variant_store_prices",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_store_settings_store_id",
                schema: "stock",
                table: "variant_store_settings",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_store_settings_updated_by_user_id",
                schema: "stock",
                table: "variant_store_settings",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_suppliers_created_by_user_id",
                schema: "catalog",
                table: "variant_suppliers",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_suppliers_removed_by_user_id",
                schema: "catalog",
                table: "variant_suppliers",
                column: "removed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_suppliers_supplier_id",
                schema: "catalog",
                table: "variant_suppliers",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_suppliers_updated_by_user_id",
                schema: "catalog",
                table: "variant_suppliers",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_variant_suppliers_one_primary",
                schema: "catalog",
                table: "variant_suppliers",
                column: "variant_id",
                unique: true,
                filter: "is_primary");

            // ---------- Garanties écrites à la main (hors modèle EF) ----------
            // Aucune suppression physique par l'application (D-05-07) ; journaux en ajout seul (mouvements, réceptions,
            // historique des prix) ; devise d'un magasin immuable ; session d'inventaire close et lot d'import clos figés.
            // Code d'erreur 42501 (insufficient_privilege), message préfixé par le schéma.
            migrationBuilder.Sql("""
                CREATE FUNCTION catalog.reject_delete() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION '%.% : suppression refusee (%)', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION catalog.reject_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION '%.% : journal en ajout seul, modification refusee (%)', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION org.reject_store_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' AND NEW.currency_code = OLD.currency_code AND NEW.id = OLD.id THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'org.stores : devise immuable, suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION org.reject_location_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' AND NEW.store_id = OLD.store_id AND NEW.id = OLD.id THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'org.locations : magasin immuable, suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION stock.reject_closed_session_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' AND OLD.status = 'open' THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'stock.inventory_sessions : session close figee ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION stock.reject_resolved_line_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF EXISTS (SELECT 1 FROM stock.inventory_sessions s WHERE s.id = NEW.session_id AND s.status = 'open') THEN
                            RETURN NEW;
                        END IF;
                    ELSIF TG_OP = 'UPDATE' AND OLD.resolution IS NULL
                        AND NEW.session_id = OLD.session_id AND NEW.variant_id = OLD.variant_id AND NEW.location_id = OLD.location_id
                        AND NOT EXISTS (SELECT 1 FROM stock.inventory_sessions s WHERE s.id = OLD.session_id AND s.status = 'abandoned') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'stock.inventory_lines : ligne figee ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION imports.reject_closed_batch_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' AND OLD.status IN ('uploaded', 'checked') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'imports.batches : lot clos fige ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION imports.reject_closed_row_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP IN ('INSERT', 'DELETE') AND EXISTS (
                        SELECT 1 FROM imports.batches b
                        WHERE b.id = CASE WHEN TG_OP = 'INSERT' THEN NEW.batch_id ELSE OLD.batch_id END AND b.status IN ('uploaded', 'checked')) THEN
                        RETURN CASE WHEN TG_OP = 'INSERT' THEN NEW ELSE OLD END;
                    END IF;
                    RAISE EXCEPTION 'imports.rows : rapport d''un lot clos fige (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);

            migrationBuilder.Sql("CREATE TRIGGER trg_stores_guard BEFORE UPDATE OR DELETE ON org.stores FOR EACH ROW EXECUTE FUNCTION org.reject_store_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_locations_guard BEFORE UPDATE OR DELETE ON org.locations FOR EACH ROW EXECUTE FUNCTION org.reject_location_change();");

            // Aucune suppression (lignes et tables entières).
            foreach (var table in new[]
            {
                "org.stores", "org.locations", "catalog.families", "catalog.lens_references", "catalog.product_models", "catalog.product_variants",
                "catalog.suppliers", "catalog.variant_suppliers", "pricing.variant_store_prices", "pricing.tax_categories",
                "stock.variant_store_settings", "stock.balances",
            })
            {
                var name = table[(table.IndexOf('.', StringComparison.Ordinal) + 1)..];
                if (table is not ("org.stores" or "org.locations"))
                {
                    migrationBuilder.Sql($"CREATE TRIGGER trg_{name}_no_delete BEFORE DELETE ON {table} FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();");
                }

                migrationBuilder.Sql($"CREATE TRIGGER trg_{name}_no_truncate BEFORE TRUNCATE ON {table} FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            }

            // Journaux en ajout seul.
            foreach (var table in new[] { "pricing.price_history", "stock.movements", "stock.receipts", "stock.receipt_lines" })
            {
                var name = table[(table.IndexOf('.', StringComparison.Ordinal) + 1)..];
                migrationBuilder.Sql($"CREATE TRIGGER trg_{name}_append_only BEFORE UPDATE OR DELETE ON {table} FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();");
                migrationBuilder.Sql($"CREATE TRIGGER trg_{name}_no_truncate BEFORE TRUNCATE ON {table} FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();");
            }

            migrationBuilder.Sql("CREATE TRIGGER trg_inventory_sessions_frozen BEFORE UPDATE OR DELETE ON stock.inventory_sessions FOR EACH ROW EXECUTE FUNCTION stock.reject_closed_session_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_inventory_sessions_no_truncate BEFORE TRUNCATE ON stock.inventory_sessions FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_inventory_lines_frozen BEFORE INSERT OR UPDATE OR DELETE ON stock.inventory_lines FOR EACH ROW EXECUTE FUNCTION stock.reject_resolved_line_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_inventory_lines_no_truncate BEFORE TRUNCATE ON stock.inventory_lines FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batches_frozen BEFORE UPDATE OR DELETE ON imports.batches FOR EACH ROW EXECUTE FUNCTION imports.reject_closed_batch_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batches_no_truncate BEFORE TRUNCATE ON imports.batches FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_rows_frozen BEFORE INSERT OR UPDATE OR DELETE ON imports.rows FOR EACH ROW EXECUTE FUNCTION imports.reject_closed_row_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_rows_no_truncate BEFORE TRUNCATE ON imports.rows FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
        }

        /// <summary>
        /// Aucun retour arrière automatique : supprimer les schémas de la Phase 05 DÉTRUIRAIT le catalogue, les prix, les
        /// mouvements et les inventaires. Retour arrière = restauration de la sauvegarde prise avant l'application,
        /// décidée par le propriétaire (procédure G05-SQL).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "0007_CatalogStockModule : aucun retour arrière automatique. Restaurer la sauvegarde prise avant l'application (procédure G05-SQL).");
    }
}
