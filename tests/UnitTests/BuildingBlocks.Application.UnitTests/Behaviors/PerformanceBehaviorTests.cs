using Advocacia.BuildingBlocks.Application.Behaviors;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.BuildingBlocks.Application.UnitTests.Behaviors;

public class PerformanceBehaviorTests
{
    private sealed record SampleQuery : IQuery<string>;

    private readonly TestLogger<PerformanceBehavior<SampleQuery, Result<string>>> _logger = new();

    private PerformanceBehavior<SampleQuery, Result<string>> CreateBehavior(int thresholdMilliseconds) =>
        new(_logger, Options.Create(new PerformanceBehaviorOptions { ThresholdMilliseconds = thresholdMilliseconds }));

    [Fact]
    public async Task Handle_WhenFasterThanThreshold_DoesNotLogWarning()
    {
        var behavior = CreateBehavior(thresholdMilliseconds: 5_000);

        var result = await behavior.Handle(new SampleQuery(), () => Task.FromResult(Result.Success("ok")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Handle_WhenSlowerThanThreshold_LogsWarning()
    {
        var behavior = CreateBehavior(thresholdMilliseconds: 0);

        await behavior.Handle(
            new SampleQuery(),
            async () =>
            {
                await Task.Delay(20);
                return Result.Success("ok");
            },
            CancellationToken.None);

        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("SampleQuery"));
    }

    [Fact]
    public async Task Handle_AlwaysReturnsTheHandlerResponseUnchanged()
    {
        var behavior = CreateBehavior(thresholdMilliseconds: 500);

        var result = await behavior.Handle(new SampleQuery(), () => Task.FromResult(Result.Success("valor-original")), CancellationToken.None);

        result.Value.Should().Be("valor-original");
    }
}
