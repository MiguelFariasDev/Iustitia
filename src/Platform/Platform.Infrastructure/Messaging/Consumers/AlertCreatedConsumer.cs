using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts.Events;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Messaging.Consumers;

/// <summary>
/// Recebe AlertCreatedEvent (via outbox) e empurra o alerta ao navegador do usuário pelo
/// canal Realtime <c>tenant:{tenantId}:alerts:{userId}</c>. Falha propaga: o MassTransit
/// retenta e, esgotadas as tentativas, o alerta continua visível pelo refetch do sino.
/// </summary>
public sealed class AlertCreatedConsumer(
    IRealtimeNotifier realtime,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<AlertCreatedEvent>> baseLogger,
    ILogger<AlertCreatedConsumer> logger)
    : ConsumerBase<AlertCreatedEvent>(processedEventStore, metrics, baseLogger)
{
    protected override async Task HandleAsync(AlertCreatedEvent message, CancellationToken cancellationToken)
    {
        await realtime.BroadcastAsync(
            RealtimeChannels.Alerts(message.TenantId, message.UserId),
            RealtimeChannels.AlertCreatedEvent,
            new
            {
                alertId = message.AlertId,
                type = message.Type.ToString(),
                title = message.Title,
                description = message.Description,
                referenceId = message.ReferenceId,
                createdAt = message.CreatedAt,
            },
            cancellationToken);

        logger.LogInformation(
            "Alerta {AlertId} enviado ao canal Realtime do tenant {TenantId}.", message.AlertId, message.TenantId);
    }
}
