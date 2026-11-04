using Advocacia.Platform.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>Filtro global de isolamento por tenant aplicado centralmente por BaseDbContext.</summary>
public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasConversion(id => id.Value, value => FeatureFlagId.From(value))
            .ValueGeneratedNever();

        builder.Property(f => f.TenantId).IsRequired();

        builder.Property(f => f.Key)
            .HasConversion(key => key.Value, value => FeatureFlagKey.Create(value).Value)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.IsEnabled).IsRequired();
        builder.Property(f => f.UpdatedAt).IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.Key }).IsUnique().HasDatabaseName("ux_feature_flags_tenant_key");
    }
}
