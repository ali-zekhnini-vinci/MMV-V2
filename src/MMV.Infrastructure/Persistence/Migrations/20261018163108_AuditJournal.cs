using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
// Code généré par dotnet ef (tableaux de colonnes d'index composites) : CA1861 sans objet ici.
#pragma warning disable CA1861

namespace MMV.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 0005 — journal d'audit métier (phase 03-D2) : schéma <c>audit</c>, table <c>audit.events</c> en ajout seul
    /// (déclencheurs ajoutés à la main ci-dessous : toute modification, suppression ou troncature est refusée, SQLSTATE
    /// 42501), permission <c>audit.read</c> admise par la contrainte des permissions. Aucune donnée existante n'est
    /// lue ni reprise : l'historique commence à l'application de cette migration (rien n'est reconstitué).
    /// </summary>
    public partial class AuditJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "events",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.CheckConstraint("ck_events_action_format", "action ~ '^[a-z][a-z_]{0,30}\\.[a-z][a-z_]{0,31}$'");
                    table.CheckConstraint("ck_events_changes_array", "changes IS NULL OR jsonb_typeof(changes) = 'array'");
                    table.CheckConstraint("ck_events_entity_type_format", "entity_type ~ '^[a-z][a-z_]{0,31}$'");
                    table.CheckConstraint("ck_events_module_format", "module ~ '^[a-z][a-z_]{0,31}$'");
                    table.ForeignKey(
                        name: "fk_events_actor_users",
                        column: x => x.actor_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_events_clients",
                        column: x => x.client_id,
                        principalSchema: "clients",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Ajout seul (écrit à la main, hors modèle EF) : le journal ne se modifie ni ne se supprime par l'application.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.reject_event_mutation() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION 'audit.events : journal en ajout seul (% refusé)', TG_OP USING ERRCODE = '42501';
                END;
                $fn$;
                """);
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_events_append_only BEFORE UPDATE OR DELETE ON audit.events FOR EACH ROW EXECUTE FUNCTION audit.reject_event_mutation();");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_events_no_truncate BEFORE TRUNCATE ON audit.events FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_event_mutation();");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions",
                sql: "permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel', 'audit.read')");

            migrationBuilder.CreateIndex(
                name: "ix_events_actor_user_id",
                schema: "audit",
                table: "events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_client_occurred",
                schema: "audit",
                table: "events",
                columns: new[] { "client_id", "occurred_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_events_entity",
                schema: "audit",
                table: "events",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retour arrière : DÉTRUIT le journal (non exécuté par l'agent ; revue du propriétaire obligatoire).
            migrationBuilder.DropTable(
                name: "events",
                schema: "audit");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions",
                sql: "permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive', 'appointments.read', 'appointments.write', 'appointments.cancel')");

            migrationBuilder.Sql("DROP FUNCTION audit.reject_event_mutation();");
        }
    }
}
