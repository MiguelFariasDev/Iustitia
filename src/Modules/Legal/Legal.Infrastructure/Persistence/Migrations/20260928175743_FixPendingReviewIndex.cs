using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingReviewIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_pending_review",
                table: "publications");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_pending_review",
                table: "publications",
                columns: new[] { "tenant_id", "published_at" },
                filter: "status = 'PendingReview' AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_pending_review",
                table: "publications");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_pending_review",
                table: "publications",
                columns: new[] { "tenant_id", "process_id" },
                filter: "status = 'PendingReview' AND is_deleted = false");
        }
    }
}
