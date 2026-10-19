using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MMV.Domain.Clients;
using MMV.Domain.Identity;

namespace MMV.Infrastructure.Clients;

/// <summary>
/// Schéma <c>clients</c> (phase 03-A2, migration 0003_ClientsModule) : table <c>clients.clients</c>, référence
/// lisible issue d'une séquence PostgreSQL, contraintes explicites, aucune donnée optique, médicale ni
/// d'assurance. Concurrence optimiste par la colonne système <c>xmin</c>.
/// </summary>
internal static class ClientModel
{
    public const string Schema = "clients";
    public const string ReferenceSequence = "client_reference_seq";

    /// <summary>
    /// <c>CLI-</c> + numéro de séquence complété à 6 chiffres au minimum, JAMAIS tronqué au-delà de 999 999
    /// (<c>to_char</c> : les 6 dernières positions <c>0</c> sont toujours écrites, les <c>9</c> seulement si
    /// significatives ; <c>lpad</c> tronquerait et produirait des doublons).
    /// </summary>
    public const string ReferenceDefaultSql =
        $"'CLI-' || to_char(nextval('{Schema}.{ReferenceSequence}'), 'FM999999999999000000')";

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(ReferenceSequence, Schema).StartsAt(1).IncrementsBy(1)
            .HasMax(999_999_999_999_999_999).IsCyclic(false);

        modelBuilder.Entity<Client>(client =>
        {
            client.ToTable("clients", Schema, t =>
            {
                t.HasCheckConstraint("ck_clients_first_name_not_blank", "btrim(first_name) <> ''");
                t.HasCheckConstraint("ck_clients_last_name_not_blank", "btrim(last_name) <> ''");
                t.HasCheckConstraint("ck_clients_birth_date_range", "birth_date IS NULL OR birth_date >= DATE '1900-01-01'");
                t.HasCheckConstraint("ck_clients_reference_format", "reference ~ '^CLI-[0-9]{6,18}$'");
                t.HasCheckConstraint("ck_clients_updated_after_created", "updated_at_utc >= created_at_utc");
                t.HasCheckConstraint("ck_clients_archived_after_created", "archived_at_utc IS NULL OR archived_at_utc >= created_at_utc");
            });
            client.HasKey(c => c.Id).HasName("pk_clients");
            client.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

            // Attribuée par PostgreSQL à l'insertion et relue (RETURNING) ; jamais envoyée ni modifiée par l'API.
            var reference = client.Property(c => c.Reference).HasColumnName("reference")
                .HasMaxLength(Client.ReferenceMaxLength).IsRequired()
                .HasDefaultValueSql(ReferenceDefaultSql);
            reference.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            reference.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            client.Property(c => c.FirstName).HasColumnName("first_name").HasMaxLength(Client.NameMaxLength).IsRequired();
            client.Property(c => c.LastName).HasColumnName("last_name").HasMaxLength(Client.NameMaxLength).IsRequired();
            client.Property(c => c.BirthDate).HasColumnName("birth_date");
            client.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(Client.PhoneMaxLength);
            client.Property(c => c.Email).HasColumnName("email").HasMaxLength(Client.EmailMaxLength);
            client.Property(c => c.AddressLine).HasColumnName("address_line").HasMaxLength(Client.AddressLineMaxLength);
            client.Property(c => c.City).HasColumnName("city").HasMaxLength(Client.CityMaxLength);
            client.Property(c => c.PostalCode).HasColumnName("postal_code").HasMaxLength(Client.PostalCodeMaxLength);
            client.Property(c => c.Notes).HasColumnName("notes").HasMaxLength(Client.NotesMaxLength);
            client.Property(c => c.SearchText).HasColumnName("search_text").HasMaxLength(Client.SearchTextMaxLength).IsRequired();
            client.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            client.Property(c => c.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            client.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            client.Property(c => c.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
            client.Property(c => c.ArchivedAtUtc).HasColumnName("archived_at_utc");
            // Npgsql : jeton de concurrence sur la colonne système xmin (aucune colonne créée par la migration).
            client.Property(c => c.Version).IsRowVersion();
            client.Ignore(c => c.IsArchived);

            client.HasIndex(c => c.Reference).IsUnique().HasDatabaseName("ux_clients_reference");
            // Liste par défaut : clients actifs triés par nom (index partiel).
            client.HasIndex(c => new { c.LastName, c.FirstName, c.Id }).HasDatabaseName("ix_clients_active_name").HasFilter("archived_at_utc IS NULL");
            client.HasIndex(c => new { c.CreatedAtUtc, c.Id }).HasDatabaseName("ix_clients_created_at");
            client.HasIndex(c => c.CreatedByUserId).HasDatabaseName("ix_clients_created_by_user_id");
            client.HasIndex(c => c.UpdatedByUserId).HasDatabaseName("ix_clients_updated_by_user_id");

            // Traçabilité minimale : auteur de la création et de la dernière modification.
            client.HasOne<UserAccount>().WithMany().HasForeignKey(c => c.CreatedByUserId)
                .HasConstraintName("fk_clients_created_by_users").OnDelete(DeleteBehavior.Restrict);
            client.HasOne<UserAccount>().WithMany().HasForeignKey(c => c.UpdatedByUserId)
                .HasConstraintName("fk_clients_updated_by_users").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
