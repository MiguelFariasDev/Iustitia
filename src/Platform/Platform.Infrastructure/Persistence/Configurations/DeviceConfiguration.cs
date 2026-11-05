using Advocacia.Platform.Domain.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>Filtro global de tenant aplicado por BaseDbContext. RLS em infra/supabase/migrations/014_devices.sql.</summary>
public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id)
            .HasConversion(id => id.Value, value => DeviceId.From(value))
            .ValueGeneratedNever();

        builder.Property(device => device.TenantId).IsRequired();
        builder.Property(device => device.UserId).IsRequired();

        builder.Property(device => device.Platform).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(device => device.DeviceToken).HasMaxLength(Device.TokenMaxLength).IsRequired();
        builder.Property(device => device.IsRevoked).IsRequired().HasDefaultValue(false);
        builder.Property(device => device.LastSeenAt).IsRequired();
        builder.Property(device => device.CreatedAt).IsRequired();

        // Um token por instalação do app, por escritório. É o que faz o registro ser idempotente:
        // o app registra a cada login e a cada renovação do token, e não pode duplicar a linha.
        builder.HasIndex(device => new { device.TenantId, device.DeviceToken })
            .IsUnique()
            .HasDatabaseName("ux_devices_tenant_token");

        // "Meus dispositivos" e o destino do push.
        builder.HasIndex(device => new { device.TenantId, device.UserId, device.IsRevoked })
            .HasDatabaseName("ix_devices_tenant_user_revoked");
    }
}
