using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
// Code généré par dotnet ef (tableaux de colonnes d'index composites) : CA1861 sans objet ici.
#pragma warning disable CA1861

namespace MMV.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions");

            migrationBuilder.EnsureSchema(
                name: "clients");

            migrationBuilder.CreateSequence(
                name: "client_reference_seq",
                schema: "clients",
                maxValue: 999999999999999999L);

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValueSql: "'CLI-' || to_char(nextval('clients.client_reference_seq'), 'FM999999999999000000')"),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    address_line = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    search_text = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                    table.CheckConstraint("ck_clients_archived_after_created", "archived_at_utc IS NULL OR archived_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_clients_birth_date_range", "birth_date IS NULL OR birth_date >= DATE '1900-01-01'");
                    table.CheckConstraint("ck_clients_first_name_not_blank", "btrim(first_name) <> ''");
                    table.CheckConstraint("ck_clients_last_name_not_blank", "btrim(last_name) <> ''");
                    table.CheckConstraint("ck_clients_reference_format", "reference ~ '^CLI-[0-9]{6,18}$'");
                    table.CheckConstraint("ck_clients_updated_after_created", "updated_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "fk_clients_created_by_users",
                        column: x => x.created_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clients_updated_by_users",
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
                sql: "permission IN ('system.admin', 'clients.read', 'clients.write', 'clients.archive')");

            migrationBuilder.CreateIndex(
                name: "ix_clients_active_name",
                schema: "clients",
                table: "clients",
                columns: new[] { "last_name", "first_name", "id" },
                filter: "archived_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_clients_created_at",
                schema: "clients",
                table: "clients",
                columns: new[] { "created_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_clients_created_by_user_id",
                schema: "clients",
                table: "clients",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_clients_updated_by_user_id",
                schema: "clients",
                table: "clients",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_clients_reference",
                schema: "clients",
                table: "clients",
                column: "reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clients",
                schema: "clients");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions");

            migrationBuilder.DropSequence(
                name: "client_reference_seq",
                schema: "clients");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_permissions_permission",
                schema: "auth",
                table: "user_permissions",
                sql: "permission IN ('system.admin')");
        }
    }
}
