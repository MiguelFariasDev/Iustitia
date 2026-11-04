using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.Infrastructure.UnitTests;

/// <summary>DbContext mínimo (EF InMemory), sem os filtros globais de tenant/soft-delete do BaseDbContext real, só para testar jobs/serviços isoladamente.</summary>
public sealed class TestPlatformDbContext(DbContextOptions<TestPlatformDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(builder =>
            builder.Property(log => log.Id).HasConversion(id => id.Value, value => AuditLogId.From(value)));

        modelBuilder.Entity<FeatureFlag>(builder =>
        {
            builder.Property(flag => flag.Id).HasConversion(id => id.Value, value => FeatureFlagId.From(value));
            builder.Property(flag => flag.Key).HasConversion(key => key.Value, value => FeatureFlagKey.Create(value).Value);
        });
    }

    public static TestPlatformDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestPlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestPlatformDbContext(options);
    }
}
