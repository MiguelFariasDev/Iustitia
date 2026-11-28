using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OtimizaIndicesConsultas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_alerts_tenant_user_created",
                table: "alerts",
                columns: new[] { "tenant_id", "user_id", "created_at" },
                descending: new[] { false, false, true },
                filter: "dismissed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_tenant_user_unread",
                table: "alerts",
                columns: new[] { "tenant_id", "user_id" },
                filter: "is_read = false AND dismissed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_alerts_tenant_user_created",
                table: "alerts");

            migrationBuilder.DropIndex(
                name: "ix_alerts_tenant_user_unread",
                table: "alerts");
        }
    }
}
