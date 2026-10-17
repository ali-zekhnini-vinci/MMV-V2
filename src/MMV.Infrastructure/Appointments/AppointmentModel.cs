using Microsoft.EntityFrameworkCore;
using MMV.Domain.Appointments;
using MMV.Domain.Clients;
using MMV.Domain.Identity;

namespace MMV.Infrastructure.Appointments;

/// <summary>
/// Schéma <c>appointments</c> (phase 03-B2, migration 0004_AppointmentsModule) : rendez-vous et éligibilité des
/// collaborateurs. Aucune extension PostgreSQL (pas de <c>btree_gist</c>) : le non-chevauchement par collaborateur
/// et la capacité d'accueil (D-03-15) sont garantis dans la transaction d'écriture sous verrous consultatifs
/// <c>pg_advisory_xact_lock</c>. Concurrence optimiste par la colonne système <c>xmin</c>. Motifs : codes de la
/// liste fermée D-03-18, format seul contrôlé ici (la liste est dans MMV.Domain).
/// </summary>
internal static class AppointmentModel
{
    public const string Schema = "appointments";

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(appointment =>
        {
            appointment.ToTable("appointments", Schema, t =>
            {
                t.HasCheckConstraint("ck_appointments_ends_after_starts", "ends_at_utc > starts_at_utc");
                t.HasCheckConstraint("ck_appointments_duration_range", "ends_at_utc - starts_at_utc <= interval '12 hours'");
                t.HasCheckConstraint("ck_appointments_status", "status IN ('planned', 'cancelled')");
                t.HasCheckConstraint(
                    "ck_appointments_cancellation_consistency",
                    "(status = 'cancelled') = (cancelled_at_utc IS NOT NULL AND cancelled_by_user_id IS NOT NULL)");
                t.HasCheckConstraint("ck_appointments_cancellation_reason_planned", "status = 'cancelled' OR cancellation_reason IS NULL");
                t.HasCheckConstraint("ck_appointments_reason_code_format", "reason_code ~ '^[a-z0-9_-]{1,32}$'");
                t.HasCheckConstraint("ck_appointments_updated_after_created", "updated_at_utc >= created_at_utc");
            });
            appointment.HasKey(a => a.Id).HasName("pk_appointments");
            appointment.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            appointment.Property(a => a.ClientId).HasColumnName("client_id").IsRequired();
            // Nullable : rendez-vous d'accueil non attribué (D-03-14) ; l'obligation par motif est contrôlée par l'application.
            appointment.Property(a => a.ResponsibleUserId).HasColumnName("responsible_user_id");
            appointment.Property(a => a.StartsAtUtc).HasColumnName("starts_at_utc").IsRequired();
            appointment.Property(a => a.EndsAtUtc).HasColumnName("ends_at_utc").IsRequired();
            appointment.Property(a => a.ReasonCode).HasColumnName("reason_code").HasMaxLength(Appointment.ReasonCodeMaxLength).IsRequired();
            appointment.Property(a => a.Notes).HasColumnName("notes").HasMaxLength(Appointment.NotesMaxLength);
            appointment.Property(a => a.Status).HasColumnName("status").HasMaxLength(Appointment.StatusMaxLength).IsRequired();
            appointment.Property(a => a.CancelledAtUtc).HasColumnName("cancelled_at_utc");
            appointment.Property(a => a.CancelledByUserId).HasColumnName("cancelled_by_user_id");
            appointment.Property(a => a.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(Appointment.CancellationReasonMaxLength);
            appointment.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            appointment.Property(a => a.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            appointment.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            appointment.Property(a => a.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
            // Npgsql : jeton de concurrence sur la colonne système xmin (aucune colonne créée par la migration).
            appointment.Property(a => a.Version).IsRowVersion();
            appointment.Ignore(a => a.IsCancelled);
            appointment.Ignore(a => a.Duration);

            // Agenda (période) ; chevauchement par collaborateur et occupation de l'accueil : rendez-vous planifiés seulement.
            appointment.HasIndex(a => new { a.StartsAtUtc, a.Id }).HasDatabaseName("ix_appointments_starts_at");
            appointment.HasIndex(a => new { a.ResponsibleUserId, a.StartsAtUtc })
                .HasDatabaseName("ix_appointments_responsible_planned").HasFilter("status = 'planned'");
            appointment.HasIndex(a => a.StartsAtUtc)
                .HasDatabaseName("ix_appointments_unassigned_planned").HasFilter("status = 'planned' AND responsible_user_id IS NULL");
            // Rendez-vous d'un client (fiche client).
            appointment.HasIndex(a => new { a.ClientId, a.StartsAtUtc }).HasDatabaseName("ix_appointments_client_starts_at");
            appointment.HasIndex(a => a.CreatedByUserId).HasDatabaseName("ix_appointments_created_by_user_id");
            appointment.HasIndex(a => a.UpdatedByUserId).HasDatabaseName("ix_appointments_updated_by_user_id");
            appointment.HasIndex(a => a.CancelledByUserId).HasDatabaseName("ix_appointments_cancelled_by_user_id");

            // Aucune suppression en cascade : un client ou un compte référencé ne peut pas disparaître.
            appointment.HasOne<Client>().WithMany().HasForeignKey(a => a.ClientId)
                .HasConstraintName("fk_appointments_clients").OnDelete(DeleteBehavior.Restrict);
            appointment.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.ResponsibleUserId)
                .HasConstraintName("fk_appointments_responsible_users").OnDelete(DeleteBehavior.Restrict);
            appointment.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.CreatedByUserId)
                .HasConstraintName("fk_appointments_created_by_users").OnDelete(DeleteBehavior.Restrict);
            appointment.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.UpdatedByUserId)
                .HasConstraintName("fk_appointments_updated_by_users").OnDelete(DeleteBehavior.Restrict);
            appointment.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.CancelledByUserId)
                .HasConstraintName("fk_appointments_cancelled_by_users").OnDelete(DeleteBehavior.Restrict);
        });

        // Éligibilité explicite des collaborateurs (D-03-14, D-03-17) : table propre au module, auth inchangé.
        modelBuilder.Entity<AppointmentResponsible>(responsible =>
        {
            responsible.ToTable("responsibles", Schema, t =>
                t.HasCheckConstraint("ck_responsibles_updated_after_created", "updated_at_utc >= created_at_utc"));
            responsible.HasKey(r => r.UserId).HasName("pk_responsibles");
            responsible.Property(r => r.UserId).HasColumnName("user_id").ValueGeneratedNever();
            responsible.Property(r => r.IsEligible).HasColumnName("is_eligible").IsRequired();
            responsible.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            responsible.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            responsible.HasOne<UserAccount>().WithOne().HasForeignKey<AppointmentResponsible>(r => r.UserId)
                .HasConstraintName("fk_responsibles_users").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
