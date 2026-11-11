using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generation_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disponivel_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reservado_por = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reservado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reservado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_queue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gpu_instance_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vast_instance_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    acao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status_anterior = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status_novo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sucesso = table.Column<bool>(type: "boolean", nullable: false),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gpu_instance_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "indexed_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    tribunal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    data_publicacao = table.Column<DateOnly>(type: "date", nullable: true),
                    url_origem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    visibilidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hash_conteudo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trechos = table.Column<int>(type: "integer", nullable: false),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    paginas_json = table.Column<string>(type: "jsonb", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    indexado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_indexed_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingestao_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    total_arquivos = table.Column<int>(type: "integer", nullable: false),
                    arquivos_processados = table.Column<int>(type: "integer", nullable: false),
                    arquivos_com_erro = table.Column<int>(type: "integer", nullable: false),
                    arquivos_ignorados = table.Column<int>(type: "integer", nullable: false),
                    arquivos_removidos = table.Column<int>(type: "integer", nullable: false),
                    commit_origem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    escopo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingestao_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_document_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_peca = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    fatos = table.Column<string>(type: "text", nullable: false),
                    pedidos = table.Column<string>(type: "text", nullable: false),
                    instrucoes_adicionais = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    etapa_atual = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    progresso = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    texto_gerado = table.Column<string>(type: "text", nullable: true),
                    modelo_utilizado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tempo_geracao_ms = table.Column<long>(type: "bigint", nullable: true),
                    tokens_entrada = table.Column<int>(type: "integer", nullable: true),
                    tokens_saida = table.Column<int>(type: "integer", nullable: true),
                    custo_estimado_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    request_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prompt_versao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    documentos_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    avisos_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_document_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "normas_juridicas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repo_path = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    urn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    titulo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    numero = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ano = table.Column<int>(type: "integer", nullable: true),
                    data = table.Column<DateOnly>(type: "date", nullable: true),
                    data_publicacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ementa = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    situacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    revogada = table.Column<bool>(type: "boolean", nullable: false),
                    versao_texto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    esfera = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tribunal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    area = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    assuntos = table.Column<string[]>(type: "text[]", nullable: false),
                    colecao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fonte_oficial_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    arquivo_markdown_path = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    versao_atual = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    data_ingestao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_ultima_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_normas_juridicas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_document_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_document_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    detalhe_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_document_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_legal_document_events_legal_document_requests_legal_documen",
                        column: x => x.legal_document_request_id,
                        principalTable: "legal_document_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "retrieved_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_document_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qdrant_point_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    documento_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    pagina = table.Column<int>(type: "integer", nullable: true),
                    trecho = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    metadados_json = table.Column<string>(type: "jsonb", nullable: true),
                    utilizada_na_geracao = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retrieved_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_retrieved_sources_legal_document_requests_legal_document_re",
                        column: x => x.legal_document_request_id,
                        principalTable: "legal_document_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "norma_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    norma_juridica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    pagina = table.Column<int>(type: "integer", nullable: true),
                    secao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    artigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    paragrafo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    inciso = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tokens = table.Column<int>(type: "integer", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    qdrant_point_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_ingestao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_norma_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_norma_chunks_normas_norma_juridica_id",
                        column: x => x.norma_juridica_id,
                        principalTable: "normas_juridicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "norma_versoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    norma_juridica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    versao_texto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    data_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    data_revogacao = table.Column<DateOnly>(type: "date", nullable: true),
                    situacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    arquivo_markdown_path = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    commit_origem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    substituida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_norma_versoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_norma_versoes_normas_juridicas_norma_juridica_id",
                        column: x => x.norma_juridica_id,
                        principalTable: "normas_juridicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_generation_queue_referencia",
                table: "generation_queue",
                column: "referencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_queue_status_disponivel",
                table: "generation_queue",
                columns: new[] { "status", "disponivel_em", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_gpu_instance_logs_criado",
                table: "gpu_instance_logs",
                column: "criado_em");

            migrationBuilder.CreateIndex(
                name: "ix_indexed_documents_tenant_created",
                table: "indexed_documents",
                columns: new[] { "tenant_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_indexed_documents_tenant_hash",
                table: "indexed_documents",
                columns: new[] { "tenant_id", "hash_conteudo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingestao_jobs_status_iniciado",
                table: "ingestao_jobs",
                columns: new[] { "status", "iniciado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_document_events_request_time",
                table: "legal_document_events",
                columns: new[] { "legal_document_request_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_document_requests_origem",
                table: "legal_document_requests",
                column: "request_origem_id",
                filter: "request_origem_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_legal_document_requests_tenant_status",
                table: "legal_document_requests",
                columns: new[] { "tenant_id", "status", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_document_requests_tenant_user_created",
                table: "legal_document_requests",
                columns: new[] { "tenant_id", "usuario_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_norma_chunks_hash",
                table: "norma_chunks",
                column: "hash_sha256");

            migrationBuilder.CreateIndex(
                name: "ux_norma_chunks_norma_versao_ordem",
                table: "norma_chunks",
                columns: new[] { "norma_juridica_id", "versao", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_norma_versoes_norma_numero",
                table: "norma_versoes",
                columns: new[] { "norma_juridica_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_normas_juridicas_colecao_status",
                table: "normas_juridicas",
                columns: new[] { "colecao", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_normas_juridicas_esfera_uf_municipio",
                table: "normas_juridicas",
                columns: new[] { "esfera", "uf", "municipio" });

            migrationBuilder.CreateIndex(
                name: "ix_normas_juridicas_tipo_numero_ano",
                table: "normas_juridicas",
                columns: new[] { "tipo", "numero", "ano" });

            migrationBuilder.CreateIndex(
                name: "ix_normas_juridicas_urn",
                table: "normas_juridicas",
                column: "urn");

            migrationBuilder.CreateIndex(
                name: "ux_normas_juridicas_repo_path",
                table: "normas_juridicas",
                column: "repo_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retrieved_sources_request",
                table: "retrieved_sources",
                column: "legal_document_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_retrieved_sources_tenant_documento",
                table: "retrieved_sources",
                columns: new[] { "tenant_id", "documento_origem_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generation_queue");

            migrationBuilder.DropTable(
                name: "gpu_instance_logs");

            migrationBuilder.DropTable(
                name: "indexed_documents");

            migrationBuilder.DropTable(
                name: "ingestao_jobs");

            migrationBuilder.DropTable(
                name: "legal_document_events");

            migrationBuilder.DropTable(
                name: "norma_chunks");

            migrationBuilder.DropTable(
                name: "norma_versoes");

            migrationBuilder.DropTable(
                name: "retrieved_sources");

            migrationBuilder.DropTable(
                name: "normas_juridicas");

            migrationBuilder.DropTable(
                name: "legal_document_requests");
        }
    }
}
