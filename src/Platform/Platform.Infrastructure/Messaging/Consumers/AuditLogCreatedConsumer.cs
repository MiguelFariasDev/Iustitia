using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Domain.Auditing.Events;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Messaging.Consumers;

/// <summary>
/// Recebe AuditLogCreatedEvent. Indexação em sistema de busca fica para fase futura — por
/// ora, só confirma o recebimento via log estruturado (sem logar Before/After, que podem
/// conter dados sensíveis).
/// </summary>
public sealed class AuditLogCreatedConsumer(
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<IntegrationEventConsumer<AuditLogCreatedEvent>> baseLogger,
    ILogger<AuditLogCreatedConsumer> logger)
    : IntegrationEventConsumer<AuditLogCreatedEvent>(processedEventStore, metrics, baseLogger)
{
    protected override Task HandleIntegrationEventAsync(AuditLogCreatedEvent message, Guid tenantId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Audit log {AuditLogId} ({Action} em {EntityType}/{EntityId}) recebido para o tenant {TenantId}.",
            message.AuditLogIdValue, message.Action, message.EntityType, message.EntityId, tenantId);

        return Task.CompletedTask;
    }
}
