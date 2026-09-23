using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.Jobs;

public sealed class OutboxProcessorJobTests
{
    [Fact]
    public async Task RunAsync_CallsOutboxProcessor()
    {
        var outboxProcessor = Substitute.For<IOutboxProcessor>();
        outboxProcessor.ProcessPendingMessagesAsync(Arg.Any<CancellationToken>()).Returns(5);

        var sut = new OutboxProcessorJob(outboxProcessor, NullLogger<OutboxProcessorJob>.Instance);

        await sut.RunAsync(CancellationToken.None);

        await outboxProcessor.Received(1).ProcessPendingMessagesAsync(Arg.Any<CancellationToken>());
    }
}
