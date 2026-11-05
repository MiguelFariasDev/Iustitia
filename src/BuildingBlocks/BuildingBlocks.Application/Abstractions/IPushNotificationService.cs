namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Porta para enviar push (FCM/APNs) a TODOS os dispositivos ativos de um usuário. Existe em
/// BuildingBlocks pelo mesmo motivo de <see cref="IAlertPublisher"/>: módulos de negócio não podem
/// depender de Platform, que é quem guarda os dispositivos (ver ADR-069).
///
/// Dispositivos revogados são ignorados. Falha de envio é registrada e <b>nunca lançada</b>: push
/// é conveniência — o alerta já está salvo e aparece no app.
/// </summary>
public interface IPushNotificationService
{
    Task<PushSendResult> SendToUserAsync(PushMessage message, CancellationToken cancellationToken = default);
}

/// <param name="TenantId">Escritório do destinatário.</param>
/// <param name="UserId">Usuário que recebe (em todos os dispositivos ativos dele).</param>
/// <param name="Title">Título da notificação.</param>
/// <param name="Body">Texto. Nunca o inteiro teor de uma publicação: aparece na tela bloqueada.</param>
/// <param name="Route">Rota do app a abrir ao tocar (ex.: <c>/dashboard</c>); vai em <c>data.route</c>.</param>
public sealed record PushMessage(Guid TenantId, Guid UserId, string Title, string? Body, string? Route = null);

/// <param name="Sent">Dispositivos aceitos pelo FCM.</param>
/// <param name="Failed">Dispositivos em que o envio falhou.</param>
/// <param name="Removed">Dispositivos cujo token o FCM declarou inexistente (app desinstalado) e que foram removidos.</param>
public sealed record PushSendResult(int Sent, int Failed, int Removed)
{
    public static readonly PushSendResult None = new(0, 0, 0);
}
