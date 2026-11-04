using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientesEProcessos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_busca = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento_busca = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnj_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cnj_digits = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsavel_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tribunal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    comarca = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    vara = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    assunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    parte_contraria = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    valor_causa = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    data_distribuicao = table.Column<DateOnly>(type: "date", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clientes_tenant_ativo",
                table: "clientes",
                columns: new[] { "tenant_id", "is_active" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_tenant_nome",
                table: "clientes",
                columns: new[] { "tenant_id", "nome_busca" });

            migrationBuilder.CreateIndex(
                name: "ux_clientes_tenant_documento",
                table: "clientes",
                columns: new[] { "tenant_id", "documento" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_processos_tenant_area_status",
                table: "processos",
                columns: new[] { "tenant_id", "area", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_processos_tenant_captura",
                table: "processos",
                columns: new[] { "tenant_id", "status" },
                filter: "status IN ('Ativo', 'Suspenso') AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_processos_tenant_cliente",
                table: "processos",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_processos_tenant_cnj_digits",
                table: "processos",
                columns: new[] { "tenant_id", "cnj_digits" });

            migrationBuilder.CreateIndex(
                name: "ix_processos_tenant_responsavel",
                table: "processos",
                columns: new[] { "tenant_id", "responsavel_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_processos_tenant_cnj",
                table: "processos",
                columns: new[] { "tenant_id", "cnj_number" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "processos");
        }
    }
}
