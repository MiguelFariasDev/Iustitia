using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Devices;
using FluentValidation;

namespace Advocacia.Platform.Application.Features.Devices;

/// <summary>Resolve (tenant, usuário) da requisição; sem os dois não há dispositivo a consultar.</summary>
internal static class DeviceScope
{
    internal static bool TryResolve(ICurrentUser currentUser, out Guid tenantId, out Guid userId)
    {
        tenantId = currentUser.TenantId ?? Guid.Empty;
        userId = currentUser.UserId ?? Guid.Empty;

        return tenantId != Guid.Empty && userId != Guid.Empty;
    }
}

public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        // O texto do erro não ecoa o valor recebido: é um token de entrega e iria parar em log.
        RuleFor(x => x.DeviceToken)
            .Must(Device.IsValidToken)
            .WithErrorCode(nameof(ErrorCode.DEVICE_TOKEN_INVALID))
            .WithMessage("O token do dispositivo é inválido.");

        RuleFor(x => x.Platform)
            .Must(value => Device.TryParsePlatform(value, out _))
            .WithErrorCode(nameof(ErrorCode.DEVICE_PLATFORM_INVALID))
            .WithMessage("Plataforma inválida. Use 'iOS' ou 'Android'.");
    }
}

/// <summary>
/// Cria o dispositivo ou renova o existente (idempotente por token: o app registra a cada login e a
/// cada renovação do token). Registrar um token REVOGADO falha com DEVICE_REVOKED — o app deve
/// encerrar a sessão local (ver <see cref="Device"/>).
/// </summary>
public sealed class RegisterDeviceHandler(
    IDeviceRepository devices, IUnitOfWork unitOfWork, ICurrentUser currentUser, IDateTimeProvider clock)
    : ICommandHandler<RegisterDeviceCommand, RegisterDeviceResponse>
{
    public async Task<Result<RegisterDeviceResponse>> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!DeviceScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<RegisterDeviceResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!Device.TryParsePlatform(request.Platform, out var platform))
        {
            return Result.Failure<RegisterDeviceResponse>(ErrorFactory.From(ErrorCode.DEVICE_PLATFORM_INVALID));
        }

        var token = request.DeviceToken.Trim();
        var now = clock.UtcNow;
        var device = await devices.GetByTokenAsync(tenantId, token, cancellationToken);

        if (device is null)
        {
            var created = Device.Register(tenantId, userId, platform, token, now);
            if (created.IsFailure)
            {
                return Result.Failure<RegisterDeviceResponse>(created.Error);
            }

            device = created.Value;
            devices.Add(device);
        }
        else
        {
            var refreshed = device.Refresh(userId, platform, now);
            if (refreshed.IsFailure)
            {
                return Result.Failure<RegisterDeviceResponse>(refreshed.Error);
            }

            devices.Update(device);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RegisterDeviceResponse(
            device.Id.Value, device.Platform == DevicePlatform.Ios ? "iOS" : "Android", device.LastSeenAt));
    }
}

public sealed class ListDevicesHandler(IDeviceRepository devices, ICurrentUser currentUser)
    : IQueryHandler<ListDevicesQuery, IReadOnlyList<DeviceResponse>>
{
    public async Task<Result<IReadOnlyList<DeviceResponse>>> Handle(ListDevicesQuery request, CancellationToken cancellationToken)
    {
        if (!DeviceScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<IReadOnlyList<DeviceResponse>>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var list = await devices.ListForUserAsync(tenantId, userId, cancellationToken);

        return Result.Success<IReadOnlyList<DeviceResponse>>(
            list.Select(device => DeviceResponse.From(device, request.CurrentDeviceId)).ToList());
    }
}

/// <summary>
/// Revoga um dispositivo DO PRÓPRIO usuário. De outro usuário, ou inexistente: DEVICE_NOT_FOUND
/// (404) — nunca diz que o id existe e é de outra pessoa.
/// </summary>
public sealed class RevokeDeviceHandler(
    IDeviceRepository devices, IUnitOfWork unitOfWork, ICurrentUser currentUser, IDateTimeProvider clock)
    : ICommandHandler<RevokeDeviceCommand>
{
    public async Task<Result> Handle(RevokeDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!DeviceScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var device = await devices.GetForUserAsync(tenantId, userId, DeviceId.From(request.DeviceId), cancellationToken);
        if (device is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DEVICE_NOT_FOUND));
        }

        var result = device.Revoke(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        devices.Update(device);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
