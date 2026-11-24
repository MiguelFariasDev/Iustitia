namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Notificações por e-mail disparadas pelos consumers de mensageria.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// O convite em si (link de acesso) é enviado pelo Supabase Auth em <c>/auth/v1/invite</c>, com o
    /// SMTP do projeto Supabase — este método só registra o evento, para não mandar duas mensagens.
    /// </summary>
    Task SendUserInvitationEmailAsync(Guid userId, Guid tenantId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Avisa o usuário por e-mail de um alerta. Só o título vai na mensagem (sigilo profissional).
    /// Não envia se o tipo não está em <c>Email:AlertTypes</c>, se o usuário não existe ou está inativo.
    /// </summary>
    Task SendAlertEmailAsync(Guid userId, Guid tenantId, string alertType, string title, CancellationToken cancellationToken = default);
}
