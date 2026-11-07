using Microsoft.EntityFrameworkCore;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;
using MMV.Domain.Stores;

namespace MMV.Infrastructure.Catalog;

/// <summary>
/// Schéma de l'assistant d'import (CSV-IMPORT-AI-01, migration 0009, schéma <c>imports</c>) — ADDITIF : aucune colonne,
/// contrainte ni index existant modifié ; <c>imports.batches</c> (dont <c>file_sha256</c> et <c>ux_batches_applied_file</c>)
/// inchangé. Gardes en base (déclencheurs écrits dans la migration) : original jamais réécrit hors purge tracée, tentatives
/// <c>queued → running → terminal</c> seulement, événements et décisions en ajout seul, aucune suppression.
/// </summary>
internal static class AssistantModel
{
    private const string Schema = CatalogStockModel.ImportsSchema;

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BatchOriginal>(original =>
        {
            original.ToTable("batch_originals", Schema, t =>
            {
                t.HasCheckConstraint("ck_batch_originals_sha256", "original_sha256 ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_batch_originals_size", $"byte_length >= 1 AND byte_length <= {ImportBatch.MaxBytes}");
                t.HasCheckConstraint("ck_batch_originals_encoding", "encoding IN ('utf-8', 'utf-8-bom', 'utf-16le', 'utf-16be', 'windows-1252')");
                t.HasCheckConstraint("ck_batch_originals_delimiter", "delimiter IN (';', ',', 'tab', '|')");
                t.HasCheckConstraint("ck_batch_originals_bytes", "bytes IS NULL OR octet_length(bytes) = byte_length");
                t.HasCheckConstraint("ck_batch_originals_purge",
                    "(bytes IS NULL) = (purged_at_utc IS NOT NULL) AND (purged_at_utc IS NULL) = (purged_by_user_id IS NULL)");
            });
            original.HasKey(o => o.BatchId).HasName("pk_batch_originals");
            original.Property(o => o.OriginalSha256).HasColumnType("character(64)").IsRequired();
            original.Property(o => o.Encoding).HasMaxLength(BatchOriginal.EncodingMaxLength).IsRequired();
            original.Property(o => o.Delimiter).HasMaxLength(BatchOriginal.DelimiterMaxLength).IsRequired();
            original.Property(o => o.Bytes).HasColumnType("bytea");
            original.HasOne<ImportBatch>().WithOne().HasForeignKey<BatchOriginal>(o => o.BatchId).HasConstraintName("fk_batch_originals_batches").OnDelete(DeleteBehavior.Restrict);
            original.HasOne<UserAccount>().WithMany().HasForeignKey(o => o.PurgedByUserId).HasConstraintName("fk_batch_originals_purged_by_users").OnDelete(DeleteBehavior.Restrict);
            original.HasIndex(o => o.PurgedByUserId).HasDatabaseName("ix_batch_originals_purged_by_user_id");
        });

        modelBuilder.Entity<BatchAssistant>(state =>
        {
            state.ToTable("batch_assistant", Schema, t =>
            {
                t.HasCheckConstraint("ck_batch_assistant_pipeline", "pipeline = 'assistant_v2'");
                t.HasCheckConstraint("ck_batch_assistant_target", "target IN ('products', 'lenses')");
                t.HasCheckConstraint("ck_batch_assistant_revision", "revision >= 1 AND (checked_revision IS NULL OR (checked_revision >= 1 AND checked_revision <= revision))");
                t.HasCheckConstraint("ck_batch_assistant_rules_only",
                    "(rules_only_at_utc IS NULL) = (rules_only_reason IS NULL) AND (rules_only_reason IS NULL OR rules_only_reason IN ('user', 'configuration', 'not_needed'))"
                    + " AND (rules_only_by_user_id IS NULL OR rules_only_at_utc IS NOT NULL)");
                t.HasCheckConstraint("ck_batch_assistant_check_summary", "check_summary IS NULL OR (jsonb_typeof(check_summary) = 'object' AND checked_revision IS NOT NULL)");
            });
            state.HasKey(s => s.BatchId).HasName("pk_batch_assistant");
            state.Property(s => s.Pipeline).HasMaxLength(16).IsRequired();
            state.Property(s => s.Target).HasMaxLength(16).IsRequired();
            state.Property(s => s.RulesOnlyReason).HasMaxLength(16);
            state.Property(s => s.CheckSummaryJson).HasColumnName("check_summary").HasColumnType("jsonb");
            state.Property(s => s.Version).IsRowVersion();
            state.Ignore(s => s.RulesOnly);
            state.HasOne<ImportBatch>().WithOne().HasForeignKey<BatchAssistant>(s => s.BatchId).HasConstraintName("fk_batch_assistant_batches").OnDelete(DeleteBehavior.Restrict);
            state.HasOne<UserAccount>().WithMany().HasForeignKey(s => s.RulesOnlyByUserId).HasConstraintName("fk_batch_assistant_rules_only_by_users").OnDelete(DeleteBehavior.Restrict);
            state.HasOne<Store>().WithMany().HasForeignKey(s => s.StoreId).HasConstraintName("fk_batch_assistant_stores").OnDelete(DeleteBehavior.Restrict);
            state.HasIndex(s => s.RulesOnlyByUserId).HasDatabaseName("ix_batch_assistant_rules_only_by_user_id");
            state.HasIndex(s => s.StoreId).HasDatabaseName("ix_batch_assistant_store_id");
        });

        modelBuilder.Entity<BatchAnalysis>(analysis =>
        {
            analysis.ToTable("batch_analyses", Schema, t =>
            {
                t.HasCheckConstraint("ck_batch_analyses_status",
                    "status IN ('queued', 'running', 'done', 'incomplete', 'unavailable', 'timeout', 'invalid_output', 'cancelled', 'interrupted')");
                t.HasCheckConstraint("ck_batch_analyses_contract", "contract = 'mmv.csvai.v2'");
                t.HasCheckConstraint("ck_batch_analyses_attempt", "attempt >= 1 AND based_on_revision >= 1");
                t.HasCheckConstraint("ck_batch_analyses_lifecycle",
                    "(status <> 'queued' OR (started_at_utc IS NULL AND ended_at_utc IS NULL))"
                    + " AND (status <> 'running' OR (started_at_utc IS NOT NULL AND ended_at_utc IS NULL))"
                    + " AND (status IN ('queued', 'running') OR ended_at_utc IS NOT NULL)"
                    + " AND (status NOT IN ('done', 'incomplete') OR (model IS NOT NULL AND proposal IS NOT NULL AND started_at_utc IS NOT NULL))");
                t.HasCheckConstraint("ck_batch_analyses_hashes",
                    "(prompt_sha256 IS NULL OR prompt_sha256 ~ '^[0-9a-f]{64}$') AND (sample_sha256 IS NULL OR sample_sha256 ~ '^[0-9a-f]{64}$')");
                t.HasCheckConstraint("ck_batch_analyses_bounds",
                    $"(raw_output IS NULL OR char_length(raw_output) <= {BatchAnalysis.RawOutputMaxLength}) AND (duration_ms IS NULL OR duration_ms >= 0) AND (eval_count IS NULL OR eval_count >= 0)");
                t.HasCheckConstraint("ck_batch_analyses_json", "jsonb_typeof(coverage) = 'object' AND (proposal IS NULL OR jsonb_typeof(proposal) = 'object')");
                t.HasCheckConstraint("ck_batch_analyses_late", "NOT late_result_discarded OR status IN ('cancelled', 'interrupted')");
            });
            analysis.HasKey(a => a.Id).HasName("pk_batch_analyses");
            analysis.Property(a => a.Id).ValueGeneratedNever();
            analysis.Property(a => a.Contract).HasMaxLength(32).IsRequired();
            analysis.Property(a => a.ConfiguredModel).HasMaxLength(BatchAnalysis.ModelMaxLength).IsRequired();
            analysis.Property(a => a.Model).HasMaxLength(BatchAnalysis.ModelMaxLength);
            analysis.Property(a => a.ModelDigest).HasMaxLength(100);
            analysis.Property(a => a.OllamaVersion).HasMaxLength(32);
            analysis.Property(a => a.PromptSha256).HasColumnType("character(64)");
            analysis.Property(a => a.SampleSha256).HasColumnType("character(64)");
            analysis.Property(a => a.Status).HasMaxLength(16).IsRequired();
            analysis.Property(a => a.CoverageJson).HasColumnName("coverage").HasColumnType("jsonb").IsRequired();
            analysis.Property(a => a.ProposalJson).HasColumnName("proposal").HasColumnType("jsonb");
            analysis.Property(a => a.Error).HasMaxLength(BatchAnalysis.ErrorMaxLength);
            analysis.Property(a => a.Version).IsRowVersion();
            analysis.HasOne<ImportBatch>().WithMany().HasForeignKey(a => a.BatchId).HasConstraintName("fk_batch_analyses_batches").OnDelete(DeleteBehavior.Restrict);
            analysis.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.RequestedByUserId).HasConstraintName("fk_batch_analyses_requested_by_users").OnDelete(DeleteBehavior.Restrict);
            analysis.HasIndex(a => new { a.BatchId, a.Attempt }).IsUnique().HasDatabaseName("ux_batch_analyses_batch_attempt");
            // Une seule tentative active par lot (deux relances concurrentes : la seconde est refusée par la base).
            analysis.HasIndex(a => a.BatchId).IsUnique().HasFilter("status IN ('queued', 'running')").HasDatabaseName("ux_batch_analyses_active");
            analysis.HasIndex(a => new { a.Status, a.QueuedAtUtc }).HasDatabaseName("ix_batch_analyses_status_queued_at");
            analysis.HasIndex(a => a.RequestedByUserId).HasDatabaseName("ix_batch_analyses_requested_by_user_id");
        });

        modelBuilder.Entity<BatchAnalysisEvent>(analysisEvent =>
        {
            analysisEvent.ToTable("batch_analysis_events", Schema, t =>
            {
                t.HasCheckConstraint("ck_batch_analysis_events_code", "code IN (" + string.Join(", ", AnalysisEventCodes.All.Select(c => $"'{c}'")) + ")");
                t.HasCheckConstraint("ck_batch_analysis_events_data", $"jsonb_typeof(data) = 'object' AND octet_length(data::text) <= {BatchAnalysisEvent.DataMaxLength}");
            });
            analysisEvent.HasKey(e => e.Id).HasName("pk_batch_analysis_events");
            analysisEvent.Property(e => e.Id).UseIdentityAlwaysColumn();
            analysisEvent.Property(e => e.Code).HasMaxLength(32).IsRequired();
            analysisEvent.Property(e => e.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
            analysisEvent.HasOne<BatchAnalysis>().WithMany().HasForeignKey(e => e.AnalysisId).HasConstraintName("fk_batch_analysis_events_analyses").OnDelete(DeleteBehavior.Restrict);
            analysisEvent.HasOne<ImportBatch>().WithMany().HasForeignKey(e => e.BatchId).HasConstraintName("fk_batch_analysis_events_batches").OnDelete(DeleteBehavior.Restrict);
            analysisEvent.HasIndex(e => new { e.AnalysisId, e.Id }).HasDatabaseName("ix_batch_analysis_events_analysis_id");
            analysisEvent.HasIndex(e => new { e.BatchId, e.Id }).HasDatabaseName("ix_batch_analysis_events_batch_id");
        });

        modelBuilder.Entity<BatchDecision>(decision =>
        {
            decision.ToTable("batch_decisions", Schema, t =>
            {
                t.HasCheckConstraint("ck_batch_decisions_type", "type IN (" + string.Join(", ", DecisionTypes.All.Select(c => $"'{c}'")) + ")");
                t.HasCheckConstraint("ck_batch_decisions_revision", "revision >= 2");
                t.HasCheckConstraint("ck_batch_decisions_json",
                    $"jsonb_typeof(target) = 'object' AND jsonb_typeof(value) = 'object' AND octet_length(target::text) <= {BatchDecision.JsonMaxLength} AND octet_length(value::text) <= {BatchDecision.JsonMaxLength}");
                t.HasCheckConstraint("ck_batch_decisions_digest", "examples_digest IS NULL OR examples_digest ~ '^[0-9a-f]{64}$'");
            });
            decision.HasKey(d => d.Id).HasName("pk_batch_decisions");
            decision.Property(d => d.Id).ValueGeneratedNever();
            decision.Property(d => d.Type).HasMaxLength(24).IsRequired();
            decision.Property(d => d.TargetJson).HasColumnName("target").HasColumnType("jsonb").IsRequired();
            decision.Property(d => d.ValueJson).HasColumnName("value").HasColumnType("jsonb").IsRequired();
            decision.Property(d => d.ExamplesDigest).HasColumnType("character(64)");
            decision.HasOne<ImportBatch>().WithMany().HasForeignKey(d => d.BatchId).HasConstraintName("fk_batch_decisions_batches").OnDelete(DeleteBehavior.Restrict);
            decision.HasOne<BatchDecision>().WithMany().HasForeignKey(d => d.SupersedesId).HasConstraintName("fk_batch_decisions_supersedes").OnDelete(DeleteBehavior.Restrict);
            decision.HasOne<UserAccount>().WithMany().HasForeignKey(d => d.AuthorUserId).HasConstraintName("fk_batch_decisions_author_users").OnDelete(DeleteBehavior.Restrict);
            // Une révision par lot (deux décisions concurrentes sur la même révision de base : la seconde est refusée).
            decision.HasIndex(d => new { d.BatchId, d.Revision }).IsUnique().HasDatabaseName("ux_batch_decisions_batch_revision");
            decision.HasIndex(d => new { d.BatchId, d.RequestId }).IsUnique().HasDatabaseName("ux_batch_decisions_batch_request");
            decision.HasIndex(d => d.SupersedesId).HasDatabaseName("ix_batch_decisions_supersedes_id");
            decision.HasIndex(d => d.AuthorUserId).HasDatabaseName("ix_batch_decisions_author_user_id");
        });
    }
}
