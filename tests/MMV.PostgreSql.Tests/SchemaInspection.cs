using Npgsql;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Inventaire des objets d'une base V2, relu par SELECT de catalogues dans une session forcée en lecture seule.
/// Sert de contrôle avant migration (base vide), après la baseline 01B (baseline seule), après la migration
/// d'identité de la phase 02 (baseline + schéma <c>auth</c> exact), après la migration Clients de la phase 03-A2
/// (+ schéma <c>clients</c> exact), après la migration Rendez-vous de la phase 03-B2 (+ schéma <c>appointments</c>
/// exact) et après les tests d'écriture (aucun objet résiduel).
/// </summary>
public sealed record SchemaState(
    string TransactionReadOnly,
    string PublicSchemaOwner,
    IReadOnlyList<string> ExtraSchemas,
    IReadOnlyList<string> Relations,
    IReadOnlyList<string> Routines,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Decorations,
    IReadOnlyList<string> HistoryColumns,
    IReadOnlyList<string> HistoryConstraints,
    IReadOnlyList<string> HistoryRows,
    IReadOnlyList<string> AuthColumns,
    IReadOnlyList<string> AuthConstraints,
    IReadOnlyList<string> AuthIndexes,
    IReadOnlyList<string> ClientsColumns,
    IReadOnlyList<string> ClientsConstraints,
    IReadOnlyList<string> ClientsIndexes,
    IReadOnlyList<string> ClientsSequences,
    IReadOnlyList<string> AppointmentsColumns,
    IReadOnlyList<string> AppointmentsConstraints,
    IReadOnlyList<string> AppointmentsIndexes,
    IReadOnlyList<string> AuditColumns,
    IReadOnlyList<string> AuditConstraints,
    IReadOnlyList<string> AuditIndexes,
    IReadOnlyList<string> OpticalColumns,
    IReadOnlyList<string> OpticalConstraints,
    IReadOnlyList<string> OpticalIndexes,
    IReadOnlyList<string> OpticalSequences)
{
    public const string BaselineMigrationId = "20261015125000_InitialBaseline";
    public const string AuthIdentityMigrationId = "20261015223849_AuthIdentity";
    public const string ClientsModuleMigrationId = "20261016230630_ClientsModule";
    public const string AppointmentsModuleMigrationId = "20261017150504_AppointmentsModule";
    public const string AuditJournalMigrationId = "20261018163108_AuditJournal";
    public const string OpticalModuleMigrationId = "20261027134615_OpticalModule";

    /// <summary>
    /// Migrations de l'assembly, dans l'ordre (phase 03-A2 : + ClientsModule ; phase 03-B2 : + AppointmentsModule ;
    /// phase 03-D2 : + AuditJournal ; phase 04-B : + OpticalModule, préparée, G04-SQL requise avant toute application).
    /// </summary>
    public static readonly string[] AllMigrationIds =
        [BaselineMigrationId, AuthIdentityMigrationId, ClientsModuleMigrationId, AppointmentsModuleMigrationId, AuditJournalMigrationId, OpticalModuleMigrationId];

    /// <summary>
    /// Base vide, baseline seule, baseline + identité, + clients, + rendez-vous ou + journal d'audit : aucun autre
    /// état n'est accepté. Les différences rapportées sont celles de l'état le plus récent.
    /// </summary>
    public IReadOnlyList<string> KnownStateDifferences(string role)
    {
        if (IsEmpty || BaselineDifferences(role).Count == 0 || AuthIdentityDifferences(role).Count == 0
            || ClientsModuleDifferences(role).Count == 0 || AppointmentsModuleDifferences(role).Count == 0
            || AuditJournalDifferences(role).Count == 0)
        {
            return [];
        }

        return OpticalModuleDifferences(role);
    }

    public bool HistoryExists => Relations.Any(r => r.StartsWith("public.__EFMigrationsHistory ", StringComparison.Ordinal));

    /// <summary>Base neuve : aucun objet utilisateur.</summary>
    public bool IsEmpty =>
        ExtraSchemas.Count == 0 && Relations.Count == 0 && Routines.Count == 0 && Types.Count == 0
        && Extensions.Count == 0 && Decorations.Count == 0;

    /// <summary>Exactement l'état produit par 0001_InitialBaseline.idempotent.sql, objets possédés par <paramref name="role"/>.</summary>
    public IReadOnlyList<string> BaselineDifferences(string role)
    {
        var differences = new List<string>();
        Expect(differences, "schémas", ExtraSchemas, []);
        Expect(differences, "relations", Relations,
        [
            $"public.PK___EFMigrationsHistory kind=i owner={role} acl=default",
            $"public.__EFMigrationsHistory kind=r owner={role} acl=default",
        ]);
        Expect(differences, "routines", Routines, []);
        Expect(differences, "types", Types, []);
        Expect(differences, "extensions", Extensions, []);
        Expect(differences, "décorations", Decorations, []);
        Expect(differences, "colonnes", HistoryColumns,
        [
            "MigrationId character varying(150) notnull=t",
            "ProductVersion character varying(32) notnull=t",
        ]);
        // PostgreSQL 18 catalogue aussi les NOT NULL dans pg_constraint (contype 'n'), avec des noms générés.
        Expect(differences, "contraintes", HistoryConstraints,
        [
            "PK___EFMigrationsHistory PRIMARY KEY (\"MigrationId\")",
            "__EFMigrationsHistory_MigrationId_not_null NOT NULL \"MigrationId\"",
            "__EFMigrationsHistory_ProductVersion_not_null NOT NULL \"ProductVersion\"",
        ]);
        Expect(differences, "lignes", HistoryRows, [$"{BaselineMigrationId} 10.0.4"]);
        Expect(differences, "colonnes auth", AuthColumns, []);
        return differences;
    }

    /// <summary>
    /// Exactement l'état produit par 0001 puis 0002_AuthIdentity (docs/phase/02/sql/), objets possédés par
    /// <paramref name="role"/>. Les contraintes sont comparées par nom, type et règle de suppression ;
    /// leur texte SQL est relu dans le script approuvé.
    /// </summary>
    public IReadOnlyList<string> AuthIdentityDifferences(string role) => IdentityDifferences(role, Stage.Auth);

    /// <summary>
    /// Exactement l'état produit par 0001, 0002 puis 0003_ClientsModule (docs/phase/03/sql/) : schéma <c>auth</c>
    /// inchangé (contrainte de permissions recréée sous le même nom), schéma <c>clients</c> exact.
    /// </summary>
    public IReadOnlyList<string> ClientsModuleDifferences(string role) => IdentityDifferences(role, Stage.Clients);

    /// <summary>
    /// Exactement l'état produit par 0001 à 0004_AppointmentsModule (docs/phase/03/sql/) : schémas <c>auth</c> et
    /// <c>clients</c> inchangés (contrainte de permissions recréée sous le même nom), schéma <c>appointments</c> exact.
    /// </summary>
    public IReadOnlyList<string> AppointmentsModuleDifferences(string role) => IdentityDifferences(role, Stage.Appointments);

    /// <summary>
    /// Exactement l'état produit par 0001 à 0005_AuditJournal (docs/phase/03/sql/, 03-D2) : schémas précédents
    /// inchangés (contrainte de permissions recréée sous le même nom), schéma <c>audit</c> exact, fonction et
    /// déclencheurs d'ajout seul.
    /// </summary>
    public IReadOnlyList<string> AuditJournalDifferences(string role) => IdentityDifferences(role, Stage.Audit);

    /// <summary>
    /// Exactement l'état produit par 0001 à 0006_OpticalModule (docs/phase/04/sql/, 04-B) : schémas précédents inchangés
    /// (contrainte de permissions recréée sous le même nom), schéma <c>optical</c> exact (deux familles de tables,
    /// séquences, fonctions et déclencheurs d'immuabilité).
    /// </summary>
    public IReadOnlyList<string> OpticalModuleDifferences(string role) => IdentityDifferences(role, Stage.Optical);

    private enum Stage
    {
        Auth,
        Clients,
        Appointments,
        Audit,
        Optical,
    }

    private List<string> IdentityDifferences(string role, Stage stage)
    {
        var withClients = stage >= Stage.Clients;
        var withAppointments = stage >= Stage.Appointments;
        var withAudit = stage >= Stage.Audit;
        var withOptical = stage >= Stage.Optical;
        var differences = new List<string>();
        string[] schemas = withOptical ? ["appointments", "audit", "auth", "clients", "optical"]
            : withAudit ? ["appointments", "audit", "auth", "clients"]
            : withAppointments ? ["appointments", "auth", "clients"] : withClients ? ["auth", "clients"] : ["auth"];
        Expect(differences, "schémas", ExtraSchemas, schemas);
        string[] authRelations =
        [
            "bootstrap_record kind=r", "pk_bootstrap_record kind=i",
            "users kind=r", "pk_users kind=i", "ux_users_normalized_user_name kind=i",
            "user_permissions kind=r", "pk_user_permissions kind=i",
            "sessions kind=r", "pk_sessions kind=i", "ix_sessions_user_id kind=i", "ux_sessions_token_hash kind=i",
        ];
        Expect(differences, "relations", Relations,
        [
            $"public.PK___EFMigrationsHistory kind=i owner={role} acl=default",
            $"public.__EFMigrationsHistory kind=r owner={role} acl=default",
            .. authRelations.Select(r => $"auth.{r} owner={role} acl=default"),
            .. (withClients ? ClientsRelations : []).Select(r => $"clients.{r} owner={role} acl=default"),
            .. (withAppointments ? AppointmentsRelations : []).Select(r => $"appointments.{r} owner={role} acl=default"),
            .. (withAudit ? AuditRelations : []).Select(r => $"audit.{r} owner={role} acl=default"),
            .. (withOptical ? OpticalRelations : []).Select(r => $"optical.{r} owner={role} acl=default"),
        ]);
        Expect(differences, "routines", Routines,
        [
            .. withAudit ? ["audit.reject_event_mutation"] : Array.Empty<string>(),
            .. withOptical ? ["optical.reject_frozen_record_change", "optical.reject_frozen_revision_change"] : Array.Empty<string>(),
        ]);
        Expect(differences, "types", Types, []);
        Expect(differences, "extensions", Extensions, []);
        Expect(differences, "décorations", Decorations,
        [
            .. withAudit ? ["trigger trg_events_append_only on audit.events", "trigger trg_events_no_truncate on audit.events"] : Array.Empty<string>(),
            .. withOptical
                ? OpticalTables.SelectMany(t => new[] { $"trigger trg_{t}_frozen on optical.{t}", $"trigger trg_{t}_no_truncate on optical.{t}" })
                : [],
        ]);
        Expect(differences, "lignes", HistoryRows,
        [
            $"{BaselineMigrationId} 10.0.4",
            $"{AuthIdentityMigrationId} 10.0.4",
            .. (withClients ? [$"{ClientsModuleMigrationId} 10.0.4"] : Array.Empty<string>()),
            .. (withAppointments ? [$"{AppointmentsModuleMigrationId} 10.0.4"] : Array.Empty<string>()),
            .. (withAudit ? [$"{AuditJournalMigrationId} 10.0.4"] : Array.Empty<string>()),
            .. (withOptical ? [$"{OpticalModuleMigrationId} 10.0.4"] : Array.Empty<string>()),
        ]);
        Expect(differences, "colonnes auth", AuthColumns,
        [
            "bootstrap_record.administrator_id uuid notnull=t",
            "bootstrap_record.completed_at_utc timestamp with time zone notnull=t",
            "bootstrap_record.id smallint notnull=t",
            "sessions.created_at_utc timestamp with time zone notnull=t",
            "sessions.expires_at_utc timestamp with time zone notnull=t",
            "sessions.id uuid notnull=t",
            "sessions.last_seen_at_utc timestamp with time zone notnull=t",
            "sessions.revocation_reason character varying(32) notnull=f",
            "sessions.revoked_at_utc timestamp with time zone notnull=f",
            "sessions.token_hash bytea notnull=t",
            "sessions.user_id uuid notnull=t",
            "user_permissions.permission character varying(64) notnull=t",
            "user_permissions.user_id uuid notnull=t",
            "users.created_at_utc timestamp with time zone notnull=t",
            "users.display_name character varying(128) notnull=t",
            "users.failed_login_count integer notnull=t",
            "users.id uuid notnull=t",
            "users.is_active boolean notnull=t",
            "users.lockout_end_utc timestamp with time zone notnull=f",
            "users.normalized_user_name character varying(64) notnull=t",
            "users.password_hash text notnull=t",
            "users.user_name character varying(64) notnull=t",
        ]);
        string[] notNull =
        [
            "bootstrap_record.id", "bootstrap_record.administrator_id", "bootstrap_record.completed_at_utc",
            "sessions.id", "sessions.user_id", "sessions.token_hash", "sessions.created_at_utc",
            "sessions.last_seen_at_utc", "sessions.expires_at_utc",
            "user_permissions.user_id", "user_permissions.permission",
            "users.id", "users.user_name", "users.normalized_user_name", "users.display_name",
            "users.password_hash", "users.is_active", "users.failed_login_count", "users.created_at_utc",
        ];
        Expect(differences, "contraintes auth", AuthConstraints,
        [
            "bootstrap_record.ck_bootstrap_record_singleton c -",
            "bootstrap_record.pk_bootstrap_record p -",
            "sessions.ck_sessions_expiry_after_creation c -",
            "sessions.ck_sessions_token_hash_length c -",
            "sessions.fk_sessions_users f c",
            "sessions.pk_sessions p -",
            "user_permissions.ck_user_permissions_permission c -",
            "user_permissions.fk_user_permissions_users f c",
            "user_permissions.pk_user_permissions p -",
            "users.ck_users_failed_login_count c -",
            "users.pk_users p -",
            // PostgreSQL 18 : NOT NULL catalogués dans pg_constraint, noms générés <table>_<colonne>_not_null.
            .. notNull.Select(c => $"{c[..c.IndexOf('.', StringComparison.Ordinal)]}.{c.Replace('.', '_')}_not_null n -"),
        ]);
        Expect(differences, "index auth", AuthIndexes,
        [
            "ix_sessions_user_id unique=f",
            "pk_bootstrap_record unique=t",
            "pk_sessions unique=t",
            "pk_user_permissions unique=t",
            "pk_users unique=t",
            "ux_sessions_token_hash unique=t",
            "ux_users_normalized_user_name unique=t",
        ]);
        Expect(differences, "colonnes clients", ClientsColumns, withClients ? ClientsColumnsExpected : []);
        Expect(differences, "contraintes clients", ClientsConstraints, withClients ? ClientsConstraintsExpected : []);
        Expect(differences, "index clients", ClientsIndexes, withClients ? ClientsIndexesExpected : []);
        Expect(differences, "séquences clients", ClientsSequences, withClients
            ? ["client_reference_seq type=bigint start=1 increment=1 min=1 max=999999999999999999 cycle=f owned_by=-"]
            : []);
        Expect(differences, "colonnes appointments", AppointmentsColumns, withAppointments ? AppointmentsColumnsExpected : []);
        Expect(differences, "contraintes appointments", AppointmentsConstraints, withAppointments ? AppointmentsConstraintsExpected : []);
        Expect(differences, "index appointments", AppointmentsIndexes, withAppointments ? AppointmentsIndexesExpected : []);
        Expect(differences, "colonnes audit", AuditColumns, withAudit ? AuditColumnsExpected : []);
        Expect(differences, "contraintes audit", AuditConstraints, withAudit ? AuditConstraintsExpected : []);
        Expect(differences, "index audit", AuditIndexes, withAudit ? AuditIndexesExpected : []);
        Expect(differences, "colonnes optical", OpticalColumns, withOptical ? OpticalColumnsExpected : []);
        Expect(differences, "contraintes optical", OpticalConstraints, withOptical ? OpticalConstraintsExpected : []);
        Expect(differences, "index optical", OpticalIndexes, withOptical ? OpticalIndexesExpected : []);
        Expect(differences, "séquences optical", OpticalSequences, withOptical
            ? [.. OpticalSequenceKinds.Select(k => $"{k}_reference_seq type=bigint start=1 increment=1 min=1 max=999999999999999999 cycle=f owned_by=-")]
            : []);
        return differences;
    }

    // ---------- 0006_OpticalModule (04-B) : deux familles symétriques (en-tête, révisions, colonne de rattachement) ----------

    private static readonly (string Records, string Revisions, string RecordColumn)[] OpticalFamilies =
        [("prescriptions", "prescription_revisions", "prescription_id"), ("measurements", "measurement_revisions", "measurement_id")];

    private static readonly string[] OpticalSequenceKinds = ["measurement", "prescription"];

    private static readonly string[] OpticalTables = ["measurement_revisions", "measurements", "prescription_revisions", "prescriptions"];

    private static readonly string[] EyeColumns =
        [.. new[] { "od", "og" }.SelectMany(side => new[] { "sphere", "cylinder", "axis", "addition", "prism", "base", "acuity" }.Select(f => $"{side}_{f}"))];

    private static readonly string[] NumericEyeColumns =
        [.. new[] { "od", "og" }.SelectMany(side => new[] { "sphere", "cylinder", "axis", "addition", "prism" }.Select(f => $"{side}_{f}"))];

    private static readonly string[] RecordUserColumns = ["created_by_user_id", "updated_by_user_id", "invalidated_by_user_id"];

    private static readonly string[] RevisionUserColumns = ["started_by_user_id", "updated_by_user_id", "confirmed_by_user_id", "discarded_by_user_id"];

    private static IEnumerable<string> OpticalIndexNames()
    {
        foreach (var (records, revisions, recordColumn) in OpticalFamilies)
        {
            yield return $"pk_{records}";
            yield return $"pk_{revisions}";
            yield return $"ux_{records}_reference";
            yield return $"ux_{records}_creation_request_id";
            yield return $"ix_{records}_client_created";
            foreach (var column in RecordUserColumns)
            {
                yield return $"ix_{records}_{column}";
            }

            yield return $"ix_{revisions}_{recordColumn[..^3]}_started";
            yield return $"ux_{revisions}_number";
            yield return $"ux_{revisions}_open_draft";
            foreach (var column in RevisionUserColumns)
            {
                yield return $"ix_{revisions}_{column}";
            }
        }

        yield return "ix_measurement_revisions_measured_by_user_id";
    }

    private static readonly string[] OpticalRelations =
    [
        .. OpticalTables.Select(t => $"{t} kind=r"),
        "prescription_reference_seq kind=S", "measurement_reference_seq kind=S",
        .. OpticalIndexNames().Select(i => $"{i} kind=i"),
    ];

    private static readonly string[] OpticalIndexesExpected =
        [.. OpticalIndexNames().Select(i => $"{i} unique={(i.StartsWith("ix_", StringComparison.Ordinal) ? 'f' : 't')}")];

    // Valeurs d'œil : texte exact (varchar(16)) et valeur numérique EXACTE générée (numeric sans précision ni échelle).
    private static readonly string[] OpticalColumnsExpected =
    [
        .. OpticalFamilies.SelectMany(f => new[]
        {
            $"{f.Records}.id uuid notnull=t", $"{f.Records}.reference character varying(24) notnull=t", $"{f.Records}.client_id uuid notnull=t",
            $"{f.Records}.created_at_utc timestamp with time zone notnull=t", $"{f.Records}.created_by_user_id uuid notnull=t",
            $"{f.Records}.updated_at_utc timestamp with time zone notnull=t", $"{f.Records}.updated_by_user_id uuid notnull=t",
            $"{f.Records}.invalidated_at_utc timestamp with time zone notnull=f", $"{f.Records}.invalidated_by_user_id uuid notnull=f",
            $"{f.Records}.invalidation_reason_code character varying(32) notnull=f", $"{f.Records}.invalidation_comment character varying(500) notnull=f",
            $"{f.Records}.creation_request_id uuid notnull=t", $"{f.Records}.last_request_id uuid notnull=t",
            $"{f.Revisions}.id uuid notnull=t", $"{f.Revisions}.{f.RecordColumn} uuid notnull=t", $"{f.Revisions}.status character varying(16) notnull=t",
            $"{f.Revisions}.revision_no integer notnull=f", $"{f.Revisions}.base_revision_no integer notnull=f",
            $"{f.Revisions}.reason_code character varying(32) notnull=f", $"{f.Revisions}.reason_comment character varying(500) notnull=f",
            $"{f.Revisions}.notes character varying(2000) notnull=f",
            $"{f.Revisions}.started_at_utc timestamp with time zone notnull=t", $"{f.Revisions}.started_by_user_id uuid notnull=t",
            $"{f.Revisions}.updated_at_utc timestamp with time zone notnull=t", $"{f.Revisions}.updated_by_user_id uuid notnull=t",
            $"{f.Revisions}.confirmed_at_utc timestamp with time zone notnull=f", $"{f.Revisions}.confirmed_by_user_id uuid notnull=f",
            $"{f.Revisions}.discarded_at_utc timestamp with time zone notnull=f", $"{f.Revisions}.discarded_by_user_id uuid notnull=f",
        }.Concat(EyeColumns.Select(c => $"{f.Revisions}.{c} character varying(16) notnull=f"))
         .Concat(NumericEyeColumns.Select(c => $"{f.Revisions}.{c}_value numeric notnull=f"))),
        "prescription_revisions.prescriber_name character varying(120) notnull=f",
        "prescription_revisions.prescriber_qualification character varying(120) notnull=f",
        "prescription_revisions.prescriber_identifier character varying(120) notnull=f",
        "prescription_revisions.prescribed_on date notnull=f",
        "prescription_revisions.received_on date notnull=f",
        "measurement_revisions.measured_on date notnull=f",
        "measurement_revisions.measured_by_user_id uuid notnull=f",
        "measurement_revisions.category character varying(120) notnull=f",
    ];

    private static readonly string[] OpticalConstraintsExpected =
    [
        .. OpticalFamilies.SelectMany(f => new[]
        {
            $"{f.Records}.pk_{f.Records} p -", $"{f.Records}.fk_{f.Records}_clients f r",
            $"{f.Records}.fk_{f.Records}_created_by_users f r", $"{f.Records}.fk_{f.Records}_updated_by_users f r",
            $"{f.Records}.fk_{f.Records}_invalidated_by_users f r",
            $"{f.Records}.ck_{f.Records}_reference_format c -", $"{f.Records}.ck_{f.Records}_updated_after_created c -",
            $"{f.Records}.ck_{f.Records}_invalidation_complete c -", $"{f.Records}.ck_{f.Records}_invalidated_after_created c -",
            $"{f.Records}.ck_{f.Records}_invalidation_reason_format c -",
            $"{f.Revisions}.pk_{f.Revisions} p -", $"{f.Revisions}.fk_{f.Revisions}_{f.Records} f r",
            $"{f.Revisions}.fk_{f.Revisions}_started_by_users f r", $"{f.Revisions}.fk_{f.Revisions}_updated_by_users f r",
            $"{f.Revisions}.fk_{f.Revisions}_confirmed_by_users f r", $"{f.Revisions}.fk_{f.Revisions}_discarded_by_users f r",
            $"{f.Revisions}.ck_{f.Revisions}_status c -", $"{f.Revisions}.ck_{f.Revisions}_confirmation c -",
            $"{f.Revisions}.ck_{f.Revisions}_discard c -", $"{f.Revisions}.ck_{f.Revisions}_numbers c -", $"{f.Revisions}.ck_{f.Revisions}_reason c -",
            $"{f.Revisions}.ck_{f.Revisions}_updated_after_started c -", $"{f.Revisions}.ck_{f.Revisions}_confirmed_provenance c -",
            $"{f.Revisions}.ck_{f.Revisions}_dates c -",
        }.Concat(NumericEyeColumns.Select(c => $"{f.Revisions}.ck_{f.Revisions}_{c}_number c -"))
         .Concat(new[] { "id", "reference", "client_id", "created_at_utc", "created_by_user_id", "updated_at_utc", "updated_by_user_id", "creation_request_id", "last_request_id" }
             .Select(c => $"{f.Records}.{f.Records}_{c}_not_null n -"))
         .Concat(new[] { "id", f.RecordColumn, "status", "started_at_utc", "started_by_user_id", "updated_at_utc", "updated_by_user_id" }
             .Select(c => $"{f.Revisions}.{f.Revisions}_{c}_not_null n -"))),
        "measurement_revisions.fk_measurement_revisions_measured_by_users f r",
    ];

    private static readonly string[] AuditRelations =
    [
        "events kind=r", "pk_events kind=i", "ix_events_actor_user_id kind=i", "ix_events_client_occurred kind=i", "ix_events_entity kind=i",
    ];

    // Aucune valeur personnelle en colonne : champs modifiés en jsonb (politique de masquage) ; identifiants techniques.
    private static readonly string[] AuditColumnsExpected =
    [
        "events.action character varying(64) notnull=t",
        "events.actor_user_id uuid notnull=t",
        "events.changes jsonb notnull=f",
        "events.client_id uuid notnull=f",
        "events.entity_id uuid notnull=t",
        "events.entity_type character varying(32) notnull=t",
        "events.id uuid notnull=t",
        "events.module character varying(32) notnull=t",
        "events.occurred_at_utc timestamp with time zone notnull=t",
        "events.session_id uuid notnull=f",
        "events.trace_id character varying(64) notnull=f",
    ];

    private static readonly string[] AuditConstraintsExpected =
    [
        "events.ck_events_action_format c -",
        "events.ck_events_changes_array c -",
        "events.ck_events_entity_type_format c -",
        "events.ck_events_module_format c -",
        "events.fk_events_actor_users f r",
        "events.fk_events_clients f r",
        "events.pk_events p -",
        .. new[] { "id", "occurred_at_utc", "actor_user_id", "module", "action", "entity_type", "entity_id" }.Select(c => $"events.events_{c}_not_null n -"),
    ];

    private static readonly string[] AuditIndexesExpected =
    [
        "ix_events_actor_user_id unique=f",
        "ix_events_client_occurred unique=f",
        "ix_events_entity unique=f",
        "pk_events unique=t",
    ];

    private static readonly string[] AppointmentsRelations =
    [
        "appointments kind=r", "pk_appointments kind=i", "responsibles kind=r", "pk_responsibles kind=i",
        "ix_appointments_cancelled_by_user_id kind=i", "ix_appointments_client_starts_at kind=i",
        "ix_appointments_created_by_user_id kind=i", "ix_appointments_responsible_planned kind=i",
        "ix_appointments_starts_at kind=i", "ix_appointments_unassigned_planned kind=i", "ix_appointments_updated_by_user_id kind=i",
    ];

    // Aucune coordonnée client, aucune donnée de santé, aucune colonne de version (xmin système).
    private static readonly string[] AppointmentsColumnsExpected =
    [
        "appointments.cancellation_reason character varying(200) notnull=f",
        "appointments.cancelled_at_utc timestamp with time zone notnull=f",
        "appointments.cancelled_by_user_id uuid notnull=f",
        "appointments.client_id uuid notnull=t",
        "appointments.created_at_utc timestamp with time zone notnull=t",
        "appointments.created_by_user_id uuid notnull=t",
        "appointments.ends_at_utc timestamp with time zone notnull=t",
        "appointments.id uuid notnull=t",
        "appointments.notes character varying(1000) notnull=f",
        "appointments.reason_code character varying(32) notnull=t",
        "appointments.responsible_user_id uuid notnull=f",
        "appointments.starts_at_utc timestamp with time zone notnull=t",
        "appointments.status character varying(16) notnull=t",
        "appointments.updated_at_utc timestamp with time zone notnull=t",
        "appointments.updated_by_user_id uuid notnull=t",
        "responsibles.created_at_utc timestamp with time zone notnull=t",
        "responsibles.is_eligible boolean notnull=t",
        "responsibles.updated_at_utc timestamp with time zone notnull=t",
        "responsibles.user_id uuid notnull=t",
    ];

    private static readonly string[] AppointmentsConstraintsExpected =
    [
        "appointments.ck_appointments_cancellation_consistency c -",
        "appointments.ck_appointments_cancellation_reason_planned c -",
        "appointments.ck_appointments_duration_range c -",
        "appointments.ck_appointments_ends_after_starts c -",
        "appointments.ck_appointments_reason_code_format c -",
        "appointments.ck_appointments_status c -",
        "appointments.ck_appointments_updated_after_created c -",
        "appointments.fk_appointments_cancelled_by_users f r",
        "appointments.fk_appointments_clients f r",
        "appointments.fk_appointments_created_by_users f r",
        "appointments.fk_appointments_responsible_users f r",
        "appointments.fk_appointments_updated_by_users f r",
        "appointments.pk_appointments p -",
        "responsibles.ck_responsibles_updated_after_created c -",
        "responsibles.fk_responsibles_users f r",
        "responsibles.pk_responsibles p -",
        // PostgreSQL 18 : NOT NULL catalogués dans pg_constraint, noms générés <table>_<colonne>_not_null.
        .. new[] { "id", "client_id", "starts_at_utc", "ends_at_utc", "reason_code", "status", "created_at_utc", "created_by_user_id", "updated_at_utc", "updated_by_user_id" }
            .Select(c => $"appointments.appointments_{c}_not_null n -"),
        .. new[] { "user_id", "is_eligible", "created_at_utc", "updated_at_utc" }
            .Select(c => $"responsibles.responsibles_{c}_not_null n -"),
    ];

    private static readonly string[] AppointmentsIndexesExpected =
    [
        "ix_appointments_cancelled_by_user_id unique=f",
        "ix_appointments_client_starts_at unique=f",
        "ix_appointments_created_by_user_id unique=f",
        "ix_appointments_responsible_planned unique=f",
        "ix_appointments_starts_at unique=f",
        "ix_appointments_unassigned_planned unique=f",
        "ix_appointments_updated_by_user_id unique=f",
        "pk_appointments unique=t",
        "pk_responsibles unique=t",
    ];

    private static readonly string[] ClientsRelations =
    [
        "clients kind=r", "pk_clients kind=i", "client_reference_seq kind=S", "ux_clients_reference kind=i",
        "ix_clients_active_name kind=i", "ix_clients_created_at kind=i",
        "ix_clients_created_by_user_id kind=i", "ix_clients_updated_by_user_id kind=i",
    ];

    // Aucune colonne de sécurité sociale, mutuelle ni donnée de santé ; aucune colonne de version (xmin système).
    private static readonly string[] ClientsColumnsExpected =
    [
        "clients.address_line character varying(200) notnull=f",
        "clients.archived_at_utc timestamp with time zone notnull=f",
        "clients.birth_date date notnull=f",
        "clients.city character varying(100) notnull=f",
        "clients.created_at_utc timestamp with time zone notnull=t",
        "clients.created_by_user_id uuid notnull=t",
        "clients.email character varying(254) notnull=f",
        "clients.first_name character varying(100) notnull=t",
        "clients.id uuid notnull=t",
        "clients.last_name character varying(100) notnull=t",
        "clients.notes character varying(1000) notnull=f",
        "clients.phone character varying(32) notnull=f",
        "clients.postal_code character varying(10) notnull=f",
        "clients.reference character varying(24) notnull=t",
        "clients.search_text character varying(700) notnull=t",
        "clients.updated_at_utc timestamp with time zone notnull=t",
        "clients.updated_by_user_id uuid notnull=t",
    ];

    private static readonly string[] ClientsConstraintsExpected =
    [
        "clients.ck_clients_archived_after_created c -",
        "clients.ck_clients_birth_date_range c -",
        "clients.ck_clients_first_name_not_blank c -",
        "clients.ck_clients_last_name_not_blank c -",
        "clients.ck_clients_reference_format c -",
        "clients.ck_clients_updated_after_created c -",
        "clients.fk_clients_created_by_users f r",
        "clients.fk_clients_updated_by_users f r",
        "clients.pk_clients p -",
        // PostgreSQL 18 : NOT NULL catalogués dans pg_constraint, noms générés <table>_<colonne>_not_null.
        .. new[] { "id", "reference", "first_name", "last_name", "search_text", "created_at_utc", "created_by_user_id", "updated_at_utc", "updated_by_user_id" }
            .Select(c => $"clients.clients_{c}_not_null n -"),
    ];

    private static readonly string[] ClientsIndexesExpected =
    [
        "ix_clients_active_name unique=f",
        "ix_clients_created_at unique=f",
        "ix_clients_created_by_user_id unique=f",
        "ix_clients_updated_by_user_id unique=f",
        "pk_clients unique=t",
        "ux_clients_reference unique=t",
    ];

    public override string ToString() => string.Join(
        Environment.NewLine,
        $"transaction_read_only={TransactionReadOnly} public_owner={PublicSchemaOwner}",
        Section("schémas hors public", ExtraSchemas),
        Section("relations", Relations),
        Section("routines", Routines),
        Section("types", Types),
        Section("extensions", Extensions),
        Section("triggers/règles/politiques/ACL par défaut", Decorations),
        Section("colonnes __EFMigrationsHistory", HistoryColumns),
        Section("contraintes __EFMigrationsHistory", HistoryConstraints),
        Section("lignes __EFMigrationsHistory", HistoryRows),
        Section("colonnes auth", AuthColumns),
        Section("contraintes auth", AuthConstraints),
        Section("index auth", AuthIndexes),
        Section("colonnes clients", ClientsColumns),
        Section("contraintes clients", ClientsConstraints),
        Section("index clients", ClientsIndexes),
        Section("séquences clients", ClientsSequences),
        Section("colonnes appointments", AppointmentsColumns),
        Section("contraintes appointments", AppointmentsConstraints),
        Section("index appointments", AppointmentsIndexes),
        Section("colonnes audit", AuditColumns),
        Section("contraintes audit", AuditConstraints),
        Section("index audit", AuditIndexes),
        Section("colonnes optical", OpticalColumns),
        Section("contraintes optical", OpticalConstraints),
        Section("index optical", OpticalIndexes),
        Section("séquences optical", OpticalSequences));

    private static string Section(string name, IReadOnlyList<string> items) =>
        $"{name} ({items.Count}) : " + (items.Count == 0 ? "—" : string.Join(" | ", items));

    private static void Expect(List<string> differences, string name, IReadOnlyList<string> actual, string[] expected)
    {
        var sortedExpected = expected.Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(sortedExpected, StringComparer.Ordinal))
        {
            differences.Add($"{name} : attendu [{string.Join(" | ", sortedExpected)}], obtenu [{string.Join(" | ", actual)}]");
        }
    }
}

public static class SchemaInspection
{
    private const string UserNamespaces =
        "n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname !~ '^pg_(toast|temp)'";

    public static async Task<SchemaState> ReadAsync(PostgreSqlTarget target, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(target.ReadOnlyConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await ReadAsync(connection, cancellationToken);
    }

    /// <summary>Inventaire relu sur une connexion existante (03-D2 : dans la transaction annulée d'une simulation de migration).</summary>
    public static async Task<SchemaState> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {

        var historyExists = (bool)(await ScalarAsync(
            connection, "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL", cancellationToken))!;

        return new SchemaState(
            TransactionReadOnly: (string)(await ScalarAsync(
                connection, "SELECT current_setting('transaction_read_only')", cancellationToken))!,
            PublicSchemaOwner: (string)(await ScalarAsync(
                connection, "SELECT pg_get_userbyid(nspowner)::text FROM pg_namespace WHERE nspname = 'public'", cancellationToken))!,
            ExtraSchemas: await ListAsync(connection, """
                SELECT nspname::text FROM pg_namespace
                WHERE nspname NOT IN ('public', 'information_schema') AND nspname !~ '^pg_'
                """, cancellationToken),
            Relations: await ListAsync(connection, $"""
                SELECT format('%s.%s kind=%s owner=%s acl=%s', n.nspname, c.relname, c.relkind,
                              pg_get_userbyid(c.relowner), coalesce(c.relacl::text, 'default'))
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE {UserNamespaces}
                """, cancellationToken),
            Routines: await ListAsync(connection, $"""
                SELECT format('%s.%s', n.nspname, p.proname)
                FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE {UserNamespaces}
                """, cancellationToken),
            Types: await ListAsync(connection, $"""
                SELECT format('%s.%s typtype=%s', n.nspname, t.typname, t.typtype)
                FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                WHERE {UserNamespaces}
                  AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.reltype = t.oid)
                  AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.reltype = t.typelem)
                """, cancellationToken),
            Extensions: await ListAsync(connection,
                "SELECT extname::text FROM pg_extension WHERE extname <> 'plpgsql'", cancellationToken),
            Decorations: await ListAsync(connection, $"""
                SELECT format('trigger %s on %s', tgname, tgrelid::regclass) FROM pg_trigger WHERE NOT tgisinternal
                UNION ALL SELECT format('policy %s on %s', polname, polrelid::regclass) FROM pg_policy
                UNION ALL SELECT format('rule %s on %s', r.rulename, r.ev_class::regclass)
                          FROM pg_rewrite r JOIN pg_class c ON c.oid = r.ev_class
                          JOIN pg_namespace n ON n.oid = c.relnamespace WHERE {UserNamespaces}
                UNION ALL SELECT format('default_acl %s', defaclacl) FROM pg_default_acl
                UNION ALL SELECT format('event_trigger %s', evtname) FROM pg_event_trigger
                """, cancellationToken),
            HistoryColumns: historyExists
                ? await ListAsync(connection, """
                    SELECT format('%s %s notnull=%s', attname, format_type(atttypid, atttypmod), attnotnull)
                    FROM pg_attribute
                    WHERE attrelid = 'public."__EFMigrationsHistory"'::regclass AND attnum > 0 AND NOT attisdropped
                    """, cancellationToken)
                : [],
            HistoryConstraints: historyExists
                ? await ListAsync(connection, """
                    SELECT format('%s %s', conname, pg_get_constraintdef(oid))
                    FROM pg_constraint WHERE conrelid = 'public."__EFMigrationsHistory"'::regclass
                    """, cancellationToken)
                : [],
            HistoryRows: historyExists
                ? await ListAsync(connection,
                    "SELECT format('%s %s', \"MigrationId\", \"ProductVersion\") FROM public.\"__EFMigrationsHistory\"",
                    cancellationToken)
                : [],
            AuthColumns: await ListAsync(connection, ColumnsSql("auth"), cancellationToken),
            AuthConstraints: await ListAsync(connection, ConstraintsSql("auth"), cancellationToken),
            AuthIndexes: await ListAsync(connection, IndexesSql("auth"), cancellationToken),
            ClientsColumns: await ListAsync(connection, ColumnsSql("clients"), cancellationToken),
            ClientsConstraints: await ListAsync(connection, ConstraintsSql("clients"), cancellationToken),
            ClientsIndexes: await ListAsync(connection, IndexesSql("clients"), cancellationToken),
            ClientsSequences: await ListAsync(connection, SequencesSql("clients"), cancellationToken),
            AppointmentsColumns: await ListAsync(connection, ColumnsSql("appointments"), cancellationToken),
            AppointmentsConstraints: await ListAsync(connection, ConstraintsSql("appointments"), cancellationToken),
            AppointmentsIndexes: await ListAsync(connection, IndexesSql("appointments"), cancellationToken),
            AuditColumns: await ListAsync(connection, ColumnsSql("audit"), cancellationToken),
            AuditConstraints: await ListAsync(connection, ConstraintsSql("audit"), cancellationToken),
            AuditIndexes: await ListAsync(connection, IndexesSql("audit"), cancellationToken),
            OpticalColumns: await ListAsync(connection, ColumnsSql("optical"), cancellationToken),
            OpticalConstraints: await ListAsync(connection, ConstraintsSql("optical"), cancellationToken),
            OpticalIndexes: await ListAsync(connection, IndexesSql("optical"), cancellationToken),
            OpticalSequences: await ListAsync(connection, SequencesSql("optical"), cancellationToken));
    }

    private static string SequencesSql(string schema) => $"""
        SELECT format('%s type=%s start=%s increment=%s min=%s max=%s cycle=%s owned_by=%s', c.relname,
                      format_type(s.seqtypid, NULL), s.seqstart, s.seqincrement, s.seqmin, s.seqmax,
                      CASE WHEN s.seqcycle THEN 't' ELSE 'f' END,
                      coalesce((SELECT d.refobjid::regclass::text FROM pg_depend d
                                WHERE d.objid = c.oid AND d.classid = 'pg_class'::regclass AND d.deptype = 'a'), '-'))
        FROM pg_sequence s JOIN pg_class c ON c.oid = s.seqrelid JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '{schema}'
        """;

    private static string ColumnsSql(string schema) => $"""
        SELECT format('%s.%s %s notnull=%s', c.relname, a.attname, format_type(a.atttypid, a.atttypmod),
                      CASE WHEN a.attnotnull THEN 't' ELSE 'f' END)
        FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '{schema}' AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
        """;

    private static string ConstraintsSql(string schema) => $"""
        SELECT format('%s.%s %s %s', c.relname, k.conname, k.contype,
                      CASE WHEN k.contype = 'f' THEN k.confdeltype::text ELSE '-' END)
        FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '{schema}'
        """;

    private static string IndexesSql(string schema) => $"""
        SELECT format('%s unique=%s', ic.relname, CASE WHEN i.indisunique THEN 't' ELSE 'f' END)
        FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid JOIN pg_namespace n ON n.oid = ic.relnamespace
        WHERE n.nspname = '{schema}'
        """;

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>Résultat trié en ordinal côté client : comparaison indépendante de la collation serveur.</summary>
    private static async Task<IReadOnlyList<string>> ListAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        var items = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetString(0));
        }

        items.Sort(StringComparer.Ordinal);
        return items;
    }
}
