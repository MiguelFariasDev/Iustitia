using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Domain.FeatureFlags;
using MapsterMapper;

namespace Advocacia.Platform.Application.Features.FeatureFlags.GetFeatureFlags;

public sealed class GetFeatureFlagsHandler(
    IRepository<FeatureFlag, FeatureFlagId> featureFlagRepository, ICurrentUser currentUser, IMapper mapper)
    : IQueryHandler<GetFeatureFlagsQuery, GetFeatureFlagsResponse>
{
    public async Task<Result<GetFeatureFlagsResponse>> Handle(GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is null)
        {
            return Result.Failure<GetFeatureFlagsResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        // Sem Criteria: o filtro global de tenant do EF Core (FeatureFlag implementa
        // IHasTenant, ver ADR-004) já restringe ao tenant corrente.
        var flags = await featureFlagRepository.ListAsync(new AllFeatureFlagsSpecification(), cancellationToken);

        var items = flags.Select(mapper.Map<FeatureFlagItem>).ToList();

        return new GetFeatureFlagsResponse(items);
    }

    private sealed class AllFeatureFlagsSpecification : Specification<FeatureFlag>;
}
