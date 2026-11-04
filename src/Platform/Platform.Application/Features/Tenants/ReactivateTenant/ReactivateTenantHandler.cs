using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Tenancy;

namespace Advocacia.Platform.Application.Features.Tenants.ReactivateTenant;

public sealed class ReactivateTenantHandler(
    IRepository<Tenant, TenantId> tenantRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    ICurrentUser currentUser) : ICommandHandler<ReactivateTenantCommand, ReactivateTenantResponse>
{
    public async Task<Result<ReactivateTenantResponse>> Handle(ReactivateTenantCommand request, CancellationToken cancellationToken)
    {
        // Ver GetTenantByIdHandler: Tenant não tem filtro global por tenant, e OwnerOnly só
        // valida o papel — sem esta checagem, um Owner poderia reativar outro escritório.
        if (currentUser.TenantId != request.TenantId)
        {
            return Result.Failure<ReactivateTenantResponse>(ErrorFactory.From(ErrorCode.TENANT_NOT_FOUND));
        }

        var tenant = await tenantRepository.GetByIdAsync(TenantId.From(request.TenantId), cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<ReactivateTenantResponse>(ErrorFactory.From(ErrorCode.TENANT_NOT_FOUND));
        }

        var reactivateResult = tenant.Reactivate();
        if (reactivateResult.IsFailure)
        {
            return Result.Failure<ReactivateTenantResponse>(reactivateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Updated,
            entityType: nameof(Tenant),
            entityId: tenant.Id.Value.ToString(),
            after: new { tenant.Status },
            cancellationToken: cancellationToken);

        return new ReactivateTenantResponse(tenant.Id.Value, tenant.Status.ToString());
    }
}
