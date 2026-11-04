using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts;
using Advocacia.Platform.Domain.Alerts.Events;
using Advocacia.Platform.Infrastructure.Messaging.Consumers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.Consumers;

public sealed class AlertCreatedConsumerTests
{
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly IProcessedEventStore _processed = Substitute.For<IProcessedEventStore>();

    private AlertCreatedConsumer CreateSut() => new(
        _realtime, _processed, Substitute.For<IAppMetrics>(),
        NullLogger<ConsumerBase<AlertCreatedEvent>>.Instance, NullLogger<AlertCreatedConsumer>.Instance);

    private static ConsumeContext<AlertCreatedEvent> ContextFor(AlertCreatedEvent message)
    {
        var context = Substitute.For<ConsumeContext<AlertCreatedEvent>>();
        context.Message.Returns(message);

        return context;
    }

    [Fact]
    public async Task Consume_BroadcastsToTheUsersPrivateChannel()
    {
        _processed.HasBeenProcessedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var message = new AlertCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AlertType.Publication, "Nova publicação", "Processo X", Guid.NewGuid(), DateTimeOffset.UtcNow);

        await CreateSut().Consume(ContextFor(message));

        await _realtime.Received(1).BroadcastAsync(
            $"tenant:{message.TenantId}:alerts:{message.UserId}",
            RealtimeChannels.AlertCreatedEvent,
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WhenAlreadyProcessed_DoesNotBroadcastAgain()
    {
        _processed.HasBeenProcessedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var message = new AlertCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AlertType.Publication, "t", null, null, DateTimeOffset.UtcNow);

        await CreateSut().Consume(ContextFor(message));

        await _realtime.DidNotReceiveWithAnyArgs().BroadcastAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Consume_WhenTheBroadcastFails_PropagatesSoTheBusRetries()
    {
        _processed.HasBeenProcessedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _realtime.BroadcastAsync(default!, default!, default!, default).ReturnsForAnyArgs(Task.FromException(new HttpRequestException("503")));
        var message = new AlertCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AlertType.Publication, "t", null, null, DateTimeOffset.UtcNow);

        var act = async () => await CreateSut().Consume(ContextFor(message));

        await act.Should().ThrowAsync<HttpRequestException>();
        await _processed.DidNotReceiveWithAnyArgs().MarkAsProcessedAsync(default, default!, default);
    }
}
