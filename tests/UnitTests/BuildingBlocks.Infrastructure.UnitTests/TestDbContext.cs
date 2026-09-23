using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests;

/// <summary>DbContext mínimo (EF InMemory) só com as entidades de outbox/idempotência, para testar OutboxProcessor/OutboxCleanupService/ProcessedEventStore isoladamente.</summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>();
        modelBuilder.Entity<ProcessedEvent>();
    }

    public static TestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options);
    }
}
