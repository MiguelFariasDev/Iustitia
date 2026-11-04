using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.FeatureFlags;

namespace Advocacia.Platform.Application.Features.FeatureFlags.UpdateFeatureFlag;

/// <summary>
/// Upsert idempotente de uma feature flag por tenant: cria se a chave não existir, e só
/// chama Enable/Disable quando o estado atual realmente difere do solicitado — evitar
/// chamar Enable() numa flag já habilitada, por exemplo, já que o domínio trata isso como
/// conflito (ver FeatureFlag.Enable/Disable), o que quebraria a semântica idempotente
/// esperada de um PUT.
/// </summary>
public sealed class UpdateFeatureFlagHandler(
    IRepository<FeatureFlag, FeatureFlagId> featureFlagRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IFeatureFlagService featureFlagService) : ICommandHandler<UpdateFeatureFlagCommand, UpdateFeatureFlagResponse>
{
    public async Task<Result<UpdateFeatureFlagResponse>> Handle(UpdateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<UpdateFeatureFlagResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var keyResult = FeatureFlagKey.Create(request.Key);
        if (keyResult.IsFailure)
        {
            return Result.Failure<UpdateFeatureFlagResponse>(keyResult.Error);
        }

        var now = dateTimeProvider.UtcNow;
        var existing = await featureFlagRepository.FirstOrDefaultAsync(
            new FeatureFlagByKeySpecification(tenantId, keyResult.Value), cancellationToken);

        if (existing is not null)
        {
            if (existing.IsEnabled != request.IsEnabled)
            {
                var transitionResult = request.IsEnabled ? existing.Enable(now) : existing.Disable(now);
                if (transitionResult.IsFailure)
                {
                    return Result.Failure<UpdateFeatureFlagResponse>(transitionResult.Error);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await featureFlagService.InvalidateCacheAsync(existing.Key.Value, tenantId, cancellationToken);
            }

            return new UpdateFeatureFlagResponse(existing.Key.Value, existing.IsEnabled, existing.UpdatedAt);
        }

        var createResult = FeatureFlag.Create(tenantId, keyResult.Value, request.IsEnabled, now);
        if (createResult.IsFailure)
        {
            return Result.Failure<UpdateFeatureFlagResponse>(createResult.Error);
        }

        var featureFlag = createResult.Value;
        featureFlagRepository.Add(featureFlag);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(featureFlag.Key.Value, tenantId, cancellationToken);

        return new UpdateFeatureFlagResponse(featureFlag.Key.Value, featureFlag.IsEnabled, featureFlag.UpdatedAt);
    }

    private sealed class FeatureFlagByKeySpecification : Specification<FeatureFlag>
    {
        public FeatureFlagByKeySpecification(Guid tenantId, FeatureFlagKey key)
            : base(f => f.TenantId == tenantId && f.Key == key)
        {
        }
    }
}
