using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Domain.Optical;

namespace MMV.Infrastructure.Optical;

/// <summary>
/// Schéma <c>optical</c> (phase 04-B, migration 0006_OpticalModule) : DEUX familles de tables distinctes (D-04-01) —
/// <c>prescriptions</c> / <c>prescription_revisions</c> et <c>measurements</c> / <c>measurement_revisions</c> — sans table
/// commune ni discriminant modifiable. Aucune pièce source, aucun contenu binaire (D-04-16, backlog BL-04-01).
/// <list type="bullet">
/// <item>Valeurs d'œil : texte EXACT tel que saisi (<c>varchar(16)</c>, <c>NULL</c> = non renseigné) ; pour Sphère, Cylindre,
/// Axe, Addition et Prisme, forme décimale imposée par contrainte et valeur numérique EXACTE dérivée par une colonne
/// générée <c>numeric</c> SANS précision ni échelle (aucun arrondi : un <c>numeric(p, s)</c> arrondirait en silence à
/// l'échelle s, ce que le mandat interdit ; aucune borne clinique cachée).</item>
/// <item>Au plus un brouillon par enregistrement et numéros de révision uniques (index uniques partiels).</item>
/// <item>Révision confirmée ou abandonnée immuable, aucune suppression, enregistrement invalidé figé (déclencheurs
/// créés par la migration, hors modèle EF).</item>
/// <item>Concurrence : <c>xmin</c> de l'en-tête ; idempotence : <c>creation_request_id</c> unique, <c>last_request_id</c>.</item>
/// </list>
/// </summary>
internal static class OpticalModel
{
    public const string Schema = "optical";
    public const string PrescriptionReferenceSequence = "prescription_reference_seq";
    public const string MeasurementReferenceSequence = "measurement_reference_seq";

    /// <summary>Colonne générée : valeur décimale exacte de la saisie (virgule → point, signe moins typographique → « - »).</summary>
    public static string NumericValueSql(string column) =>
        $"(replace(replace(replace({column}, ',', '.'), U&'\\2212', '-'), ' ', ''))::numeric";

    /// <summary>Forme décimale (même expression que <see cref="OpticalFields.NumberRegex"/>, signe U+2212 écrit en ASCII).</summary>
    public static string NumberCheckSql(string column) =>
        $"{column} IS NULL OR {column} ~ ('^[-+' || U&'\\2212' || ']? ?[0-9]+([.,][0-9]+)?$')";

    private static readonly string[] Sides = ["od", "og"];

    private static readonly bool[] ClientCreatedDescending = [false, true, true];

    private static readonly ValueConverter<OpticalRevisionStatus, string> StatusConverter =
        new(status => OpticalRevisionStatusCodes.ToCode(status), code => OpticalRevisionStatusCodes.FromCode(code));

    public static void Configure(ModelBuilder modelBuilder)
    {
        foreach (var sequence in new[] { PrescriptionReferenceSequence, MeasurementReferenceSequence })
        {
            modelBuilder.HasSequence<long>(sequence, Schema).StartsAt(1).IncrementsBy(1).HasMax(999_999_999_999_999_999).IsCyclic(false);
        }

        modelBuilder.Entity<Prescription>(record =>
            ConfigureRecord<Prescription, PrescriptionRevision, PrescriptionContent>(record, "prescriptions", "RX", PrescriptionReferenceSequence));
        modelBuilder.Entity<PrescriptionRevision>(revision =>
        {
            ConfigureRevision<Prescription, PrescriptionRevision, PrescriptionContent>(
                revision, "prescription_revisions", "prescription_id",
                confirmedProvenanceSql: "prescribed_on IS NOT NULL AND prescriber_name IS NOT NULL",
                [
                    "prescribed_on IS NULL OR prescribed_on >= DATE '1900-01-01'",
                    "received_on IS NULL OR received_on >= DATE '1900-01-01'",
                ]);
            revision.Property(r => r.PrescriberName).HasColumnName("prescriber_name").HasMaxLength(OpticalLimits.TextMaxLength);
            revision.Property(r => r.PrescriberQualification).HasColumnName("prescriber_qualification").HasMaxLength(OpticalLimits.TextMaxLength);
            revision.Property(r => r.PrescriberIdentifier).HasColumnName("prescriber_identifier").HasMaxLength(OpticalLimits.TextMaxLength);
            revision.Property(r => r.PrescribedOn).HasColumnName("prescribed_on");
            revision.Property(r => r.ReceivedOn).HasColumnName("received_on");
        });

        modelBuilder.Entity<Measurement>(record =>
            ConfigureRecord<Measurement, MeasurementRevision, MeasurementContent>(record, "measurements", "MES", MeasurementReferenceSequence));
        modelBuilder.Entity<MeasurementRevision>(revision =>
        {
            ConfigureRevision<Measurement, MeasurementRevision, MeasurementContent>(
                revision, "measurement_revisions", "measurement_id",
                confirmedProvenanceSql: "measured_on IS NOT NULL AND measured_by_user_id IS NOT NULL",
                ["measured_on IS NULL OR measured_on >= DATE '1900-01-01'"]);
            revision.Property(r => r.MeasuredOn).HasColumnName("measured_on");
            revision.Property(r => r.MeasuredByUserId).HasColumnName("measured_by_user_id");
            revision.Property(r => r.Category).HasColumnName("category").HasMaxLength(OpticalLimits.TextMaxLength);
            revision.HasIndex(r => r.MeasuredByUserId).HasDatabaseName("ix_measurement_revisions_measured_by_user_id");
            revision.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.MeasuredByUserId)
                .HasConstraintName("fk_measurement_revisions_measured_by_users").OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRecord<TRecord, TRevision, TContent>(
        EntityTypeBuilder<TRecord> record, string table, string referencePrefix, string sequence)
        where TRecord : OpticalRecord<TRevision, TContent>
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
    {
        record.ToTable(table, Schema, t =>
        {
            t.HasCheckConstraint($"ck_{table}_reference_format", $"reference ~ '^{referencePrefix}-[0-9]{{6,18}}$'");
            t.HasCheckConstraint($"ck_{table}_updated_after_created", "updated_at_utc >= created_at_utc");
            t.HasCheckConstraint(
                $"ck_{table}_invalidation_complete",
                "(invalidated_at_utc IS NULL) = (invalidated_by_user_id IS NULL) AND (invalidated_at_utc IS NULL) = (invalidation_reason_code IS NULL)"
                + " AND (invalidation_comment IS NULL OR invalidated_at_utc IS NOT NULL)");
            t.HasCheckConstraint($"ck_{table}_invalidated_after_created", "invalidated_at_utc IS NULL OR invalidated_at_utc >= created_at_utc");
            t.HasCheckConstraint(
                $"ck_{table}_invalidation_reason_format", $"invalidation_reason_code IS NULL OR invalidation_reason_code ~ '{OpticalReasons.CodeRegex}'");
        });
        record.HasKey(r => r.Id).HasName($"pk_{table}");
        record.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        // Attribuée par PostgreSQL à l'insertion et relue (RETURNING) ; jamais envoyée ni modifiée par l'API.
        var reference = record.Property(r => r.Reference).HasColumnName("reference").HasMaxLength(OpticalLimits.ReferenceMaxLength).IsRequired()
            .HasDefaultValueSql($"'{referencePrefix}-' || to_char(nextval('{Schema}.{sequence}'), 'FM999999999999000000')");
        reference.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        reference.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        record.Property(r => r.ClientId).HasColumnName("client_id").IsRequired();
        record.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        record.Property(r => r.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        record.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        record.Property(r => r.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        record.Property(r => r.InvalidatedAtUtc).HasColumnName("invalidated_at_utc");
        record.Property(r => r.InvalidatedByUserId).HasColumnName("invalidated_by_user_id");
        record.Property(r => r.InvalidationReasonCode).HasColumnName("invalidation_reason_code").HasMaxLength(OpticalLimits.CodeMaxLength);
        record.Property(r => r.InvalidationComment).HasColumnName("invalidation_comment").HasMaxLength(OpticalLimits.CommentMaxLength);
        record.Property(r => r.CreationRequestId).HasColumnName("creation_request_id").IsRequired();
        record.Property(r => r.LastRequestId).HasColumnName("last_request_id").IsRequired();
        // Npgsql : jeton de concurrence sur la colonne système xmin (aucune colonne créée par la migration).
        record.Property(r => r.Version).IsRowVersion();
        record.Ignore(r => r.IsInvalidated);
        record.Ignore(r => r.OpenDraft);
        record.Ignore(r => r.LastConfirmed);
        record.Ignore(r => r.Chronological);

        record.HasMany(r => r.Revisions).WithOne().HasForeignKey(r => r.RecordId)
            .HasConstraintName($"fk_{table[..^1]}_revisions_{table}").OnDelete(DeleteBehavior.Restrict);
        record.Navigation(r => r.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);

        record.HasIndex(r => r.Reference).IsUnique().HasDatabaseName($"ux_{table}_reference");
        record.HasIndex(r => r.CreationRequestId).IsUnique().HasDatabaseName($"ux_{table}_creation_request_id");
        // Dossier d'un client, du plus récent au plus ancien.
        record.HasIndex(r => new { r.ClientId, r.CreatedAtUtc, r.Id }).IsDescending(ClientCreatedDescending).HasDatabaseName($"ix_{table}_client_created");
        record.HasIndex(r => r.CreatedByUserId).HasDatabaseName($"ix_{table}_created_by_user_id");
        record.HasIndex(r => r.UpdatedByUserId).HasDatabaseName($"ix_{table}_updated_by_user_id");
        record.HasIndex(r => r.InvalidatedByUserId).HasDatabaseName($"ix_{table}_invalidated_by_user_id");

        record.HasOne<Client>().WithMany().HasForeignKey(r => r.ClientId)
            .HasConstraintName($"fk_{table}_clients").OnDelete(DeleteBehavior.Restrict);
        record.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.CreatedByUserId)
            .HasConstraintName($"fk_{table}_created_by_users").OnDelete(DeleteBehavior.Restrict);
        record.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.UpdatedByUserId)
            .HasConstraintName($"fk_{table}_updated_by_users").OnDelete(DeleteBehavior.Restrict);
        record.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.InvalidatedByUserId)
            .HasConstraintName($"fk_{table}_invalidated_by_users").OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureRevision<TRecord, TRevision, TContent>(
        EntityTypeBuilder<TRevision> revision, string table, string recordColumn, string confirmedProvenanceSql, string[] dateChecks)
        where TRecord : OpticalRecord<TRevision, TContent>
        where TRevision : OpticalRevision<TContent>
        where TContent : OpticalContent
    {
        string[] numericColumns = [.. Sides.SelectMany(side => OpticalFields.Names.Where(OpticalFields.Numeric.Contains).Select(field => $"{side}_{field}"))];

        revision.ToTable(table, Schema, t =>
        {
            t.HasCheckConstraint($"ck_{table}_status", "status IN ('draft', 'confirmed', 'discarded')");
            t.HasCheckConstraint(
                $"ck_{table}_confirmation",
                "(status = 'confirmed') = (revision_no IS NOT NULL) AND (revision_no IS NULL) = (confirmed_at_utc IS NULL)"
                + " AND (confirmed_at_utc IS NULL) = (confirmed_by_user_id IS NULL)");
            t.HasCheckConstraint(
                $"ck_{table}_discard",
                "(status = 'discarded') = (discarded_at_utc IS NOT NULL) AND (discarded_at_utc IS NULL) = (discarded_by_user_id IS NULL)"
                + " AND (status <> 'discarded' OR base_revision_no IS NOT NULL)");
            t.HasCheckConstraint(
                $"ck_{table}_numbers",
                "(revision_no IS NULL OR revision_no >= 1) AND (base_revision_no IS NULL OR base_revision_no >= 1)"
                + " AND (revision_no IS NULL OR base_revision_no IS NULL OR revision_no > base_revision_no)");
            t.HasCheckConstraint(
                $"ck_{table}_reason",
                $"(base_revision_no IS NULL) = (reason_code IS NULL) AND (reason_comment IS NULL OR reason_code IS NOT NULL)"
                + $" AND (reason_code IS NULL OR reason_code ~ '{OpticalReasons.CodeRegex}')");
            t.HasCheckConstraint($"ck_{table}_updated_after_started", "updated_at_utc >= started_at_utc");
            // Provenance exigée pour CONFIRMER (facultative au brouillon) : garde de base, en plus du service.
            t.HasCheckConstraint($"ck_{table}_confirmed_provenance", $"status <> 'confirmed' OR ({confirmedProvenanceSql})");
            t.HasCheckConstraint($"ck_{table}_dates", string.Join(" AND ", dateChecks.Select(check => $"({check})")));
            foreach (var column in numericColumns)
            {
                t.HasCheckConstraint($"ck_{table}_{column}_number", NumberCheckSql(column));
            }
        });
        revision.HasKey(r => r.Id).HasName($"pk_{table}");
        revision.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        revision.Property(r => r.RecordId).HasColumnName(recordColumn).IsRequired();
        revision.Property(r => r.Status).HasColumnName("status").HasMaxLength(OpticalLimits.StatusMaxLength).IsRequired().HasConversion(StatusConverter);
        revision.Property(r => r.RevisionNo).HasColumnName("revision_no");
        revision.Property(r => r.BaseRevisionNo).HasColumnName("base_revision_no");
        revision.Property(r => r.ReasonCode).HasColumnName("reason_code").HasMaxLength(OpticalLimits.CodeMaxLength);
        revision.Property(r => r.ReasonComment).HasColumnName("reason_comment").HasMaxLength(OpticalLimits.CommentMaxLength);

        revision.Property(r => r.OdSphere).HasColumnName("od_sphere").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OdCylinder).HasColumnName("od_cylinder").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OdAxis).HasColumnName("od_axis").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OdAddition).HasColumnName("od_addition").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OdPrism).HasColumnName("od_prism").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OdBase).HasColumnName("od_base").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OdAcuity).HasColumnName("od_acuity").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OgSphere).HasColumnName("og_sphere").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OgCylinder).HasColumnName("og_cylinder").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OgAxis).HasColumnName("og_axis").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OgAddition).HasColumnName("og_addition").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OgPrism).HasColumnName("og_prism").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OgBase).HasColumnName("og_base").HasMaxLength(OpticalFields.ValueMaxLength);
        revision.Property(r => r.OgAcuity).HasColumnName("og_acuity").HasMaxLength(OpticalFields.ValueMaxLength);

        // Valeurs numériques exactes dérivées par PostgreSQL (propriétés fantômes : jamais écrites par l'application).
        foreach (var column in numericColumns)
        {
            revision.Property<decimal?>($"{column}_value").HasColumnName($"{column}_value").HasColumnType("numeric")
                .HasComputedColumnSql(NumericValueSql(column), stored: true);
        }

        revision.Property(r => r.Notes).HasColumnName("notes").HasMaxLength(OpticalLimits.NotesMaxLength);
        revision.Property(r => r.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        revision.Property(r => r.StartedByUserId).HasColumnName("started_by_user_id").IsRequired();
        revision.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        revision.Property(r => r.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        revision.Property(r => r.ConfirmedAtUtc).HasColumnName("confirmed_at_utc");
        revision.Property(r => r.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
        revision.Property(r => r.DiscardedAtUtc).HasColumnName("discarded_at_utc");
        revision.Property(r => r.DiscardedByUserId).HasColumnName("discarded_by_user_id");
        revision.Ignore(r => r.IsDraft);
        revision.Ignore(r => r.IsRectification);
        revision.Ignore(r => r.Values);
        revision.Ignore(r => r.Content);

        // Révisions d'un enregistrement (ordre chronologique) ; numéros uniques ; AU PLUS UN brouillon ouvert.
        revision.HasIndex(r => new { r.RecordId, r.StartedAtUtc, r.Id }).HasDatabaseName($"ix_{table}_{recordColumn[..^3]}_started");
        revision.HasIndex(r => new { r.RecordId, r.RevisionNo }).IsUnique().HasFilter("revision_no IS NOT NULL").HasDatabaseName($"ux_{table}_number");
        revision.HasIndex(r => r.RecordId).IsUnique().HasFilter("status = 'draft'").HasDatabaseName($"ux_{table}_open_draft");
        revision.HasIndex(r => r.StartedByUserId).HasDatabaseName($"ix_{table}_started_by_user_id");
        revision.HasIndex(r => r.UpdatedByUserId).HasDatabaseName($"ix_{table}_updated_by_user_id");
        revision.HasIndex(r => r.ConfirmedByUserId).HasDatabaseName($"ix_{table}_confirmed_by_user_id");
        revision.HasIndex(r => r.DiscardedByUserId).HasDatabaseName($"ix_{table}_discarded_by_user_id");

        revision.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.StartedByUserId)
            .HasConstraintName($"fk_{table}_started_by_users").OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.UpdatedByUserId)
            .HasConstraintName($"fk_{table}_updated_by_users").OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.ConfirmedByUserId)
            .HasConstraintName($"fk_{table}_confirmed_by_users").OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.DiscardedByUserId)
            .HasConstraintName($"fk_{table}_discarded_by_users").OnDelete(DeleteBehavior.Restrict);
    }
}
