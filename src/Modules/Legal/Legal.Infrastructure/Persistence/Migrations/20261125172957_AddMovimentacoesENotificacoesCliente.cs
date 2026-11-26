using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMovimentacoesENotificacoesCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificacoes_cliente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    quantidade_andamentos = table.Column<int>(type: "integer", nullable: false),
                    andamentos_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revisado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revisado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    falha_envio_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacoes_cliente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processo_movimentacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    fonte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    complemento = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    data_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    instancia = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    possui_documento = table.Column<bool>(type: "boolean", nullable: false),
                    capturada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processo_movimentacoes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_cliente_tenant_status_criacao",
                table: "notificacoes_cliente",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_notificacoes_cliente_pendente_por_processo",
                table: "notificacoes_cliente",
                columns: new[] { "tenant_id", "processo_id" },
                unique: true,
                filter: "status = 'PendenteRevisao'");

            migrationBuilder.CreateIndex(
                name: "ix_processo_movimentacoes_tenant_processo_capturada",
                table: "processo_movimentacoes",
                columns: new[] { "tenant_id", "processo_id", "capturada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_processo_movimentacoes_tenant_processo_data",
                table: "processo_movimentacoes",
                columns: new[] { "tenant_id", "processo_id", "data_hora" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_processo_movimentacoes_tenant_processo_external",
                table: "processo_movimentacoes",
                columns: new[] { "tenant_id", "processo_id", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacoes_cliente");

            migrationBuilder.DropTable(
                name: "processo_movimentacoes");
        }
    }
}
