using Advocacia.BuildingBlocks.Domain.Events;

namespace Advocacia.Platform.Domain.Auditing.Events;

/// <summary>
/// Evento de integração publicado pelo AuditService após gravar um AuditLog. AuditLog não é
/// AggregateRoot (é write-once — ver AuditLog.cs) então não passa pelo Outbox Pattern; é
/// publicado diretamente via IIntegrationEventPublisher, best-effort (falha aqui nunca
/// desfaz o registro de auditoria já persistido).
/// </summary>
public sealed record AuditLogCreatedEvent(
    Guid AuditLogIdValue,
    Guid TenantIdValue,
    Guid UserId,
    AuditAction Action,
    string EntityType,
    string EntityId) : IntegrationEvent(TenantIdValue);
