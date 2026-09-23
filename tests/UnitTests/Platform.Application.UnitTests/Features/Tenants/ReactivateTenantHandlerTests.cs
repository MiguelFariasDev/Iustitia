using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.ReactivateTenant;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Tenants;

public class ReactivateTenantHandlerTests
{
    private readonly IRepository<Tenant, TenantId> _tenantRepository = Substitute.For<IRepository<Tenant, TenantId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private ReactivateTenantHandler CreateHandler() => new(_tenantRepository, _unitOfWork, _auditService, _currentUser);

    [Fact]
    public async Task Handle_WhenTenantIsSuspendedAndBelongsToCurrentUser_Reactivates()
    {
        var tenant = Tenant.Create("Escritorio Teste", CNPJ.Create("11444777000161").Value).Value;
        tenant.Suspend();
        _currentUser.TenantId.Returns(tenant.Id.Value);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(new ReactivateTenantCommand(tenant.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(TenantStatus.Active));
        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Handle_WhenTenantIsNotSuspended_ReturnsConflict()
    {
        var tenant = Tenant.Create("Escritorio Teste", CNPJ.Create("11444777000161").Value).Value;
        _currentUser.TenantId.Returns(tenant.Id.Value);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(new ReactivateTenantCommand(tenant.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Fact]
    public async Task Handle_WhenTenantDoesNotExist_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        _currentUser.TenantId.Returns(tenantId);
        _tenantRepository.GetByIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var result = await CreateHandler().Handle(new ReactivateTenantCommand(tenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WhenTenantBelongsToAnotherTenant_ReturnsNotFoundWithoutSaving()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new ReactivateTenantCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
