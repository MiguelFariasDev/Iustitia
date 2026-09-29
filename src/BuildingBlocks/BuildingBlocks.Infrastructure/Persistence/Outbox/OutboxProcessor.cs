using System.Diagnostics;
using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Lê mensagens pendentes da outbox, resolve o tipo concreto do evento (via
/// <see cref="IEventTypeResolver"/>), desserializa o payload e publica via MassTransit
/// (<see cref="IPublishEndpoint"/>) — que roteia para Azure Service Bus/RabbitMQ conforme a
/// configuração de mensageria (ver MassTransitConfiguration). Falhas de publicação usam
/// backoff exponencial (NextRetryAt) até esgotar MaxRetries, quando a mensagem vira
/// definitivamente Failed. Nunca loga o payload completo (pode conter dados sensíveis).
/// </summary>
public sealed class OutboxProcessor(
    EfDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    IEventTypeResolver eventTypeResolver,
    IOptions<OutboxProcessorOptions> options,
    IAppMetrics metrics,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    private static readonly ActivitySource ActivitySource = new(ActivitySourceNames.Outbox);

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("outbox.process_batch");
        var stopwatch = Stopwatch.StartNew();

        var settings = options.Value;
        var now = DateTimeOffset.UtcNow;

        var pendingMessages = await dbContext.Set<OutboxMessage>()
            .Where(message => message.Status == OutboxStatus.Pending
                && (message.NextRetryAt == null || message.NextRetryAt <= now))
            .OrderBy(message => message.CreatedAt)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        var failedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                var eventType = eventTypeResolver.Resolve(message.EventType);
                var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType)
                    ?? throw new InvalidOperationException($"Payload da mensagem {message.Id} desserializou como nulo.");

                await publishEndpoint.Publish(domainEvent, eventType, publishContext =>
                {
                    if (message.CorrelationId is { } correlationId)
                    {
                        publishContext.CorrelationId = correlationId;
                    }
                }, cancellationToken);

                message.MarkAsProcessed(DateTimeOffset.UtcNow);
                processedCount++;

                logger.LogInformation(
                    "Outbox: mensagem {MessageId} ({EventType}) publicada com sucesso.",
                    message.Id, message.EventType);
            }
            catch (Exception ex)
            {
                var backoffSeconds = settings.BackoffBaseSeconds * Math.Pow(2, message.RetryCount);
                var nextRetryAt = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);

                message.MarkAsFailed(ex.Message, nextRetryAt, settings.MaxRetries);
                failedCount++;

                logger.LogError(
                    ex,
                    "Outbox: falha ao publicar mensagem {MessageId} ({EventType}) — tentativa {RetryCount}/{MaxRetries}.",
                    message.Id, message.EventType, message.RetryCount, settings.MaxRetries);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        stopwatch.Stop();
        metrics.RecordOutboxProcessed(processedCount);
        metrics.RecordOutboxFailed(failedCount);
        metrics.RecordOutboxProcessingDuration(stopwatch.Elapsed);
        activity?.SetTag("outbox.batch_size", pendingMessages.Count);
        activity?.SetTag("outbox.processed_count", processedCount);
        activity?.SetTag("outbox.failed_count", failedCount);

        return processedCount;
    }

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<OutboxMessage>().CountAsync(message => message.Status == OutboxStatus.Pending, cancellationToken);
}
