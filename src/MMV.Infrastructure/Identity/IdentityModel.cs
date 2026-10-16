using Microsoft.EntityFrameworkCore;
using MMV.Domain.Identity;

namespace MMV.Infrastructure.Identity;

/// <summary>
/// Schéma <c>auth</c> : comptes, permissions techniques, sessions, trace de bootstrap. Noms de tables,
/// colonnes et contraintes explicites (snake_case) pour un SQL relisible.
/// </summary>
internal static class IdentityModel
{
    public const string Schema = "auth";

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(user =>
        {
            user.ToTable("users", Schema);
            user.HasKey(u => u.Id).HasName("pk_users");
            user.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
            user.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(UserAccount.UserNameMaxLength).IsRequired();
            user.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(UserAccount.UserNameMaxLength).IsRequired();
            user.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(UserAccount.DisplayNameMaxLength).IsRequired();
            user.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            user.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
            user.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count").IsRequired();
            user.Property(u => u.LockoutEndUtc).HasColumnName("lockout_end_utc");
            user.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            user.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ux_users_normalized_user_name");
            user.ToTable(t => t.HasCheckConstraint("ck_users_failed_login_count", "failed_login_count >= 0"));

            user.HasMany(u => u.Permissions)
                .WithOne()
                .HasForeignKey(p => p.UserId)
                .HasConstraintName("fk_user_permissions_users")
                .OnDelete(DeleteBehavior.Cascade);
            user.Navigation(u => u.Permissions).HasField("_permissions").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PermissionGrant>(permission =>
        {
            permission.ToTable("user_permissions", Schema, t => t.HasCheckConstraint(
                "ck_user_permissions_permission",
                $"permission IN ({string.Join(", ", Permissions.All.Select(p => $"'{p}'"))})"));
            permission.HasKey(p => new { p.UserId, p.Permission }).HasName("pk_user_permissions");
            permission.Property(p => p.UserId).HasColumnName("user_id");
            permission.Property(p => p.Permission).HasColumnName("permission").HasMaxLength(Permissions.MaxLength);
        });

        modelBuilder.Entity<UserSession>(session =>
        {
            session.ToTable("sessions", Schema, t =>
            {
                t.HasCheckConstraint("ck_sessions_token_hash_length", $"octet_length(token_hash) = {UserSession.TokenHashLength}");
                t.HasCheckConstraint("ck_sessions_expiry_after_creation", "expires_at_utc > created_at_utc");
            });
            session.HasKey(s => s.Id).HasName("pk_sessions");
            session.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            session.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
            session.Property(s => s.TokenHash).HasColumnName("token_hash").IsRequired();
            session.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            session.Property(s => s.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();
            session.Property(s => s.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
            session.Property(s => s.RevokedAtUtc).HasColumnName("revoked_at_utc");
            session.Property(s => s.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(UserSession.RevocationReasonMaxLength);
            session.HasIndex(s => s.TokenHash).IsUnique().HasDatabaseName("ux_sessions_token_hash");
            session.HasIndex(s => s.UserId).HasDatabaseName("ix_sessions_user_id");
            session.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .HasConstraintName("fk_sessions_users")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BootstrapRecord>(record =>
        {
            record.ToTable("bootstrap_record", Schema, t => t.HasCheckConstraint(
                "ck_bootstrap_record_singleton", $"id = {BootstrapRecord.SingletonId}"));
            record.HasKey(r => r.Id).HasName("pk_bootstrap_record");
            record.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            // Pas de clé étrangère : la trace du bootstrap survit à une éventuelle suppression du compte.
            record.Property(r => r.AdministratorId).HasColumnName("administrator_id").IsRequired();
            record.Property(r => r.CompletedAtUtc).HasColumnName("completed_at_utc").IsRequired();
        });
    }
}
