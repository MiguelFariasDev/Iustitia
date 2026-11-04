using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Domain.Tenancy;
using MapsterMapper;

namespace Advocacia.Platform.Application.Features.Tenants.GetTenantById;

public sealed class GetTenantByIdHandler(IRepository<Tenant, TenantId> tenantRepository, ICurrentUser currentUser, IMapper mapper)
    : IQueryHandler<GetTenantByIdQuery, GetTenantByIdResponse>
{
    public async Task<Result<GetTenantByIdResponse>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        // Tenant é a própria fronteira de multi-tenancy — não implementa IHasTenant, então
        // não existe filtro global do EF Core/RLS bloqueando esta consulta por tenant (ver
        // ADR-004). Policies.OwnerOnly só garante "é Owner de algum escritório", não "é
        // Owner DESTE escritório" — sem esta checagem explícita, qualquer Owner poderia ler
        // os dados de qualquer outro tenant só sabendo o Id (IDOR entre tenants).
        if (currentUser.TenantId != request.TenantId)
        {
            return Result.Failure<GetTenantByIdResponse>(ErrorFactory.From(ErrorCode.TENANT_NOT_FOUND));
        }

        var tenant = await tenantRepository.GetByIdAsync(TenantId.From(request.TenantId), cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<GetTenantByIdResponse>(ErrorFactory.From(ErrorCode.TENANT_NOT_FOUND));
        }

        return mapper.Map<GetTenantByIdResponse>(tenant);
    }
}
