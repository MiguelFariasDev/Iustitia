using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Identity.Events;
using Advocacia.Platform.Infrastructure.Messaging.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.Consumers;

public sealed class UserInvitedConsumerTests
{
    [Fact]
    public async Task Consume_SendsInvitationEmail_WithMessageData()
    {
        var notificationService = Substitute.For<INotificationService>();
        var processedEventStore = Substitute.For<IProcessedEventStore>();
        processedEventStore.HasBeenProcessedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var sut = new UserInvitedConsumer(notificationService, processedEventStore, NullLogger<ConsumerBase<UserInvitedEvent>>.Instance);

        var message = new UserInvitedEvent(UserId.New(), Guid.NewGuid(), "advogado@escritorio.com.br");
        var context = Substitute.For<ConsumeContext<UserInvitedEvent>>();
        context.Message.Returns(message);

        await sut.Consume(context);

        await notificationService.Received(1).SendUserInvitationEmailAsync(
            message.UserId.Value, message.TenantId, message.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WhenAlreadyProcessed_DoesNotSendEmailAgain()
    {
        var notificationService = Substitute.For<INotificationService>();
        var processedEventStore = Substitute.For<IProcessedEventStore>();
        processedEventStore.HasBeenProcessedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var sut = new UserInvitedConsumer(notificationService, processedEventStore, NullLogger<ConsumerBase<UserInvitedEvent>>.Instance);

        var message = new UserInvitedEvent(UserId.New(), Guid.NewGuid(), "advogado@escritorio.com.br");
        var context = Substitute.For<ConsumeContext<UserInvitedEvent>>();
        context.Message.Returns(message);

        await sut.Consume(context);

        await notificationService.DidNotReceive().SendUserInvitationEmailAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
