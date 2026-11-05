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

public class AlertCreatedConsumerTests
{
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly IProcessedEventStore _store = Substitute.For<IProcessedEventStore>();

    private static AlertCreatedEvent Event() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AlertType.Publication,
        "Nova publicação: intimação", "Processo 5001001-10.2026.8.26.0001 — TJSP", Guid.NewGuid(), DateTimeOffset.UtcNow);

    private AlertCreatedConsumer CreateSut()
    {
        _store.HasBeenProcessedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        return new AlertCreatedConsumer(
            _realtime, _push, _store, Substitute.For<IAppMetrics>(),
            NullLogger<ConsumerBase<AlertCreatedEvent>>.Instance, NullLogger<AlertCreatedConsumer>.Instance);
    }

    private static ConsumeContext<AlertCreatedEvent> Context(AlertCreatedEvent message)
    {
        var context = Substitute.For<ConsumeContext<AlertCreatedEvent>>();
        context.Message.Returns(message);
        return context;
    }

    [Fact]
    public async Task Consume_BroadcastsToRealtime_ThenPushesToTheUsersDevices()
    {
        var message = Event();

        await CreateSut().Consume(Context(message));

        await _realtime.Received(1).BroadcastAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _push.Received(1).SendToUserAsync(
            Arg.Is<PushMessage>(p =>
                p.TenantId == message.TenantId && p.UserId == message.UserId
                && p.Title == message.Title && p.Body == message.Description && p.Route == "/dashboard"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WhenPushThrows_StillSucceeds_SoRealtimeIsNotResent()
    {
        // Se o push propagasse a falha, o MassTransit retentaria o evento inteiro e o alerta seria
        // reenviado ao navegador em duplicidade.
        _push.SendToUserAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns<PushSendResult>(_ => throw new InvalidOperationException("fcm fora do ar"));

        var act = () => CreateSut().Consume(Context(Event()));

        await act.Should().NotThrowAsync();
        await _realtime.Received(1).BroadcastAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WhenRealtimeFails_DoesNotPush_AndThePipelineRetries()
    {
        _realtime.BroadcastAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("realtime fora do ar")));

        var act = () => CreateSut().Consume(Context(Event()));

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _push.DidNotReceiveWithAnyArgs().SendToUserAsync(default!, default);
    }
}
