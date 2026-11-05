using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.Platform.Domain.Devices;

namespace Advocacia.Platform.Application.Features.Devices;

/// <param name="DeviceToken">Token FCM (Android) ou o token FCM do APNs (iOS). Segredo de entrega: guardado, nunca devolvido.</param>
/// <param name="Platform">"iOS" ou "Android" (sem diferenciar maiúsculas).</param>
public sealed record RegisterDeviceCommand(string DeviceToken, string Platform) : ICommand<RegisterDeviceResponse>;

/// <summary>O app guarda <see cref="DeviceId"/> e o envia em <c>X-Device-Id</c> para a API saber qual aparelho fala.</summary>
public sealed record RegisterDeviceResponse(Guid DeviceId, string Platform, DateTimeOffset LastSeenAt);

public sealed record ListDevicesQuery(Guid? CurrentDeviceId = null) : IQuery<IReadOnlyList<DeviceResponse>>;

public sealed record RevokeDeviceCommand(Guid DeviceId) : ICommand;

/// <summary>Sem o token, de propósito.</summary>
/// <param name="Id">Id do dispositivo (o de <c>DELETE /devices/{id}</c>).</param>
/// <param name="Platform">iOS ou Android.</param>
/// <param name="IsRevoked">Se foi revogado (permanente).</param>
/// <param name="RevokedAt">Quando foi revogado.</param>
/// <param name="LastSeenAt">Último registro do app neste aparelho.</param>
/// <param name="CreatedAt">Primeiro registro.</param>
/// <param name="IsCurrent">Se é o aparelho que fez a requisição (cabeçalho <c>X-Device-Id</c>).</param>
public sealed record DeviceResponse(
    Guid Id,
    string Platform,
    bool IsRevoked,
    DateTimeOffset? RevokedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset CreatedAt,
    bool IsCurrent)
{
    public static DeviceResponse From(Device device, Guid? currentDeviceId) => new(
        device.Id.Value,
        device.Platform == DevicePlatform.Ios ? "iOS" : "Android",
        device.IsRevoked,
        device.RevokedAt,
        device.LastSeenAt,
        device.CreatedAt,
        currentDeviceId == device.Id.Value);
}
