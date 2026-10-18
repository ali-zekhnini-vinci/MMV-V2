using Microsoft.EntityFrameworkCore;
using MMV.Domain.Audit;
using MMV.Domain.Clients;
using MMV.Domain.Identity;

namespace MMV.Infrastructure.Audit;

/// <summary>
/// Schéma <c>audit</c> (phase 03-D2, migration 0005_AuditJournal) : journal d'audit métier centralisé, une table
/// pour tous les modules. Ajout seul : le déclencheur <c>trg_events_append_only</c> (créé par la migration, hors
/// modèle EF) refuse toute modification, suppression ou troncature. Aucune cascade : un compte ou un client cité ne
/// peut pas disparaître. Champs modifiés en <c>jsonb</c> (tableau), sans valeur personnelle (politique
/// <see cref="AuditFieldPolicy"/>).
/// </summary>
internal static class AuditModel
{
    public const string Schema = "audit";

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entry =>
        {
            entry.ToTable("events", Schema, t =>
            {
                t.HasCheckConstraint("ck_events_module_format", $"module ~ '{AuditEvent.ModuleRegex}'");
                t.HasCheckConstraint("ck_events_entity_type_format", $"entity_type ~ '{AuditEvent.ModuleRegex}'");
                t.HasCheckConstraint("ck_events_action_format", $"action ~ '{AuditEvent.ActionRegex}'");
                t.HasCheckConstraint("ck_events_changes_array", "changes IS NULL OR jsonb_typeof(changes) = 'array'");
            });
            entry.HasKey(e => e.Id).HasName("pk_events");
            entry.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entry.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
            entry.Property(e => e.ActorUserId).HasColumnName("actor_user_id").IsRequired();
            entry.Property(e => e.ClientId).HasColumnName("client_id");
            entry.Property(e => e.Module).HasColumnName("module").HasMaxLength(AuditEvent.ModuleMaxLength).IsRequired();
            entry.Property(e => e.Action).HasColumnName("action").HasMaxLength(AuditEvent.CodeMaxLength).IsRequired();
            entry.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(AuditEvent.EntityTypeMaxLength).IsRequired();
            entry.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
            entry.Property(e => e.Changes).HasColumnName("changes").HasColumnType("jsonb");
            entry.Property(e => e.SessionId).HasColumnName("session_id");
            entry.Property(e => e.TraceId).HasColumnName("trace_id").HasMaxLength(AuditEvent.TraceIdMaxLength);

            // Historique d'un dossier, du plus récent au plus ancien ; événements d'une entité ; clé étrangère de l'auteur.
            entry.HasIndex(e => new { e.ClientId, e.OccurredAtUtc, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_events_client_occurred");
            entry.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("ix_events_entity");
            entry.HasIndex(e => e.ActorUserId).HasDatabaseName("ix_events_actor_user_id");

            entry.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.ActorUserId)
                .HasConstraintName("fk_events_actor_users").OnDelete(DeleteBehavior.Restrict);
            entry.HasOne<Client>().WithMany().HasForeignKey(e => e.ClientId)
                .HasConstraintName("fk_events_clients").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
