using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicationSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "publication_suggestions");
        }
    }
}
