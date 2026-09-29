using System.Diagnostics;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Observability;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Advocacia.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Classe base para consumers de MassTransit (ver ADR-016). Cobre, para qualquer
/// <typeparamref name="TMessage"/> (evento de domínio publicado via outbox ou evento de
/// integração), os requisitos transversais exigidos pelo prompt de mensageria:
///   - Idempotência: cada mensagem só é processada uma vez por consumer (EventId +
///     nome do consumer — ver <see cref="IProcessedEventStore"/>, ADR-035).
///   - Logging estruturado com correlation id, sem nunca logar o payload completo.
///   - Span (ActivitySourceNames.Consumers) e métricas (consumers.processed/failed.count —
///     ver ADR-036) por mensagem consumida.
///   - Erros não tratados sobem para o MassTransit aplicar retry/dead-letter (ver
///     MassTransitConfiguration) — nunca são engolidos aqui.
/// </summary>
public abstract class ConsumerBase<TMessage>(
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<TMessage>> logger) : IConsumer<TMessage>
    where TMessage : class, IDomainEvent
{
    private static readonly ActivitySource ActivitySource = new(ActivitySourceNames.Consumers);

    public async Task Consume(ConsumeContext<TMessage> context)
    {
        var message = context.Message;
        var consumerName = GetType().Name;

        using var activity = ActivitySource.StartActivity($"consume {consumerName}");
        activity?.SetTag("messaging.message_id", message.EventId);
        activity?.SetTag("messaging.consumer", consumerName);

        if (await processedEventStore.HasBeenProcessedAsync(message.EventId, consumerName, context.CancellationToken))
        {
            logger.LogDebug(
                "Evento {EventId} ({EventType}) já processado por {Consumer}, ignorando (idempotência).",
                message.EventId, typeof(TMessage).Name, consumerName);
            return;
        }

        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["EventId"] = message.EventId,
            ["EventType"] = typeof(TMessage).Name,
            ["Consumer"] = consumerName,
            ["CorrelationId"] = context.CorrelationId ?? Guid.NewGuid(),
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await HandleAsync(message, context.CancellationToken);
            await processedEventStore.MarkAsProcessedAsync(message.EventId, consumerName, context.CancellationToken);
            metrics.RecordConsumerProcessed(consumerName, success: true);

            logger.LogInformation(
                "Consumer {Consumer} processou {EventType} ({EventId}) em {ElapsedMs}ms.",
                consumerName, typeof(TMessage).Name, message.EventId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            metrics.RecordConsumerProcessed(consumerName, success: false);
            logger.LogError(
                ex,
                "Consumer {Consumer} falhou ao processar {EventType} ({EventId}).",
                consumerName, typeof(TMessage).Name, message.EventId);
            throw;
        }
    }

    protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
