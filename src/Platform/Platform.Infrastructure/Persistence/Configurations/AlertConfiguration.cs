using Advocacia.Platform.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>Filtro global de tenant aplicado por BaseDbContext. RLS em infra/supabase/migrations/013_alerts.sql.</summary>
public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.Id)
            .HasConversion(id => id.Value, value => AlertId.From(value))
            .ValueGeneratedNever();

        builder.Property(alert => alert.TenantId).IsRequired();
        builder.Property(alert => alert.UserId).IsRequired();

        builder.Property(alert => alert.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(alert => alert.Title).HasMaxLength(Alert.TitleMaxLength).IsRequired();
        builder.Property(alert => alert.Description).HasColumnType("text");
        builder.Property(alert => alert.IsRead).IsRequired().HasDefaultValue(false);
        builder.Property(alert => alert.CreatedAt).IsRequired();

        builder.HasIndex(alert => new { alert.TenantId, alert.UserId, alert.IsRead })
            .HasDatabaseName("ix_alerts_tenant_user_read");

        builder.HasIndex(alert => new { alert.TenantId, alert.Type, alert.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_alerts_tenant_type_created");

        builder.HasIndex(alert => alert.ReferenceId).HasDatabaseName("ix_alerts_reference");
    }
}
