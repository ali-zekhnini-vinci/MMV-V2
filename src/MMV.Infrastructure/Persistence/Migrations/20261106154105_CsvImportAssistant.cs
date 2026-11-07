using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

// Code généré par dotnet ef (tableaux de colonnes d'index composites) : CA1861 sans objet ici.
#pragma warning disable CA1861

#nullable disable

namespace MMV.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 0009 — CSV-IMPORT-AI-01 (assistant d'import) : migration ADDITIVE du schéma <c>imports</c> (5 tables, index, gardes).
    /// Aucune table, colonne, contrainte ni index existant modifié ; <c>imports.batches</c> inchangé (empreinte du texte et
    /// refus du réimport intacts). Application aux bases du propriétaire SEULEMENT après GO SQL distinct (dossier
    /// docs/phase/05/sql/0009_*), par le script SQL revu — jamais par migration automatique.
    /// </summary>
    public partial class CsvImportAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "batch_analyses",
                schema: "imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    based_on_revision = table.Column<int>(type: "integer", nullable: false),
                    contract = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    configured_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model_digest = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ollama_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    prompt_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    sample_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    queued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    eval_count = table.Column<int>(type: "integer", nullable: true),
                    coverage = table.Column<string>(type: "jsonb", nullable: false),
                    raw_output = table.Column<string>(type: "text", nullable: true),
                    proposal = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    late_result_discarded = table.Column<bool>(type: "boolean", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_analyses", x => x.id);
                    table.CheckConstraint("ck_batch_analyses_attempt", "attempt >= 1 AND based_on_revision >= 1");
                    table.CheckConstraint("ck_batch_analyses_bounds", "(raw_output IS NULL OR char_length(raw_output) <= 65536) AND (duration_ms IS NULL OR duration_ms >= 0) AND (eval_count IS NULL OR eval_count >= 0)");
                    table.CheckConstraint("ck_batch_analyses_contract", "contract = 'mmv.csvai.v2'");
                    table.CheckConstraint("ck_batch_analyses_hashes", "(prompt_sha256 IS NULL OR prompt_sha256 ~ '^[0-9a-f]{64}$') AND (sample_sha256 IS NULL OR sample_sha256 ~ '^[0-9a-f]{64}$')");
                    table.CheckConstraint("ck_batch_analyses_json", "jsonb_typeof(coverage) = 'object' AND (proposal IS NULL OR jsonb_typeof(proposal) = 'object')");
                    table.CheckConstraint("ck_batch_analyses_late", "NOT late_result_discarded OR status IN ('cancelled', 'interrupted')");
                    table.CheckConstraint("ck_batch_analyses_lifecycle", "(status <> 'queued' OR (started_at_utc IS NULL AND ended_at_utc IS NULL)) AND (status <> 'running' OR (started_at_utc IS NOT NULL AND ended_at_utc IS NULL)) AND (status IN ('queued', 'running') OR ended_at_utc IS NOT NULL) AND (status NOT IN ('done', 'incomplete') OR (model IS NOT NULL AND proposal IS NOT NULL AND started_at_utc IS NOT NULL))");
                    table.CheckConstraint("ck_batch_analyses_status", "status IN ('queued', 'running', 'done', 'incomplete', 'unavailable', 'timeout', 'invalid_output', 'cancelled', 'interrupted')");
                    table.ForeignKey(
                        name: "fk_batch_analyses_batches",
                        column: x => x.batch_id,
                        principalSchema: "imports",
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batch_analyses_requested_by_users",
                        column: x => x.requested_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batch_assistant",
                schema: "imports",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pipeline = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    checked_revision = table.Column<int>(type: "integer", nullable: true),
                    rules_only_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rules_only_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rules_only_reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    check_summary = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_assistant", x => x.batch_id);
                    table.CheckConstraint("ck_batch_assistant_check_summary", "check_summary IS NULL OR (jsonb_typeof(check_summary) = 'object' AND checked_revision IS NOT NULL)");
                    table.CheckConstraint("ck_batch_assistant_pipeline", "pipeline = 'assistant_v2'");
                    table.CheckConstraint("ck_batch_assistant_revision", "revision >= 1 AND (checked_revision IS NULL OR (checked_revision >= 1 AND checked_revision <= revision))");
                    table.CheckConstraint("ck_batch_assistant_rules_only", "(rules_only_at_utc IS NULL) = (rules_only_reason IS NULL) AND (rules_only_reason IS NULL OR rules_only_reason IN ('user', 'configuration', 'not_needed')) AND (rules_only_by_user_id IS NULL OR rules_only_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_batch_assistant_target", "target IN ('products', 'lenses')");
                    table.ForeignKey(
                        name: "fk_batch_assistant_batches",
                        column: x => x.batch_id,
                        principalSchema: "imports",
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batch_assistant_rules_only_by_users",
                        column: x => x.rules_only_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batch_assistant_stores",
                        column: x => x.store_id,
                        principalSchema: "org",
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batch_decisions",
                schema: "imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    target = table.Column<string>(type: "jsonb", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    examples_digest = table.Column<string>(type: "character(64)", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_decisions", x => x.id);
                    table.CheckConstraint("ck_batch_decisions_digest", "examples_digest IS NULL OR examples_digest ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_batch_decisions_json", "jsonb_typeof(target) = 'object' AND jsonb_typeof(value) = 'object' AND octet_length(target::text) <= 4096 AND octet_length(value::text) <= 4096");
                    table.CheckConstraint("ck_batch_decisions_revision", "revision >= 2");
                    table.CheckConstraint("ck_batch_decisions_type", "type IN ('column_field', 'header_presence', 'family_class', 'continuation', 'cell_value', 'row_skip', 'row_restore', 'decimal_separator', 'file_default', 'create_reference', 'rules_only', 'revoke')");
                    table.ForeignKey(
                        name: "fk_batch_decisions_author_users",
                        column: x => x.author_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batch_decisions_batches",
                        column: x => x.batch_id,
                        principalSchema: "imports",
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batch_decisions_supersedes",
                        column: x => x.supersedes_id,
                        principalSchema: "imports",
                        principalTable: "batch_decisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batch_originals",
                schema: "imports",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    byte_length = table.Column<int>(type: "integer", nullable: false),
                    encoding = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    delimiter = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    bytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    stored_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    purged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_originals", x => x.batch_id);
                    table.CheckConstraint("ck_batch_originals_bytes", "bytes IS NULL OR octet_length(bytes) = byte_length");
                    table.CheckConstraint("ck_batch_originals_delimiter", "delimiter IN (';', ',', 'tab', '|')");
                    table.CheckConstraint("ck_batch_originals_encoding", "encoding IN ('utf-8', 'utf-8-bom', 'utf-16le', 'utf-16be', 'windows-1252')");
                    table.CheckConstraint("ck_batch_originals_purge", "(bytes IS NULL) = (purged_at_utc IS NOT NULL) AND (purged_at_utc IS NULL) = (purged_by_user_id IS NULL)");
                    table.CheckConstraint("ck_batch_originals_sha256", "original_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_batch_originals_size", "byte_length >= 1 AND byte_length <= 2097152");
                    table.ForeignKey(
                        name: "fk_batch_originals_batches",
                        column: x => x.batch_id,
                        principalSchema: "imports",
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batch_originals_purged_by_users",
                        column: x => x.purged_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batch_analysis_events",
                schema: "imports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_analysis_events", x => x.id);
                    table.CheckConstraint("ck_batch_analysis_events_code", "code IN ('file_received', 'encoding_detected', 'structure_read', 'rules_validated', 'sample_prepared', 'ollama_queued', 'ollama_running', 'ollama_received', 'validation_running', 'analysis_done', 'ollama_unavailable', 'ollama_timeout', 'ollama_invalid_output', 'analysis_cancelled', 'analysis_interrupted', 'late_result_discarded', 'rules_only_chosen', 'analysis_disabled')");
                    table.CheckConstraint("ck_batch_analysis_events_data", "jsonb_typeof(data) = 'object' AND octet_length(data::text) <= 4096");
                    table.ForeignKey(
                        name: "fk_batch_analysis_events_analyses",
                        column: x => x.analysis_id,
                        principalSchema: "imports",
                        principalTable: "batch_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_batch_analysis_events_batches",
                        column: x => x.batch_id,
                        principalSchema: "imports",
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_batch_analyses_requested_by_user_id",
                schema: "imports",
                table: "batch_analyses",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_analyses_status_queued_at",
                schema: "imports",
                table: "batch_analyses",
                columns: new[] { "status", "queued_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_batch_analyses_active",
                schema: "imports",
                table: "batch_analyses",
                column: "batch_id",
                unique: true,
                filter: "status IN ('queued', 'running')");

            migrationBuilder.CreateIndex(
                name: "ux_batch_analyses_batch_attempt",
                schema: "imports",
                table: "batch_analyses",
                columns: new[] { "batch_id", "attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_batch_analysis_events_analysis_id",
                schema: "imports",
                table: "batch_analysis_events",
                columns: new[] { "analysis_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_batch_analysis_events_batch_id",
                schema: "imports",
                table: "batch_analysis_events",
                columns: new[] { "batch_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_batch_assistant_rules_only_by_user_id",
                schema: "imports",
                table: "batch_assistant",
                column: "rules_only_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_assistant_store_id",
                schema: "imports",
                table: "batch_assistant",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_decisions_author_user_id",
                schema: "imports",
                table: "batch_decisions",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_decisions_supersedes_id",
                schema: "imports",
                table: "batch_decisions",
                column: "supersedes_id");

            migrationBuilder.CreateIndex(
                name: "ux_batch_decisions_batch_request",
                schema: "imports",
                table: "batch_decisions",
                columns: new[] { "batch_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_batch_decisions_batch_revision",
                schema: "imports",
                table: "batch_decisions",
                columns: new[] { "batch_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_batch_originals_purged_by_user_id",
                schema: "imports",
                table: "batch_originals",
                column: "purged_by_user_id");

            // ---------- Gardes écrites à la main (hors modèle EF), conventions 0007 / 0008 ----------
            // Original jamais réécrit (seule la purge TRACÉE des octets est permise, jamais automatique) ; tentatives
            // queued → running → terminal seulement, terminal figé sauf l'enregistrement UNIQUE d'un résultat tardif ;
            // événements et décisions en ajout seul ; aucune décision sur un lot clos ; aucune suppression. Code 42501.
            migrationBuilder.Sql("""
                CREATE FUNCTION imports.guard_batch_original() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' AND OLD.bytes IS NOT NULL AND NEW.bytes IS NULL AND NEW.purged_at_utc IS NOT NULL AND NEW.purged_by_user_id IS NOT NULL
                       AND NEW.batch_id = OLD.batch_id AND NEW.original_sha256 = OLD.original_sha256 AND NEW.byte_length = OLD.byte_length
                       AND NEW.encoding = OLD.encoding AND NEW.delimiter = OLD.delimiter AND NEW.stored_at_utc = OLD.stored_at_utc THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'imports.batch_originals : original immuable (purge tracee des octets seulement), suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION imports.guard_batch_assistant() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP = 'UPDATE' AND NEW.batch_id = OLD.batch_id AND NEW.pipeline = OLD.pipeline AND NEW.target = OLD.target
                       AND NEW.store_id IS NOT DISTINCT FROM OLD.store_id AND NEW.revision >= OLD.revision
                       AND (OLD.rules_only_at_utc IS NULL OR (NEW.rules_only_at_utc = OLD.rules_only_at_utc AND NEW.rules_only_reason = OLD.rules_only_reason))
                       AND EXISTS (SELECT 1 FROM imports.batches b WHERE b.id = NEW.batch_id AND b.status IN ('uploaded', 'checked')) THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'imports.batch_assistant : etat fige (lot clos, champ immuable ou revision decroissante), suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION imports.guard_batch_analysis() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP <> 'UPDATE' THEN
                        RAISE EXCEPTION 'imports.batch_analyses : suppression refusee (%)', TG_OP USING ERRCODE = '42501';
                    END IF;
                    IF NEW.id <> OLD.id OR NEW.batch_id <> OLD.batch_id OR NEW.attempt <> OLD.attempt OR NEW.based_on_revision <> OLD.based_on_revision
                       OR NEW.contract <> OLD.contract OR NEW.configured_model <> OLD.configured_model OR NEW.queued_at_utc <> OLD.queued_at_utc
                       OR NEW.requested_by_user_id <> OLD.requested_by_user_id THEN
                        RAISE EXCEPTION 'imports.batch_analyses : identite de la tentative immuable' USING ERRCODE = '42501';
                    END IF;
                    IF OLD.status = 'queued' AND NEW.status IN ('queued', 'running', 'cancelled', 'interrupted', 'unavailable') THEN
                        RETURN NEW;
                    END IF;
                    IF OLD.status = 'running' AND NEW.status IN ('running', 'done', 'incomplete', 'unavailable', 'timeout', 'invalid_output', 'cancelled', 'interrupted') THEN
                        RETURN NEW;
                    END IF;
                    IF OLD.status IN ('cancelled', 'interrupted') AND NEW.status = OLD.status AND NOT OLD.late_result_discarded AND NEW.late_result_discarded
                       AND NEW.ended_at_utc = OLD.ended_at_utc AND NEW.proposal IS NOT DISTINCT FROM OLD.proposal THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'imports.batch_analyses : tentative terminee figee ou transition interdite (% -> %)', OLD.status, NEW.status USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION imports.reject_closed_batch_decision() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF EXISTS (SELECT 1 FROM imports.batches b WHERE b.id = NEW.batch_id AND b.status IN ('uploaded', 'checked')) THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'imports.batch_decisions : lot clos, decision refusee' USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_originals_guard BEFORE UPDATE OR DELETE ON imports.batch_originals FOR EACH ROW EXECUTE FUNCTION imports.guard_batch_original();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_originals_no_truncate BEFORE TRUNCATE ON imports.batch_originals FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_assistant_guard BEFORE UPDATE OR DELETE ON imports.batch_assistant FOR EACH ROW EXECUTE FUNCTION imports.guard_batch_assistant();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_assistant_no_truncate BEFORE TRUNCATE ON imports.batch_assistant FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_analyses_guard BEFORE UPDATE OR DELETE ON imports.batch_analyses FOR EACH ROW EXECUTE FUNCTION imports.guard_batch_analysis();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_analyses_no_truncate BEFORE TRUNCATE ON imports.batch_analyses FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_delete();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_analysis_events_append_only BEFORE UPDATE OR DELETE ON imports.batch_analysis_events FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_analysis_events_no_truncate BEFORE TRUNCATE ON imports.batch_analysis_events FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_decisions_append_only BEFORE UPDATE OR DELETE ON imports.batch_decisions FOR EACH ROW EXECUTE FUNCTION catalog.reject_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_decisions_no_truncate BEFORE TRUNCATE ON imports.batch_decisions FOR EACH STATEMENT EXECUTE FUNCTION catalog.reject_change();");
            migrationBuilder.Sql("CREATE TRIGGER trg_batch_decisions_open_batch BEFORE INSERT ON imports.batch_decisions FOR EACH ROW EXECUTE FUNCTION imports.reject_closed_batch_decision();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retour arrière : réservé à une base de travail ; sur une base du propriétaire, restauration de la sauvegarde prise
            // avant l'application (procédure G05-SQL-0009). Seules les tables de 0009 et leurs données sont supprimées.
            foreach (var (trigger, table) in new[]
            {
                ("trg_batch_decisions_open_batch", "batch_decisions"), ("trg_batch_decisions_no_truncate", "batch_decisions"), ("trg_batch_decisions_append_only", "batch_decisions"),
                ("trg_batch_analysis_events_no_truncate", "batch_analysis_events"), ("trg_batch_analysis_events_append_only", "batch_analysis_events"),
                ("trg_batch_analyses_no_truncate", "batch_analyses"), ("trg_batch_analyses_guard", "batch_analyses"),
                ("trg_batch_assistant_no_truncate", "batch_assistant"), ("trg_batch_assistant_guard", "batch_assistant"),
                ("trg_batch_originals_no_truncate", "batch_originals"), ("trg_batch_originals_guard", "batch_originals"),
            })
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger} ON imports.{table};");
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS imports.reject_closed_batch_decision();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS imports.guard_batch_analysis();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS imports.guard_batch_assistant();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS imports.guard_batch_original();");

            migrationBuilder.DropTable(
                name: "batch_analysis_events",
                schema: "imports");

            migrationBuilder.DropTable(
                name: "batch_assistant",
                schema: "imports");

            migrationBuilder.DropTable(
                name: "batch_decisions",
                schema: "imports");

            migrationBuilder.DropTable(
                name: "batch_originals",
                schema: "imports");

            migrationBuilder.DropTable(
                name: "batch_analyses",
                schema: "imports");
        }
    }
}
