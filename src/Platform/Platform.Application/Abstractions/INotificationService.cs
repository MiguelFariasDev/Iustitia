namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Placeholder de envio de notificações (e-mail/push), consumido pelos consumers de
/// mensageria (ex.: UserInvitedConsumer). Implementação real de e-mail/push fica para a
/// Fase 1+ (módulo Notifications) — por ora, a implementação de infraestrutura só loga.
/// </summary>
public interface INotificationService
{
    Task SendUserInvitationEmailAsync(Guid userId, Guid tenantId, string email, CancellationToken cancellationToken = default);
}
