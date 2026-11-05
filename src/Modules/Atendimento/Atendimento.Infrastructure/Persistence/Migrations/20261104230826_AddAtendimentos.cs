using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAtendimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "atendimentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phone_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atendimentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bot_api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bot_api_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "atendimento_auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atendimento_auditoria", x => x.id);
                    table.ForeignKey(
                        name: "fk_atendimento_auditoria_atendimentos_atendimento_id",
                        column: x => x.atendimento_id,
                        principalTable: "atendimentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "atendimento_mensagens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    delivery = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atendimento_mensagens", x => x.id);
                    table.ForeignKey(
                        name: "fk_atendimento_mensagens_atendimentos_atendimento_id",
                        column: x => x.atendimento_id,
                        principalTable: "atendimentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_atendimento_auditoria_atendimento",
                table: "atendimento_auditoria",
                columns: new[] { "atendimento_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_atendimento_mensagens_conversa",
                table: "atendimento_mensagens",
                columns: new[] { "atendimento_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_atendimento_mensagens_external_id",
                table: "atendimento_mensagens",
                columns: new[] { "tenant_id", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_atendimentos_tenant_assigned",
                table: "atendimentos",
                columns: new[] { "tenant_id", "assigned_user_id" },
                filter: "assigned_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_atendimentos_tenant_client",
                table: "atendimentos",
                columns: new[] { "tenant_id", "client_id" },
                filter: "client_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_atendimentos_tenant_status_last_message",
                table: "atendimentos",
                columns: new[] { "tenant_id", "status", "last_message_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_atendimentos_open_phone",
                table: "atendimentos",
                columns: new[] { "tenant_id", "phone_number" },
                unique: true,
                filter: "status <> 'Closed'");

            migrationBuilder.CreateIndex(
                name: "ix_bot_api_keys_tenant",
                table: "bot_api_keys",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_bot_api_keys_hash",
                table: "bot_api_keys",
                column: "key_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atendimento_auditoria");

            migrationBuilder.DropTable(
                name: "atendimento_mensagens");

            migrationBuilder.DropTable(
                name: "bot_api_keys");

            migrationBuilder.DropTable(
                name: "atendimentos");
        }
    }
}
