using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Push;

/// <summary>Push desligado (sem credenciais do Firebase): não envia nada e diz isso uma vez por envio, sem dados do usuário.</summary>
public sealed class NoopPushNotificationService(ILogger<NoopPushNotificationService> logger) : IPushNotificationService
{
    public Task<PushSendResult> SendToUserAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Push não configurado (Push:Fcm sem credenciais): nenhuma notificação enviada.");

        return Task.FromResult(PushSendResult.None);
    }
}
