using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Domain.Settings;
using MapsterMapper;

namespace Advocacia.Platform.Application.Features.Settings.GetSettings;

public sealed class GetSettingsHandler(
    IRepository<Setting, SettingId> settingRepository, ICurrentUser currentUser, IMapper mapper)
    : IQueryHandler<GetSettingsQuery, GetSettingsResponse>
{
    public async Task<Result<GetSettingsResponse>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is null)
        {
            return Result.Failure<GetSettingsResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        // Sem Criteria: o filtro global de tenant do EF Core (Setting implementa IHasTenant,
        // ver ADR-004) já restringe ao tenant corrente — não precisa repetir aqui.
        var settings = await settingRepository.ListAsync(new AllSettingsSpecification(), cancellationToken);

        var items = settings.Select(mapper.Map<SettingItem>).ToList();

        return new GetSettingsResponse(items);
    }

    private sealed class AllSettingsSpecification : Specification<Setting>;
}
