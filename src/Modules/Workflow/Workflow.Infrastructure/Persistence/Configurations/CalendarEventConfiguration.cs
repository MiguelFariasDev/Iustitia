using Advocacia.Modules.Workflow.Domain.Agenda;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Configurations;

/// <summary>Filtro global de tenant aplicado por BaseDbContext. RLS em infra/supabase/migrations/015_workflow.sql.</summary>
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
        builder.Property(calendarEvent => calendarEvent.OwnerUserId).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Title).HasMaxLength(CalendarEvent.TitleMaxLength).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Description).HasMaxLength(CalendarEvent.DescriptionMaxLength);
        builder.Property(calendarEvent => calendarEvent.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(calendarEvent => calendarEvent.StartsAt).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Location).HasMaxLength(CalendarEvent.LocationMaxLength);
        builder.Property(calendarEvent => calendarEvent.CreatedAt).IsRequired();

        // A agenda é sempre "os eventos deste usuário num intervalo de tempo".
        builder.HasIndex(calendarEvent => new { calendarEvent.TenantId, calendarEvent.OwnerUserId, calendarEvent.StartsAt })
            .HasDatabaseName("ix_calendar_events_tenant_owner_starts");

        builder.HasIndex(calendarEvent => calendarEvent.ProcessoId).HasDatabaseName("ix_calendar_events_processo");
    }
}
