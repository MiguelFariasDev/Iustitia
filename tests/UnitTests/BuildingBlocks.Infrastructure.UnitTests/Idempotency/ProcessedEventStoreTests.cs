using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Idempotency;

public sealed class ProcessedEventStoreTests
{
    [Fact]
    public async Task HasBeenProcessedAsync_WhenNeverMarked_ReturnsFalse()
    {
        using var dbContext = TestDbContext.Create();
        var sut = new ProcessedEventStore(dbContext);

        (await sut.HasBeenProcessedAsync(Guid.NewGuid(), "MeuConsumer")).Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ThenHasBeenProcessedAsync_ReturnsTrue()
    {
        using var dbContext = TestDbContext.Create();
        var sut = new ProcessedEventStore(dbContext);
        var eventId = Guid.NewGuid();

        await sut.MarkAsProcessedAsync(eventId, "MeuConsumer");

        (await sut.HasBeenProcessedAsync(eventId, "MeuConsumer")).Should().BeTrue();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_IsScopedPerConsumer()
    {
        using var dbContext = TestDbContext.Create();
        var sut = new ProcessedEventStore(dbContext);
        var eventId = Guid.NewGuid();

        await sut.MarkAsProcessedAsync(eventId, "ConsumerA");

        (await sut.HasBeenProcessedAsync(eventId, "ConsumerB")).Should().BeFalse();
    }
}
