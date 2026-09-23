using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.Jobs;

public sealed class HealthCheckJobTests
{
    [Fact]
    public async Task RunAsync_WhenRedisHealthyAndOutboxBelowThreshold_DoesNotLogWarning()
    {
        using var dbContext = TestPlatformDbContext.Create();
        var cacheService = Substitute.For<ICacheService>();
        var outboxProcessor = Substitute.For<IOutboxProcessor>();
        outboxProcessor.GetPendingCountAsync(Arg.Any<CancellationToken>()).Returns(10);
        var logger = Substitute.For<ILogger<HealthCheckJob>>();

        var sut = new HealthCheckJob(dbContext, cacheService, outboxProcessor, logger);

        await sut.RunAsync(CancellationToken.None);

        logger.DidNotReceive().Log(
            LogLevel.Warning, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception?>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RunAsync_WhenOutboxPendingCountExceedsThreshold_LogsWarning()
    {
        using var dbContext = TestPlatformDbContext.Create();
        var cacheService = Substitute.For<ICacheService>();
        var outboxProcessor = Substitute.For<IOutboxProcessor>();
        outboxProcessor.GetPendingCountAsync(Arg.Any<CancellationToken>()).Returns(1500);

        var sut = new HealthCheckJob(dbContext, cacheService, outboxProcessor, NullLogger<HealthCheckJob>.Instance);

        var act = () => sut.RunAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunAsync_WhenRedisThrows_LogsCriticalButDoesNotThrow()
    {
        using var dbContext = TestPlatformDbContext.Create();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.SetAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Redis indisponível"));
        var outboxProcessor = Substitute.For<IOutboxProcessor>();
        outboxProcessor.GetPendingCountAsync(Arg.Any<CancellationToken>()).Returns(0);

        var sut = new HealthCheckJob(dbContext, cacheService, outboxProcessor, NullLogger<HealthCheckJob>.Instance);

        var act = () => sut.RunAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
