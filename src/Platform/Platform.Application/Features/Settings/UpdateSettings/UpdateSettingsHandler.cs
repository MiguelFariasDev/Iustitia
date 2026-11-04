using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Domain.Settings;

namespace Advocacia.Platform.Application.Features.Settings.UpdateSettings;

/// <summary>Upsert de uma configuração por tenant: cria se a chave não existir, atualiza o valor caso contrário.</summary>
public sealed class UpdateSettingsHandler(
    IRepository<Setting, SettingId> settingRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<UpdateSettingsCommand, UpdateSettingsResponse>
{
    public async Task<Result<UpdateSettingsResponse>> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<UpdateSettingsResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var keyResult = SettingKey.Create(request.Key);
        if (keyResult.IsFailure)
        {
            return Result.Failure<UpdateSettingsResponse>(keyResult.Error);
        }

        var now = dateTimeProvider.UtcNow;
        var existing = await settingRepository.FirstOrDefaultAsync(
            new SettingByKeySpecification(tenantId, keyResult.Value), cancellationToken);

        if (existing is not null)
        {
            existing.UpdateValue(request.ValueJson, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new UpdateSettingsResponse(existing.Key.Value, existing.ValueJson, existing.UpdatedAt);
        }

        var createResult = Setting.Create(tenantId, keyResult.Value, request.ValueJson, now);
        if (createResult.IsFailure)
        {
            return Result.Failure<UpdateSettingsResponse>(createResult.Error);
        }

        var setting = createResult.Value;
        settingRepository.Add(setting);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateSettingsResponse(setting.Key.Value, setting.ValueJson, setting.UpdatedAt);
    }

    private sealed class SettingByKeySpecification : Specification<Setting>
    {
        public SettingByKeySpecification(Guid tenantId, SettingKey key)
            : base(s => s.TenantId == tenantId && s.Key == key)
        {
        }
    }
}
