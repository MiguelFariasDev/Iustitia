using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Financeiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesParcelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parcela",
                table: "lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recorrencia",
                table: "lancamentos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unica");

            migrationBuilder.AddColumn<Guid>(
                name: "serie_id",
                table: "lancamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_parcelas",
                table: "lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_tenant_serie",
                table: "lancamentos",
                columns: new[] { "tenant_id", "serie_id" },
                filter: "serie_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lancamentos_tenant_serie",
                table: "lancamentos");

            migrationBuilder.DropColumn(
                name: "parcela",
                table: "lancamentos");

            migrationBuilder.DropColumn(
                name: "recorrencia",
                table: "lancamentos");

            migrationBuilder.DropColumn(
                name: "serie_id",
                table: "lancamentos");

            migrationBuilder.DropColumn(
                name: "total_parcelas",
                table: "lancamentos");
        }
    }
}
