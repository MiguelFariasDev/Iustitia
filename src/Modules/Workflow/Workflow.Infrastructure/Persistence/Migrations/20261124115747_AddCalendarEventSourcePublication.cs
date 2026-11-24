using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEventSourcePublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_publication_id",
                table: "calendar_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_calendar_events_tenant_source_publication",
                table: "calendar_events",
                columns: new[] { "tenant_id", "source_publication_id" },
                unique: true,
                filter: "is_deleted = false AND source_publication_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_calendar_events_tenant_source_publication",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "source_publication_id",
                table: "calendar_events");
        }
    }
}
