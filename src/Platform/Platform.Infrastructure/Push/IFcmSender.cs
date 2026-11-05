using FirebaseAdmin.Messaging;

namespace Advocacia.Platform.Infrastructure.Push;

/// <summary>
/// Costura fina sobre o <c>FirebaseMessaging</c> (que não é mockável): devolve o RESULTADO do envio
/// em vez de lançar, e é o serviço quem decide o que fazer com cada resultado — o que dá para testar
/// sem rede nem Firebase.
/// </summary>
public interface IFcmSender
{
    Task<FcmSendOutcome> SendAsync(Message message, CancellationToken cancellationToken);
}

public enum FcmSendOutcome
{
    Sent,

    /// <summary>O FCM não reconhece o token (app desinstalado ou token expirado): remover o dispositivo.</summary>
    Unregistered,

    Failed,
}
