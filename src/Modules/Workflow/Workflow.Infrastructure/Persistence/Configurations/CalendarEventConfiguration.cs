using Advocacia.Modules.Workflow.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events");

        builder.HasKey(calendarEvent => calendarEvent.Id);
        builder.Property(calendarEvent => calendarEvent.Id)
            .HasConversion(id => id.Value, value => CalendarEventId.From(value))
            .ValueGeneratedNever();

        builder.Property(calendarEvent => calendarEvent.TenantId).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Title).HasMaxLength(CalendarEvent.TitleMaxLength).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Description).HasMaxLength(4000);
        builder.Property(calendarEvent => calendarEvent.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(calendarEvent => calendarEvent.StartAt).IsRequired();
        builder.Property(calendarEvent => calendarEvent.EndAt).IsRequired();
        builder.Property(calendarEvent => calendarEvent.AllDay).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Location).HasMaxLength(300);

        builder.Property(calendarEvent => calendarEvent.CreatedAt).IsRequired();
        builder.Property(calendarEvent => calendarEvent.CreatedBy).HasMaxLength(100);
        builder.Property(calendarEvent => calendarEvent.UpdatedBy).HasMaxLength(100);
        builder.Property(calendarEvent => calendarEvent.DeletedBy).HasMaxLength(100);

        // Consulta principal da agenda: eventos de um escritório numa janela de datas.
        builder.HasIndex(calendarEvent => new { calendarEvent.TenantId, calendarEvent.StartAt })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_calendar_events_tenant_start");

        builder.HasIndex(calendarEvent => new { calendarEvent.TenantId, calendarEvent.ProcessId })
            .HasFilter("is_deleted = false AND process_id IS NOT NULL")
            .HasDatabaseName("ix_calendar_events_tenant_process");

        // Varredura do job de lembretes: só eventos com algum aviso ainda pendente.
        builder.HasIndex(calendarEvent => calendarEvent.StartAt)
            .HasFilter("is_deleted = false AND assigned_user_id IS NOT NULL AND (one_day_reminder_sent_at IS NULL OR one_hour_reminder_sent_at IS NULL)")
            .HasDatabaseName("ix_calendar_events_pending_reminders");
    }
}
