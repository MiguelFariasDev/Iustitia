using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts.Events;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Messaging.Consumers;

/// <summary>
/// Recebe AlertCreatedEvent (via outbox) e empurra o alerta ao navegador do usuário pelo
/// canal Realtime <c>tenant:{tenantId}:alerts:{userId}</c> e, depois, ao celular (push). O
/// Realtime é o que propaga falha: o MassTransit retenta e, esgotadas as tentativas, o alerta
/// continua visível pelo refetch do sino. O push NÃO propaga: se falhasse depois do Realtime, a
/// retentativa reenviaria o alerta ao navegador em duplicidade — e o push é só conveniência.
/// </summary>
public sealed class AlertCreatedConsumer(
    IRealtimeNotifier realtime,
    IPushNotificationService push,
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

        await TryPushAsync(message, cancellationToken);
    }

    private async Task TryPushAsync(AlertCreatedEvent message, CancellationToken cancellationToken)
    {
        try
        {
            // A rota é o painel: o evento só traz o id da publicação, e é o app que resolve o
            // processo dela (Platform não conhece Legal). No painel o item já está lá.
            var result = await push.SendToUserAsync(
                new PushMessage(message.TenantId, message.UserId, message.Title, message.Description, "/dashboard"),
                cancellationToken);

            if (result.Sent + result.Failed + result.Removed > 0)
            {
                logger.LogInformation(
                    "Push do alerta {AlertId}: {Sent} enviado(s), {Failed} falha(s), {Removed} removido(s).",
                    message.AlertId, result.Sent, result.Failed, result.Removed);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nem a mensagem da exceção: pode conter o token.
            logger.LogWarning("Push do alerta {AlertId} não enviado ({ExceptionType}).", message.AlertId, ex.GetType().Name);
        }
    }
}
