using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAccountType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "cnpj",
                table: "tenants",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(14)",
                oldMaxLength: 14);

            migrationBuilder.AddColumn<string>(
                name: "account_type",
                table: "tenants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Firm");

            migrationBuilder.AddColumn<string>(
                name: "cpf",
                table: "tenants",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenants_cpf",
                table: "tenants",
                column: "cpf",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_tenants_cpf",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "account_type",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "cpf",
                table: "tenants");

            migrationBuilder.AlterColumn<string>(
                name: "cnpj",
                table: "tenants",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(14)",
                oldMaxLength: 14,
                oldNullable: true);
        }
    }
}
