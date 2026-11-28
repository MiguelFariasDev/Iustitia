using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Financeiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OtimizaIndicesConsultas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lancamentos_tenant_status_tipo",
                table: "lancamentos");

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_tenant_tipo_status_valor",
                table: "lancamentos",
                columns: new[] { "tenant_id", "tipo", "status" },
                filter: "is_deleted = false")
                .Annotation("Npgsql:IndexInclude", new[] { "valor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lancamentos_tenant_tipo_status_valor",
                table: "lancamentos");

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_tenant_status_tipo",
                table: "lancamentos",
                columns: new[] { "tenant_id", "status", "tipo" });
        }
    }
}
