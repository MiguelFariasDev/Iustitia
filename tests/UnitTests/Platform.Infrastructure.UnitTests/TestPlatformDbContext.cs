using Advocacia.Platform.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.Infrastructure.UnitTests;

/// <summary>DbContext mínimo (EF InMemory), sem os filtros globais de tenant/soft-delete do BaseDbContext real, só para testar jobs isoladamente.</summary>
public sealed class TestPlatformDbContext(DbContextOptions<TestPlatformDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(builder =>
            builder.Property(log => log.Id).HasConversion(id => id.Value, value => AuditLogId.From(value)));
    }

    public static TestPlatformDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestPlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestPlatformDbContext(options);
    }
}
