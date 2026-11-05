using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Push;

public sealed class FirebaseFcmSender(FirebaseMessaging messaging, ILogger<FirebaseFcmSender> logger) : IFcmSender
{
    public async Task<FcmSendOutcome> SendAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            await messaging.SendAsync(message, cancellationToken);
            return FcmSendOutcome.Sent;
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        {
            return FcmSendOutcome.Unregistered;
        }
        catch (FirebaseMessagingException ex)
        {
            // Só o código do erro: a mensagem da exceção pode ecoar o token de entrega.
            logger.LogWarning("O FCM recusou o envio: {ErrorCode}.", ex.MessagingErrorCode);
            return FcmSendOutcome.Failed;
        }
    }
}
