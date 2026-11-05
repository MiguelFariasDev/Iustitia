using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Devices;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Push;

/// <summary>
/// Envia push pelo Firebase Cloud Messaging (que entrega no APNs para iOS). Só para dispositivos
/// <b>não revogados</b>.
///
/// Privacidade: o token de entrega NUNCA é logado — os logs identificam o dispositivo por
/// <c>DeviceId</c>. O texto vai como o do alerta (título e descrição, sem o inteiro teor da
/// publicação), porque aparece na tela bloqueada.
///
/// Token que o FCM declara inexistente (app desinstalado) é removido: mantê-lo só geraria uma falha
/// a cada envio.
/// </summary>
public sealed class FcmPushNotificationService(
    IDeviceRepository devices,
    IUnitOfWork unitOfWork,
    IFcmSender sender,
    ILogger<FcmPushNotificationService> logger) : IPushNotificationService
{
    private const string AndroidChannelId = "alertas";

    public async Task<PushSendResult> SendToUserAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        var active = await devices.ListActiveForUserAsync(message.TenantId, message.UserId, cancellationToken);
        if (active.Count == 0)
        {
            return PushSendResult.None;
        }

        int sent = 0, failed = 0, removed = 0;

        foreach (var device in active)
        {
            switch (await sender.SendAsync(BuildMessage(device, message), cancellationToken))
            {
                case FcmSendOutcome.Sent:
                    sent++;
                    break;

                case FcmSendOutcome.Unregistered:
                    devices.Remove(device);
                    removed++;
                    logger.LogInformation("Dispositivo {DeviceId} removido: o FCM não reconhece mais o token.", device.Id);
                    break;

                default:
                    failed++;
                    logger.LogWarning("Falha ao enviar push ao dispositivo {DeviceId}.", device.Id);
                    break;
            }
        }

        if (removed > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PushSendResult(sent, failed, removed);
    }

    // O Firebase Admin 3.x depreciou Message.Token em favor de Fid (Installation ID). O app móvel
    // ainda envia o token FCM clássico (o `getToken` do firebase_messaging também está depreciado),
    // então os dois lados usam o MESMO identificador hoje e migram JUNTOS — ver ADR-069.
#pragma warning disable CS0618
    public static Message BuildMessage(Device device, PushMessage message) => new()
    {
        Token = device.DeviceToken,
        Notification = new Notification { Title = message.Title, Body = message.Body },
        Data = message.Route is null ? null : new Dictionary<string, string> { ["route"] = message.Route },
        Android = new AndroidConfig
        {
            Priority = Priority.High,
            Notification = new AndroidNotification { ChannelId = AndroidChannelId },
        },
        Apns = new ApnsConfig { Aps = new Aps { Sound = "default" } },
    };
#pragma warning restore CS0618
}
