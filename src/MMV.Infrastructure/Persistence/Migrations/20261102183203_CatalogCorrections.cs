using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
// Code généré par dotnet ef (tableaux de colonnes d'index composites) : CA1861 sans objet ici.
#pragma warning disable CA1861

namespace MMV.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 0008 — corrections de la Phase 05 (COR-05-04 import CSV, COR-05-05 référentiel verres), STRICTEMENT additive :
    /// aucune table, colonne ou contrainte existante modifiée ni supprimée.
    /// Nouvelles tables : <c>imports.supplier_profiles</c>, <c>catalog.lens_offers</c>, <c>catalog.lens_offer_treatments</c>,
    /// <c>catalog.lens_offer_revisions</c> ; nouvelle colonne nullable <c>imports.batches.supplier_id</c>. 0007 inchangée.
    /// </summary>
    /// <inheritdoc />
    public partial class CatalogCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                schema: "imports",
                table: "batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lens_offers",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    range_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lens_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    material = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    index_id = table.Column<Guid>(type: "uuid", nullable: true),
                    production = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    lead_time_days = table.Column<int>(type: "integer", nullable: true),
                    price_ht = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    price_currency = table.Column<string>(type: "character(3)", nullable: true),
                    diameters = table.Column<string>(type: "jsonb", nullable: false),
                    sphere_min = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    sphere_max = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    cylinder_min = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    cylinder_max = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    addition_min = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    addition_max = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    rules = table.Column<string>(type: "jsonb", nullable: false),
                    rules_complete = table.Column<bool>(type: "boolean", nullable: false),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_lens_offers", x => x.id);
                    table.CheckConstraint("ck_lens_offers_json", "jsonb_typeof(diameters) = 'array' AND jsonb_typeof(rules) = 'array'");
                    table.CheckConstraint("ck_lens_offers_lead_time", "lead_time_days IS NULL OR (lead_time_days >= 0 AND lead_time_days <= 365)");
                    table.CheckConstraint("ck_lens_offers_price", "(price_ht IS NULL) = (price_currency IS NULL) AND (price_ht IS NULL OR price_ht >= 0) AND (price_currency IS NULL OR price_currency ~ '^[A-Z]{3}$')");
                    table.CheckConstraint("ck_lens_offers_production", "production IN ('stocked', 'on_order')");
                    table.CheckConstraint("ck_lens_offers_ranges", "(sphere_min IS NULL) = (sphere_max IS NULL) AND (sphere_min IS NULL OR sphere_min <= sphere_max) AND (cylinder_min IS NULL) = (cylinder_max IS NULL) AND (cylinder_min IS NULL OR cylinder_min <= cylinder_max) AND (addition_min IS NULL) = (addition_max IS NULL) AND (addition_min IS NULL OR (addition_min >= 0 AND addition_min <= addition_max))");
                    table.CheckConstraint("ck_lens_offers_revision", "revision >= 1");
                    table.CheckConstraint("ck_lens_offers_type", "lens_type IN ('single_vision', 'progressive', 'degressive', 'bifocal', 'other')");
                    table.CheckConstraint("ck_lens_offers_validity", "valid_from IS NULL OR valid_until IS NULL OR valid_from <= valid_until");
                    table.ForeignKey(
                        name: "fk_lens_offers_archived_by_users",
                        column: x => x.archived_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_offers_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_offers_index",
                        column: x => x.index_id,
                        principalSchema: "catalog",
                        principalTable: "lens_references",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_offers_range",
                        column: x => x.range_id,
                        principalSchema: "catalog",
                        principalTable: "lens_references",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_offers_suppliers",
                        column: x => x.supplier_id,
                        principalSchema: "catalog",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_offers_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_profiles",
                schema: "imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    mapping = table.Column<string>(type: "jsonb", nullable: false),
                    headers = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_profiles", x => x.id);
                    table.CheckConstraint("ck_supplier_profiles_json", "jsonb_typeof(mapping) = 'object' AND jsonb_typeof(headers) = 'array'");
                    table.CheckConstraint("ck_supplier_profiles_kind", "kind IN ('catalog', 'prices')");
                    table.ForeignKey(
                        name: "fk_supplier_profiles_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_profiles_suppliers",
                        column: x => x.supplier_id,
                        principalSchema: "catalog",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_profiles_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lens_offer_revisions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lens_offer_revisions", x => x.id);
                    table.CheckConstraint("ck_lens_offer_revisions_revision", "revision >= 1");
                    table.CheckConstraint("ck_lens_offer_revisions_snapshot", "jsonb_typeof(snapshot) = 'object'");
                    table.ForeignKey(
                        name: "fk_lens_offer_revisions_changed_by_users",
                        column: x => x.changed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lens_offer_revisions_offers",
                        column: x => x.offer_id,
                        principalSchema: "catalog",
                        principalTable: "lens_offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lens_offer_treatments",
                schema: "catalog",
                columns: table => new
                {
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    treatment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lens_offer_treatments", x => new { x.offer_id, x.treatment_id });
                    table.ForeignKey(
                        name: "fk_lens_offer_treatments_offers",
                        column: x => x.offer_id,
                        principalSchema: "catalog",
                        principalTable: "lens_offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lens_offer_treatments_treatment",
                        column: x => x.treatment_id,
                        principalSchema: "catalog",
                        principalTable: "lens_references",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_batches_supplier_id",
                schema: "imports",
                table: "batches",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_offer_revisions_changed_by_user_id",
                schema: "catalog",
                table: "lens_offer_revisions",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_lens_offer_revisions_offer_revision",
                schema: "catalog",
                table: "lens_offer_revisions",
                columns: new[] { "offer_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lens_offer_treatments_treatment_id",
                schema: "catalog",
                table: "lens_offer_treatments",
                column: "treatment_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_offers_archived_by_user_id",
                schema: "catalog",
                table: "lens_offers",
                column: "archived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_offers_created_by_user_id",
                schema: "catalog",
                table: "lens_offers",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_offers_index_id",
                schema: "catalog",
                table: "lens_offers",
                column: "index_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_offers_range_id",
                schema: "catalog",
                table: "lens_offers",
                column: "range_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_offers_supplier_id",
                schema: "catalog",
                table: "lens_offers",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_lens_offers_updated_by_user_id",
                schema: "catalog",
                table: "lens_offers",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_lens_offers_creation_request_id",
                schema: "catalog",
                table: "lens_offers",
                column: "creation_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_profiles_created_by_user_id",
                schema: "imports",
                table: "supplier_profiles",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_profiles_updated_by_user_id",
                schema: "imports",
                table: "supplier_profiles",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_supplier_profiles_supplier_kind",
                schema: "imports",
                table: "supplier_profiles",
                columns: new[] { "supplier_id", "kind" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_batches_suppliers",
                schema: "imports",
                table: "batches",
                column: "supplier_id",
                principalSchema: "catalog",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ---------- Garanties écrites à la main (hors modèle EF), mêmes fonctions que 0007 ----------
            // Offres de verres et modèles d'import : jamais de suppression (archivage / mise à jour) ; révisions des
            // offres : historique en AJOUT SEUL. Les traitements d'une offre (table de liaison) restent remplaçables.
            migrationBuilder.Sql("CREATE TRIGGER trg_lens_offers_no_delete BEFORE DELETE ON catalog.lens_offers FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_lens_offers_no_truncate BEFORE TRUNCATE ON catalog.lens_offers FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_lens_offer_revisions_append_only BEFORE UPDATE OR DELETE ON catalog.lens_offer_revisions FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_lens_offer_revisions_no_truncate BEFORE TRUNCATE ON catalog.lens_offer_revisions FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_supplier_profiles_no_delete BEFORE DELETE ON imports.supplier_profiles FOR EACH ROW EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_supplier_profiles_no_truncate BEFORE TRUNCATE ON imports.supplier_profiles FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_supplier_profiles_no_truncate ON imports.supplier_profiles;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_supplier_profiles_no_delete ON imports.supplier_profiles;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_lens_offer_revisions_no_truncate ON catalog.lens_offer_revisions;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_lens_offer_revisions_append_only ON catalog.lens_offer_revisions;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_lens_offers_no_truncate ON catalog.lens_offers;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_lens_offers_no_delete ON catalog.lens_offers;");

            migrationBuilder.DropForeignKey(
                name: "fk_batches_suppliers",
                schema: "imports",
                table: "batches");

            migrationBuilder.DropTable(
                name: "lens_offer_revisions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "lens_offer_treatments",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "supplier_profiles",
                schema: "imports");

            migrationBuilder.DropTable(
                name: "lens_offers",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "ix_batches_supplier_id",
                schema: "imports",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                schema: "imports",
                table: "batches");
        }
    }
}
