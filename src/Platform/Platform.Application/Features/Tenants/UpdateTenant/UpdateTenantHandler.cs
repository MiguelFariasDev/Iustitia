using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Tenancy;

namespace Advocacia.Platform.Application.Features.Tenants.UpdateTenant;

public sealed class UpdateTenantHandler(
    IRepository<Tenant, TenantId> tenantRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    ICurrentUser currentUser) : ICommandHandler<UpdateTenantCommand, UpdateTenantResponse>
{
    public async Task<Result<UpdateTenantResponse>> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        // Tenant não implementa IHasTenant (é a própria fronteira de multi-tenancy — ver
        // ADR-004) e Policies.OwnerOnly só valida o papel, não o tenant — sem esta checagem
        // qualquer Owner poderia editar outro escritório só sabendo o Id (IDOR entre
        // tenants, ver GetTenantByIdHandler para o mesmo raciocínio).
        if (currentUser.TenantId != request.TenantId)
        {
            return Result.Failure<UpdateTenantResponse>(ErrorFactory.From(ErrorCode.TENANT_NOT_FOUND));
        }

        var tenant = await tenantRepository.GetByIdAsync(TenantId.From(request.TenantId), cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<UpdateTenantResponse>(ErrorFactory.From(ErrorCode.TENANT_NOT_FOUND));
        }

        var before = new { tenant.Name, tenant.LogoUrl };

        var updateResult = tenant.Update(request.Name, request.LogoUrl, request.AddressJson);
        if (updateResult.IsFailure)
        {
            return Result.Failure<UpdateTenantResponse>(updateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Updated,
            entityType: nameof(Tenant),
            entityId: tenant.Id.Value.ToString(),
            before: before,
            after: new { tenant.Name, tenant.LogoUrl },
            cancellationToken: cancellationToken);

        return new UpdateTenantResponse(tenant.Id.Value, tenant.Name);
    }
}
