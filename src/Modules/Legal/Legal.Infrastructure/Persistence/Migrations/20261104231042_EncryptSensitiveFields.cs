using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EncryptSensitiveFields : Migration
    {
        // Só o SCHEMA (colunas mais largas para o texto cifrado; índice único movido para o índice
        // cego). Os dados existentes continuam em texto puro até rodar o backfill de criptografia
        // (ver docs/runbooks/field-encryption.md) — a leitura aceita legado enquanto
        // Encryption:AllowLegacyPlaintext=true. O Down só funciona com colunas ainda em texto
        // puro: valores cifrados não cabem nas colunas antigas.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_clientes_tenant_documento",
                table: "clientes");

            migrationBuilder.AlterColumn<string>(
                name: "telefone",
                table: "clientes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "clientes",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "documento_busca",
                table: "clientes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(14)",
                oldMaxLength: 14);

            migrationBuilder.AlterColumn<string>(
                name: "documento",
                table: "clientes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(14)",
                oldMaxLength: 14);

            migrationBuilder.CreateIndex(
                name: "ux_clientes_tenant_documento",
                table: "clientes",
                columns: new[] { "tenant_id", "documento_busca" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_clientes_tenant_documento",
                table: "clientes");

            migrationBuilder.AlterColumn<string>(
                name: "telefone",
                table: "clientes",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "clientes",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(600)",
                oldMaxLength: 600,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "documento_busca",
                table: "clientes",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "documento",
                table: "clientes",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "ux_clientes_tenant_documento",
                table: "clientes",
                columns: new[] { "tenant_id", "documento" },
                unique: true,
                filter: "is_deleted = false");
        }
    }
}
