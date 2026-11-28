using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OtimizaIndicesConsultas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_publications_cnj_number",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_pending_review",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_published_on",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_status",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_unread",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_processos_tenant_area_status",
                table: "processos");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_cliente_pub",
                table: "publications",
                columns: new[] { "tenant_id", "cliente_id", "published_on", "captured_at" },
                descending: new[] { false, false, true, true },
                filter: "is_deleted = false AND cliente_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_published_cap",
                table: "publications",
                columns: new[] { "tenant_id", "published_on", "captured_at" },
                descending: new[] { false, true, true },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_responsavel_pub",
                table: "publications",
                columns: new[] { "tenant_id", "responsavel_user_id", "published_on", "captured_at" },
                descending: new[] { false, false, true, true },
                filter: "is_deleted = false AND responsavel_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_status_published",
                table: "publications",
                columns: new[] { "tenant_id", "status", "published_on", "captured_at" },
                descending: new[] { false, false, true, true },
                filter: "is_deleted = false")
                .Annotation("Npgsql:IndexInclude", new[] { "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_processos_tenant_status_area_created",
                table: "processos",
                columns: new[] { "tenant_id", "status", "area", "created_at" },
                descending: new[] { false, false, false, true },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_cliente_pub",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_published_cap",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_responsavel_pub",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_status_published",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_processos_tenant_status_area_created",
                table: "processos");

            migrationBuilder.CreateIndex(
                name: "ix_publications_cnj_number",
                table: "publications",
                column: "cnj_number");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_pending_review",
                table: "publications",
                columns: new[] { "tenant_id", "published_at" },
                filter: "status = 'PendingReview' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_published_on",
                table: "publications",
                columns: new[] { "tenant_id", "published_on" },
                descending: new[] { false, true });

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
                name: "ix_processos_tenant_area_status",
                table: "processos",
                columns: new[] { "tenant_id", "area", "status" });
        }
    }
}
