using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cnj_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    raw_content = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_publications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_publications_cnj_number",
                table: "publications",
                column: "cnj_number");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_pending_review",
                table: "publications",
                columns: new[] { "tenant_id", "process_id" },
                filter: "status = 'PendingReview' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_process_published",
                table: "publications",
                columns: new[] { "tenant_id", "process_id", "published_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_status",
                table: "publications",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_unread",
                table: "publications",
                columns: new[] { "tenant_id", "is_read" },
                filter: "is_read = false AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_publications_tenant_external_id",
                table: "publications",
                columns: new[] { "tenant_id", "external_id", "source" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "publications");
        }
    }
}
