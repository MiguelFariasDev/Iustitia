using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CasosAnexosRevisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "legal_document_requests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "avisos_json",
                table: "legal_document_requests",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "caso_id",
                table: "legal_document_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cliente_id",
                table: "legal_document_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consulta_json",
                table: "legal_document_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_fatos",
                table: "legal_document_requests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revisado_em",
                table: "legal_document_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revisado_por",
                table: "legal_document_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "texto_revisado",
                table: "legal_document_requests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_nome = table.Column<string>(type: "text", nullable: true),
                    usuario_id_responsavel = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    processo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_processo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_casos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "drafting_security_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hash_conteudo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drafting_security_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_document_citations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_document_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detalhe = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    ponto_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fonte_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    urn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    decisao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decidido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    decidido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_document_citations", x => x.id);
                    table.ForeignKey(
                        name: "fk_legal_document_citations_legal_document_requests_legal_docu",
                        column: x => x.legal_document_request_id,
                        principalTable: "legal_document_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "caso_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id_responsavel = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    formato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    chave_original = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    chave_markdown = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    restrito = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    confianca_ocr = table.Column<double>(type: "double precision", nullable: true),
                    tipo_documento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    data_documento = table.Column<DateOnly>(type: "date", nullable: true),
                    classificacao_json = table.Column<string>(type: "text", nullable: true),
                    paginas = table.Column<int>(type: "integer", nullable: false),
                    trechos = table.Column<int>(type: "integer", nullable: false),
                    requer_revisao_humana = table.Column<bool>(type: "boolean", nullable: false),
                    revisado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revisado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    injecao_suspeita = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    indexado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caso_documentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_caso_documentos_casos_caso_id",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "caso_membros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    concedido_por = table.Column<Guid>(type: "uuid", nullable: false),
                    concedido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caso_membros", x => x.id);
                    table.ForeignKey(
                        name: "fk_caso_membros_casos_caso_id",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "caso_documento_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    pagina = table.Column<int>(type: "integer", nullable: true),
                    secao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    texto = table.Column<string>(type: "text", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tokens = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    confianca = table.Column<double>(type: "double precision", nullable: true),
                    suspeito = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caso_documento_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_caso_documento_chunks_caso_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "caso_documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_legal_document_requests_tenant_caso",
                table: "legal_document_requests",
                columns: new[] { "tenant_id", "caso_id" });

            migrationBuilder.CreateIndex(
                name: "ix_caso_documento_chunks_tenant_caso",
                table: "caso_documento_chunks",
                columns: new[] { "tenant_id", "caso_id" });

            migrationBuilder.CreateIndex(
                name: "ux_caso_documento_chunks_doc_ordem",
                table: "caso_documento_chunks",
                columns: new[] { "documento_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_caso_documentos_tenant_caso_status",
                table: "caso_documentos",
                columns: new[] { "tenant_id", "caso_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_caso_documentos_caso_hash",
                table: "caso_documentos",
                columns: new[] { "caso_id", "hash_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_caso_membros_tenant_usuario",
                table: "caso_membros",
                columns: new[] { "tenant_id", "usuario_id" });

            migrationBuilder.CreateIndex(
                name: "ux_caso_membros_caso_usuario",
                table: "caso_membros",
                columns: new[] { "caso_id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_casos_tenant_cliente",
                table: "casos",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_casos_tenant_responsavel",
                table: "casos",
                columns: new[] { "tenant_id", "usuario_id_responsavel", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_drafting_security_events_tenant_data",
                table: "drafting_security_events",
                columns: new[] { "tenant_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_document_citations_request",
                table: "legal_document_citations",
                columns: new[] { "legal_document_request_id", "ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "caso_documento_chunks");

            migrationBuilder.DropTable(
                name: "caso_membros");

            migrationBuilder.DropTable(
                name: "drafting_security_events");

            migrationBuilder.DropTable(
                name: "legal_document_citations");

            migrationBuilder.DropTable(
                name: "caso_documentos");

            migrationBuilder.DropTable(
                name: "casos");

            migrationBuilder.DropIndex(
                name: "ix_legal_document_requests_tenant_caso",
                table: "legal_document_requests");

            migrationBuilder.DropColumn(
                name: "caso_id",
                table: "legal_document_requests");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                table: "legal_document_requests");

            migrationBuilder.DropColumn(
                name: "consulta_json",
                table: "legal_document_requests");

            migrationBuilder.DropColumn(
                name: "data_fatos",
                table: "legal_document_requests");

            migrationBuilder.DropColumn(
                name: "revisado_em",
                table: "legal_document_requests");

            migrationBuilder.DropColumn(
                name: "revisado_por",
                table: "legal_document_requests");

            migrationBuilder.DropColumn(
                name: "texto_revisado",
                table: "legal_document_requests");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "legal_document_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "avisos_json",
                table: "legal_document_requests",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
