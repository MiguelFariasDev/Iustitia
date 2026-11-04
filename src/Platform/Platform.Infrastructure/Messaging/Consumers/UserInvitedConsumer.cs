using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Identity.Events;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Messaging.Consumers;

/// <summary>Recebe UserInvitedEvent (via outbox) e dispara o e-mail de convite.</summary>
public sealed class UserInvitedConsumer(
    INotificationService notificationService,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<UserInvitedEvent>> logger)
    : ConsumerBase<UserInvitedEvent>(processedEventStore, metrics, logger)
{
    protected override Task HandleAsync(UserInvitedEvent message, CancellationToken cancellationToken) =>
        notificationService.SendUserInvitationEmailAsync(message.UserId.Value, message.TenantId, message.Email, cancellationToken);
}
