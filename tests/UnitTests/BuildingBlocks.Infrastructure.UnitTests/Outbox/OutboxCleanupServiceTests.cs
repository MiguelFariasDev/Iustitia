using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Outbox;

public sealed class OutboxCleanupServiceTests
{
    private readonly OutboxProcessorOptions _options = new() { RetentionDays = 30 };

    private OutboxCleanupService CreateSut(TestDbContext dbContext) =>
        new(dbContext, Options.Create(_options), NullLogger<OutboxCleanupService>.Instance);

    [Fact]
    public async Task CleanupAsync_RemovesOldProcessedMessages_KeepsRecentOnes()
    {
        using var dbContext = TestDbContext.Create();

        var oldProcessed = new OutboxMessage(Guid.NewGuid(), "Old", "{}", DateTimeOffset.UtcNow.AddDays(-40));
        oldProcessed.MarkAsProcessed(DateTimeOffset.UtcNow.AddDays(-31));

        var recentProcessed = new OutboxMessage(Guid.NewGuid(), "Recent", "{}", DateTimeOffset.UtcNow.AddDays(-5));
        recentProcessed.MarkAsProcessed(DateTimeOffset.UtcNow.AddDays(-2));

        dbContext.Set<OutboxMessage>().AddRange(oldProcessed, recentProcessed);
        await dbContext.SaveChangesAsync();

        var removedCount = await CreateSut(dbContext).CleanupAsync();

        removedCount.Should().Be(1);
        (await dbContext.Set<OutboxMessage>().ToListAsync()).Should().ContainSingle(m => m.Id == recentProcessed.Id);
    }

    [Fact]
    public async Task CleanupAsync_RemovesOldFailedMessages_AfterDoubleRetention()
    {
        using var dbContext = TestDbContext.Create();

        var oldFailed = new OutboxMessage(Guid.NewGuid(), "OldFailed", "{}", DateTimeOffset.UtcNow.AddDays(-65));
        for (var i = 0; i < 10; i++)
        {
            oldFailed.MarkAsFailed("erro", DateTimeOffset.UtcNow, maxRetries: 1);
        }

        var recentFailed = new OutboxMessage(Guid.NewGuid(), "RecentFailed", "{}", DateTimeOffset.UtcNow.AddDays(-10));
        recentFailed.MarkAsFailed("erro", DateTimeOffset.UtcNow, maxRetries: 1);

        dbContext.Set<OutboxMessage>().AddRange(oldFailed, recentFailed);
        await dbContext.SaveChangesAsync();

        await CreateSut(dbContext).CleanupAsync();

        var remaining = await dbContext.Set<OutboxMessage>().ToListAsync();
        remaining.Should().ContainSingle(m => m.Id == recentFailed.Id);
    }

    [Fact]
    public async Task CleanupAsync_RemovesExpiredProcessedEvents()
    {
        using var dbContext = TestDbContext.Create();

        dbContext.Set<ProcessedEvent>().Add(new ProcessedEvent(Guid.NewGuid(), "SomeConsumer", DateTimeOffset.UtcNow.AddDays(-31)));
        var recent = new ProcessedEvent(Guid.NewGuid(), "SomeConsumer", DateTimeOffset.UtcNow.AddDays(-1));
        dbContext.Set<ProcessedEvent>().Add(recent);
        await dbContext.SaveChangesAsync();

        await CreateSut(dbContext).CleanupAsync();

        var remaining = await dbContext.Set<ProcessedEvent>().ToListAsync();
        remaining.Should().ContainSingle(p => p.Id == recent.Id);
    }
}
