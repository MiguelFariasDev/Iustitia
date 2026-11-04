using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Platform.Domain.Alerts.Events;

/// <summary>
/// Um alerta foi criado para um usuário. Vai pela outbox (mesma transação do alerta — ADR-008);
/// o consumer <c>AlertCreatedConsumer</c> o repassa ao canal Realtime do usuário.
/// Carrega só o necessário para a notificação in-app, nunca conteúdo de publicação.
/// </summary>
public sealed record AlertCreatedEvent(
    Guid AlertId,
    Guid TenantId,
    Guid UserId,
    AlertType Type,
    string Title,
    string? Description,
    Guid? ReferenceId,
    DateTimeOffset CreatedAt) : DomainEvent;

public sealed record AlertReadEvent(Guid AlertId, Guid TenantId, Guid UserId) : DomainEvent;
