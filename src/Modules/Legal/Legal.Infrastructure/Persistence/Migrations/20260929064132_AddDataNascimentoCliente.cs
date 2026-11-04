using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataNascimentoCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "data_nascimento",
                table: "clientes",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_nascimento",
                table: "clientes");
        }
    }
}
