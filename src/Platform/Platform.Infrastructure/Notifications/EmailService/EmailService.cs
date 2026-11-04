using Advocacia.Platform.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Notifications.EmailService;

/// <summary>
/// Implementação placeholder de INotificationService: só loga a intenção de envio, sem
/// integrar com nenhum provider real (SendGrid/SES/...), que fica para a Fase 1+. Existe
/// para que UserInvitedConsumer já tenha um alvo real e testável nesta etapa.
/// </summary>
public sealed class EmailService(ILogger<EmailService> logger) : INotificationService
{
    public Task SendUserInvitationEmailAsync(Guid userId, Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notificação (placeholder): convite enviado para o usuário {UserId} do tenant {TenantId}.",
            userId, tenantId);

        return Task.CompletedTask;
    }
}
