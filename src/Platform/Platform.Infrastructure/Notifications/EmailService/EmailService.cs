using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.Notifications.EmailService;

/// <summary>
/// Implementação de INotificationService sobre <see cref="IEmailSender"/>. Roda dentro de consumers
/// (sem requisição HTTP), então fixa o tenant do evento antes de ler o usuário: o filtro global e o
/// RLS dependem dele.
/// </summary>
public sealed class EmailService(
    IEmailSender emailSender,
    IRepository<User, UserId> users,
    ITenantContext tenantContext,
    IOptions<EmailOptions> options,
    ILogger<EmailService> logger) : INotificationService
{
    public Task SendUserInvitationEmailAsync(Guid userId, Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Convite do usuário {UserId} (tenant {TenantId}) entregue pelo Supabase Auth; nenhum e-mail adicional.",
            userId, tenantId);

        return Task.CompletedTask;
    }

    public async Task SendAlertEmailAsync(
        Guid userId, Guid tenantId, string alertType, string title, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.AlertTypes.Contains(alertType, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        tenantContext.SetTenant(tenantId);
        var user = await users.GetByIdAsync(UserId.From(userId), cancellationToken);
        if (user is null || !user.IsActive)
        {
            logger.LogInformation("Alerta sem e-mail: usuário {UserId} inexistente ou inativo.", userId);

            return;
        }

        var link = string.IsNullOrWhiteSpace(settings.AppBaseUrl)
            ? "/alertas"
            : settings.AppBaseUrl.TrimEnd('/') + "/alertas";
        var (subject, html, text) = EmailTemplates.Alert(title, link);

        await emailSender.SendAsync(new EmailMessage(user.Email.Value, subject, html, text), cancellationToken);
    }
}
