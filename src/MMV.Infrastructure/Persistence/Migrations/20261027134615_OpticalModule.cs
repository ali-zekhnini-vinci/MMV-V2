using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
// Code généré par dotnet ef (tableaux de colonnes d'index composites) : CA1861 sans objet ici.
#pragma warning disable CA1861

namespace MMV.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 0006 — dossier optique (phase 04-B, mandat GO du 27/10/2026) : schéma <c>optical</c>, ordonnances médicales et
    /// mesures magasin dans DEUX familles de tables distinctes (en-tête + révisions), séquences de références, quatre
    /// permissions admises par la contrainte des permissions. Déclencheurs ajoutés à la main ci-dessous (SQLSTATE 42501) :
    /// révision confirmée ou abandonnée immuable, enregistrement invalidé figé, aucune suppression ni troncature. Aucune
    /// donnée existante n'est lue, modifiée ni reprise ; aucune pièce source (D-04-16). Pas de retour arrière automatique :
    /// <see cref="Down"/> refuse de s'exécuter ; le retour arrière est la restauration de la sauvegarde prise avant
    /// l'application (procédure G04-SQL).
    /// </summary>
    public partial class OpticalModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions");

            migrationBuilder.EnsureSchema(
                name: "optical");

            migrationBuilder.CreateSequence(
                name: "measurement_reference_seq",
                schema: "optical",
                maxValue: 999999999999999999L);

            migrationBuilder.CreateSequence(
                name: "prescription_reference_seq",
                schema: "optical",
                maxValue: 999999999999999999L);

            migrationBuilder.CreateTable(
                name: "measurements",
                schema: "optical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValueSql: "'MES-' || to_char(nextval('optical.measurement_reference_seq'), 'FM999999999999000000')"),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invalidated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invalidation_reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    invalidation_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measurements", x => x.id);
                    table.CheckConstraint("ck_measurements_invalidated_after_created", "invalidated_at_utc IS NULL OR invalidated_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_measurements_invalidation_complete", "(invalidated_at_utc IS NULL) = (invalidated_by_user_id IS NULL) AND (invalidated_at_utc IS NULL) = (invalidation_reason_code IS NULL) AND (invalidation_comment IS NULL OR invalidated_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_measurements_invalidation_reason_format", "invalidation_reason_code IS NULL OR invalidation_reason_code ~ '^[a-z][a-z_]{0,31}$'");
                    table.CheckConstraint("ck_measurements_reference_format", "reference ~ '^MES-[0-9]{6,18}$'");
                    table.CheckConstraint("ck_measurements_updated_after_created", "updated_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "fk_measurements_clients",
                        column: x => x.client_id,
                        principalSchema: "clients",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurements_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurements_invalidated_by_users",
                        column: x => x.invalidated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurements_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescriptions",
                schema: "optical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValueSql: "'RX-' || to_char(nextval('optical.prescription_reference_seq'), 'FM999999999999000000')"),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invalidated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invalidation_reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    invalidation_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescriptions", x => x.id);
                    table.CheckConstraint("ck_prescriptions_invalidated_after_created", "invalidated_at_utc IS NULL OR invalidated_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_prescriptions_invalidation_complete", "(invalidated_at_utc IS NULL) = (invalidated_by_user_id IS NULL) AND (invalidated_at_utc IS NULL) = (invalidation_reason_code IS NULL) AND (invalidation_comment IS NULL OR invalidated_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_prescriptions_invalidation_reason_format", "invalidation_reason_code IS NULL OR invalidation_reason_code ~ '^[a-z][a-z_]{0,31}$'");
                    table.CheckConstraint("ck_prescriptions_reference_format", "reference ~ '^RX-[0-9]{6,18}$'");
                    table.CheckConstraint("ck_prescriptions_updated_after_created", "updated_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "fk_prescriptions_clients",
                        column: x => x.client_id,
                        principalSchema: "clients",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescriptions_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescriptions_invalidated_by_users",
                        column: x => x.invalidated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescriptions_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "measurement_revisions",
                schema: "optical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    measured_on = table.Column<DateOnly>(type: "date", nullable: true),
                    measured_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    od_addition_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_addition, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_axis_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_axis, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_cylinder_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_cylinder, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_prism_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_prism, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_sphere_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_sphere, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_addition_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_addition, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_axis_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_axis, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_cylinder_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_cylinder, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_prism_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_prism, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_sphere_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_sphere, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: true),
                    base_revision_no = table.Column<int>(type: "integer", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reason_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    od_sphere = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_cylinder = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_axis = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_addition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_prism = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_base = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_acuity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_sphere = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_cylinder = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_axis = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_addition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_prism = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_base = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_acuity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discarded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    discarded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measurement_revisions", x => x.id);
                    table.CheckConstraint("ck_measurement_revisions_confirmation", "(status = 'confirmed') = (revision_no IS NOT NULL) AND (revision_no IS NULL) = (confirmed_at_utc IS NULL) AND (confirmed_at_utc IS NULL) = (confirmed_by_user_id IS NULL)");
                    table.CheckConstraint("ck_measurement_revisions_confirmed_provenance", "status <> 'confirmed' OR (measured_on IS NOT NULL AND measured_by_user_id IS NOT NULL)");
                    table.CheckConstraint("ck_measurement_revisions_dates", "(measured_on IS NULL OR measured_on >= DATE '1900-01-01')");
                    table.CheckConstraint("ck_measurement_revisions_discard", "(status = 'discarded') = (discarded_at_utc IS NOT NULL) AND (discarded_at_utc IS NULL) = (discarded_by_user_id IS NULL) AND (status <> 'discarded' OR base_revision_no IS NOT NULL)");
                    table.CheckConstraint("ck_measurement_revisions_numbers", "(revision_no IS NULL OR revision_no >= 1) AND (base_revision_no IS NULL OR base_revision_no >= 1) AND (revision_no IS NULL OR base_revision_no IS NULL OR revision_no > base_revision_no)");
                    table.CheckConstraint("ck_measurement_revisions_od_addition_number", "od_addition IS NULL OR od_addition ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_od_axis_number", "od_axis IS NULL OR od_axis ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_od_cylinder_number", "od_cylinder IS NULL OR od_cylinder ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_od_prism_number", "od_prism IS NULL OR od_prism ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_od_sphere_number", "od_sphere IS NULL OR od_sphere ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_og_addition_number", "og_addition IS NULL OR og_addition ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_og_axis_number", "og_axis IS NULL OR og_axis ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_og_cylinder_number", "og_cylinder IS NULL OR og_cylinder ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_og_prism_number", "og_prism IS NULL OR og_prism ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_og_sphere_number", "og_sphere IS NULL OR og_sphere ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_measurement_revisions_reason", "(base_revision_no IS NULL) = (reason_code IS NULL) AND (reason_comment IS NULL OR reason_code IS NOT NULL) AND (reason_code IS NULL OR reason_code ~ '^[a-z][a-z_]{0,31}$')");
                    table.CheckConstraint("ck_measurement_revisions_status", "status IN ('draft', 'confirmed', 'discarded')");
                    table.CheckConstraint("ck_measurement_revisions_updated_after_started", "updated_at_utc >= started_at_utc");
                    table.ForeignKey(
                        name: "fk_measurement_revisions_confirmed_by_users",
                        column: x => x.confirmed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_revisions_discarded_by_users",
                        column: x => x.discarded_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_revisions_measured_by_users",
                        column: x => x.measured_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_revisions_measurements",
                        column: x => x.measurement_id,
                        principalSchema: "optical",
                        principalTable: "measurements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_revisions_started_by_users",
                        column: x => x.started_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_measurement_revisions_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescription_revisions",
                schema: "optical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescriber_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    prescriber_qualification = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    prescriber_identifier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    prescribed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: true),
                    od_addition_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_addition, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_axis_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_axis, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_cylinder_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_cylinder, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_prism_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_prism, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    od_sphere_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(od_sphere, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_addition_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_addition, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_axis_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_axis, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_cylinder_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_cylinder, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_prism_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_prism, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    og_sphere_value = table.Column<decimal>(type: "numeric", nullable: true, computedColumnSql: "(replace(replace(replace(og_sphere, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric", stored: true),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: true),
                    base_revision_no = table.Column<int>(type: "integer", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reason_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    od_sphere = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_cylinder = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_axis = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_addition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_prism = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_base = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    od_acuity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_sphere = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_cylinder = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_axis = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_addition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_prism = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_base = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    og_acuity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discarded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    discarded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescription_revisions", x => x.id);
                    table.CheckConstraint("ck_prescription_revisions_confirmation", "(status = 'confirmed') = (revision_no IS NOT NULL) AND (revision_no IS NULL) = (confirmed_at_utc IS NULL) AND (confirmed_at_utc IS NULL) = (confirmed_by_user_id IS NULL)");
                    table.CheckConstraint("ck_prescription_revisions_confirmed_provenance", "status <> 'confirmed' OR (prescribed_on IS NOT NULL AND prescriber_name IS NOT NULL)");
                    table.CheckConstraint("ck_prescription_revisions_dates", "(prescribed_on IS NULL OR prescribed_on >= DATE '1900-01-01') AND (received_on IS NULL OR received_on >= DATE '1900-01-01')");
                    table.CheckConstraint("ck_prescription_revisions_discard", "(status = 'discarded') = (discarded_at_utc IS NOT NULL) AND (discarded_at_utc IS NULL) = (discarded_by_user_id IS NULL) AND (status <> 'discarded' OR base_revision_no IS NOT NULL)");
                    table.CheckConstraint("ck_prescription_revisions_numbers", "(revision_no IS NULL OR revision_no >= 1) AND (base_revision_no IS NULL OR base_revision_no >= 1) AND (revision_no IS NULL OR base_revision_no IS NULL OR revision_no > base_revision_no)");
                    table.CheckConstraint("ck_prescription_revisions_od_addition_number", "od_addition IS NULL OR od_addition ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_od_axis_number", "od_axis IS NULL OR od_axis ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_od_cylinder_number", "od_cylinder IS NULL OR od_cylinder ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_od_prism_number", "od_prism IS NULL OR od_prism ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_od_sphere_number", "od_sphere IS NULL OR od_sphere ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_og_addition_number", "og_addition IS NULL OR og_addition ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_og_axis_number", "og_axis IS NULL OR og_axis ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_og_cylinder_number", "og_cylinder IS NULL OR og_cylinder ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_og_prism_number", "og_prism IS NULL OR og_prism ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_og_sphere_number", "og_sphere IS NULL OR og_sphere ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')");
                    table.CheckConstraint("ck_prescription_revisions_reason", "(base_revision_no IS NULL) = (reason_code IS NULL) AND (reason_comment IS NULL OR reason_code IS NOT NULL) AND (reason_code IS NULL OR reason_code ~ '^[a-z][a-z_]{0,31}$')");
                    table.CheckConstraint("ck_prescription_revisions_status", "status IN ('draft', 'confirmed', 'discarded')");
                    table.CheckConstraint("ck_prescription_revisions_updated_after_started", "updated_at_utc >= started_at_utc");
                    table.ForeignKey(
                        name: "fk_prescription_revisions_confirmed_by_users",
                        column: x => x.confirmed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescription_revisions_discarded_by_users",
                        column: x => x.discarded_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescription_revisions_prescriptions",
                        column: x => x.prescription_id,
                        principalSchema: "optical",
                        principalTable: "prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescription_revisions_started_by_users",
                        column: x => x.started_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescription_revisions_updated_by_users",
                        column: x => x.updated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions",
                sql: "permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel', 'audit.read', 'optical.read', 'optical.prescriptions.write', 'optical.measurements.write', 'optical.invalidate')");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_revisions_confirmed_by_user_id",
                schema: "optical",
                table: "measurement_revisions",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_revisions_discarded_by_user_id",
                schema: "optical",
                table: "measurement_revisions",
                column: "discarded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_revisions_measured_by_user_id",
                schema: "optical",
                table: "measurement_revisions",
                column: "measured_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_revisions_measurement_started",
                schema: "optical",
                table: "measurement_revisions",
                columns: new[] { "measurement_id", "started_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_measurement_revisions_started_by_user_id",
                schema: "optical",
                table: "measurement_revisions",
                column: "started_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_revisions_updated_by_user_id",
                schema: "optical",
                table: "measurement_revisions",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_measurement_revisions_number",
                schema: "optical",
                table: "measurement_revisions",
                columns: new[] { "measurement_id", "revision_no" },
                unique: true,
                filter: "revision_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_measurement_revisions_open_draft",
                schema: "optical",
                table: "measurement_revisions",
                column: "measurement_id",
                unique: true,
                filter: "status = 'draft'");

            migrationBuilder.CreateIndex(
                name: "ix_measurements_client_created",
                schema: "optical",
                table: "measurements",
                columns: new[] { "client_id", "created_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_measurements_created_by_user_id",
                schema: "optical",
                table: "measurements",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurements_invalidated_by_user_id",
                schema: "optical",
                table: "measurements",
                column: "invalidated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurements_updated_by_user_id",
                schema: "optical",
                table: "measurements",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_measurements_creation_request_id",
                schema: "optical",
                table: "measurements",
                column: "creation_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_measurements_reference",
                schema: "optical",
                table: "measurements",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prescription_revisions_confirmed_by_user_id",
                schema: "optical",
                table: "prescription_revisions",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_revisions_discarded_by_user_id",
                schema: "optical",
                table: "prescription_revisions",
                column: "discarded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_revisions_prescription_started",
                schema: "optical",
                table: "prescription_revisions",
                columns: new[] { "prescription_id", "started_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_prescription_revisions_started_by_user_id",
                schema: "optical",
                table: "prescription_revisions",
                column: "started_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_revisions_updated_by_user_id",
                schema: "optical",
                table: "prescription_revisions",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_prescription_revisions_number",
                schema: "optical",
                table: "prescription_revisions",
                columns: new[] { "prescription_id", "revision_no" },
                unique: true,
                filter: "revision_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_prescription_revisions_open_draft",
                schema: "optical",
                table: "prescription_revisions",
                column: "prescription_id",
                unique: true,
                filter: "status = 'draft'");

            migrationBuilder.CreateIndex(
                name: "ix_prescriptions_client_created",
                schema: "optical",
                table: "prescriptions",
                columns: new[] { "client_id", "created_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_prescriptions_created_by_user_id",
                schema: "optical",
                table: "prescriptions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescriptions_invalidated_by_user_id",
                schema: "optical",
                table: "prescriptions",
                column: "invalidated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescriptions_updated_by_user_id",
                schema: "optical",
                table: "prescriptions",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_prescriptions_creation_request_id",
                schema: "optical",
                table: "prescriptions",
                column: "creation_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prescriptions_reference",
                schema: "optical",
                table: "prescriptions",
                column: "reference",
                unique: true);

            // Immuabilité (écrite à la main, hors modèle EF) : seule une révision en brouillon se modifie ; un enregistrement
            // invalidé ne se modifie plus ; rien ne se supprime ni ne se tronque par l'application.
            migrationBuilder.Sql("""
                CREATE FUNCTION optical.reject_frozen_revision_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD.status = 'draft' THEN
                            RETURN NEW;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'optical : revision figee ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION optical.reject_frozen_record_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD.invalidated_at_utc IS NULL THEN
                            RETURN NEW;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'optical : enregistrement invalide fige ou suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            foreach (var (records, revisions) in new[] { ("prescriptions", "prescription_revisions"), ("measurements", "measurement_revisions") })
            {
                migrationBuilder.Sql(
                    $"CREATE TRIGGER trg_{records}_frozen BEFORE UPDATE OR DELETE ON optical.{records} FOR EACH ROW EXECUTE FUNCTION optical.reject_frozen_record_change();");
                migrationBuilder.Sql(
                    $"CREATE TRIGGER trg_{records}_no_truncate BEFORE TRUNCATE ON optical.{records} FOR EACH STATEMENT EXECUTE FUNCTION optical.reject_frozen_record_change();");
                migrationBuilder.Sql(
                    $"CREATE TRIGGER trg_{revisions}_frozen BEFORE UPDATE OR DELETE ON optical.{revisions} FOR EACH ROW EXECUTE FUNCTION optical.reject_frozen_revision_change();");
                migrationBuilder.Sql(
                    $"CREATE TRIGGER trg_{revisions}_no_truncate BEFORE TRUNCATE ON optical.{revisions} FOR EACH STATEMENT EXECUTE FUNCTION optical.reject_frozen_revision_change();");
            }
        }

        /// <summary>
        /// Aucun retour arrière automatique (mandat 04-B) : supprimer le schéma optique DÉTRUIRAIT des données de santé.
        /// Retour arrière = restauration de la sauvegarde de la base prise avant l'application, décidée par le propriétaire.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "0006_OpticalModule : aucun retour arrière automatique. Restaurer la sauvegarde prise avant l'application (procédure G04-SQL).");
    }
}
