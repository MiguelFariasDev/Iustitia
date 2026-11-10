using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Auditing.Events;
using Microsoft.Extensions.Logging;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.Platform.Infrastructure.Auditing.AuditService;

/// <summary>
/// Grava audit_logs diretamente via DbContext (não via IRepository{T,TId}: AuditLog não é
/// AggregateRoot — é um registro imutável, ver Platform.Domain.Auditing.AuditLog). Faz seu
/// próprio SaveChangesAsync, separado da transação do handler chamador — falha ao gravar
/// auditoria nunca deve derrubar a operação de negócio principal, só é logada como erro.
/// Publica AuditLogCreatedEvent via mensageria diretamente (não via Outbox, já que AuditLog
/// não é AggregateRoot) best-effort: falha ao publicar nunca desfaz o registro já persistido.
/// </summary>
public sealed class AuditService(
    EfDbContext dbContext,
    ICurrentUser currentUser,
    IRequestOrigin requestOrigin,
    IDateTimeProvider dateTimeProvider,
    IIntegrationEventPublisher integrationEventPublisher,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task RecordAsync(
        AuditAction action,
        string entityType,
        string entityId,
        object? before = null,
        object? after = null,
        Guid? performedByUserId = null,
        Guid? tenantId = null,
        ErrorCode? errorCode = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = tenantId ?? currentUser.TenantId;
        if (effectiveTenantId is null)
        {
            logger.LogWarning(
                "Auditoria não registrada: nenhum tenant disponível (ação {Action} em {EntityType}/{EntityId})",
                action, entityType, entityId);
            return;
        }

        // audit_logs não tem uma coluna própria para o código de erro de uma ação que
        // falhou — errorCode é gravado dentro do próprio "after" (ver ADR-032).
        var effectiveAfter = errorCode is null ? after : new { ErrorCode = errorCode.Value.ToString(), After = after };

        var auditLogResult = AuditLog.Create(
            effectiveTenantId.Value,
            performedByUserId ?? currentUser.UserId ?? Guid.Empty,
            action,
            entityType,
            entityId,
            before is null ? null : JsonSerializer.Serialize(before),
            effectiveAfter is null ? null : JsonSerializer.Serialize(effectiveAfter),
            requestOrigin.IpAddress,
            requestOrigin.UserAgent,
            dateTimeProvider.UtcNow);

        if (auditLogResult.IsFailure)
        {
            logger.LogWarning(
                "Auditoria não registrada: {ErrorCode} (ação {Action} em {EntityType}/{EntityId})",
                auditLogResult.Error.Code, action, entityType, entityId);
            return;
        }

        dbContext.Set<AuditLog>().Add(auditLogResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await integrationEventPublisher.PublishAsync(
                new AuditLogCreatedEvent(
                    auditLogResult.Value.Id.Value,
                    effectiveTenantId.Value,
                    performedByUserId ?? currentUser.UserId ?? Guid.Empty,
                    action,
                    entityType,
                    entityId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao publicar AuditLogCreatedEvent para o audit log {AuditLogId}.", auditLogResult.Value.Id.Value);
        }
    }
}
