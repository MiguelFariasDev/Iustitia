using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts.Events;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Messaging.Consumers;

/// <summary>
/// Segundo consumer de <see cref="AlertCreatedEvent"/>, separado do push Realtime de propósito: se o
/// provedor de e-mail estiver fora, o retry do MassTransit reenvia só o e-mail — o sino do app não
/// recebe o mesmo alerta de novo (a idempotência é por consumer).
/// </summary>
public sealed class AlertEmailConsumer(
    INotificationService notifications,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<AlertCreatedEvent>> logger)
    : ConsumerBase<AlertCreatedEvent>(processedEventStore, metrics, logger)
{
    protected override Task HandleAsync(AlertCreatedEvent message, CancellationToken cancellationToken) =>
        notifications.SendAlertEmailAsync(message.UserId, message.TenantId, message.Type.ToString(), message.Title, cancellationToken);
}
