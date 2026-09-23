using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.Platform.Infrastructure.Jobs.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.Jobs;

public sealed class OutboxCleanupJobTests
{
    [Fact]
    public async Task RunAsync_CallsOutboxCleanupService()
    {
        var cleanupService = Substitute.For<IOutboxCleanupService>();
        cleanupService.CleanupAsync(Arg.Any<CancellationToken>()).Returns(3);

        var sut = new OutboxCleanupJob(cleanupService, NullLogger<OutboxCleanupJob>.Instance);

        await sut.RunAsync(CancellationToken.None);

        await cleanupService.Received(1).CleanupAsync(Arg.Any<CancellationToken>());
    }
}
