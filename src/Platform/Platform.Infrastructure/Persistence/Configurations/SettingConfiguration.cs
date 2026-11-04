using Advocacia.Platform.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>Filtro global de isolamento por tenant aplicado centralmente por BaseDbContext.</summary>
public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("settings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => SettingId.From(value))
            .ValueGeneratedNever();

        builder.Property(s => s.TenantId).IsRequired();

        builder.Property(s => s.Key)
            .HasConversion(key => key.Value, value => SettingKey.Create(value).Value)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.ValueJson)
            .HasColumnName("value")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.Key }).IsUnique().HasDatabaseName("ux_settings_tenant_key");
    }
}
