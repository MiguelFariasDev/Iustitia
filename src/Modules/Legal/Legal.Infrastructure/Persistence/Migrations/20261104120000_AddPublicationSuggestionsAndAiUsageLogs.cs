using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicationSuggestionsAndAiUsageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    cache_creation_input_tokens = table.Column<int>(type: "integer", nullable: false),
                    cache_read_input_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(12,6)", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usage_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "publication_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    act_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    suggested_deadline_days = table.Column<int>(type: "integer", nullable: true),
                    deadline_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    legal_basis = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<int>(type: "integer", nullable: false),
                    requires_manual_review = table.Column<bool>(type: "boolean", nullable: false),
                    content_truncated = table.Column<bool>(type: "boolean", nullable: false),
                    ai_provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ai_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    raw_response = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "fk_publication_suggestions_publications_publication_id",
                        column: x => x.publication_id,
                        principalTable: "publications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_reference",
                table: "ai_usage_logs",
                column: "reference_id",
                filter: "reference_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_tenant_created",
                table: "ai_usage_logs",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_publication_suggestions_created_at",
                table: "publication_suggestions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ux_publication_suggestions_publication",
                table: "publication_suggestions",
                column: "publication_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage_logs");

            migrationBuilder.DropTable(
                name: "publication_suggestions");
        }
    }
}
