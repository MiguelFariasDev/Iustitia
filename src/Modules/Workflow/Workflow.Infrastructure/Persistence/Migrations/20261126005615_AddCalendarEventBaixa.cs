using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEventBaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_calendar_events_pending_reminders",
                table: "calendar_events");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "calendar_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completed_by_user_id",
                table: "calendar_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_pending_reminders",
                table: "calendar_events",
                column: "start_at",
                filter: "is_deleted = false AND assigned_user_id IS NOT NULL AND completed_at IS NULL AND (one_day_reminder_sent_at IS NULL OR one_hour_reminder_sent_at IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_calendar_events_pending_reminders",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "completed_by_user_id",
                table: "calendar_events");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_pending_reminders",
                table: "calendar_events",
                column: "start_at",
                filter: "is_deleted = false AND assigned_user_id IS NOT NULL AND (one_day_reminder_sent_at IS NULL OR one_hour_reminder_sent_at IS NULL)");
        }
    }
}
