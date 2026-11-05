using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultaProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "atendimento_auditoria",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "reference_id",
                table: "atendimento_auditoria",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "atendimento_consulta_sessoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    blocked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    process_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atendimento_consulta_sessoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_atendimento_consulta_sessoes_atendimentos_atendimento_id",
                        column: x => x.atendimento_id,
                        principalTable: "atendimentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consulta_sessoes_atendimento",
                table: "atendimento_consulta_sessoes",
                columns: new[] { "atendimento_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_consulta_sessoes_cliente",
                table: "atendimento_consulta_sessoes",
                columns: new[] { "tenant_id", "client_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atendimento_consulta_sessoes");

            migrationBuilder.DropColumn(
                name: "reference_id",
                table: "atendimento_auditoria");

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "atendimento_auditoria",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
