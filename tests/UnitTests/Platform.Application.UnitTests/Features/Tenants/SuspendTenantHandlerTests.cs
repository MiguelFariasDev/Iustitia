using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.SuspendTenant;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Tenants;

public class SuspendTenantHandlerTests
{
    private readonly IRepository<Tenant, TenantId> _tenantRepository = Substitute.For<IRepository<Tenant, TenantId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private SuspendTenantHandler CreateHandler() => new(_tenantRepository, _unitOfWork, _auditService, _currentUser);

    [Fact]
    public async Task Handle_WhenTenantIsActiveAndBelongsToCurrentUser_Suspends()
    {
        var tenant = Tenant.Create("Escritorio Teste", CNPJ.Create("11444777000161").Value).Value;
        _currentUser.TenantId.Returns(tenant.Id.Value);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(new SuspendTenantCommand(tenant.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(TenantStatus.Suspended));
        tenant.Status.Should().Be(TenantStatus.Suspended);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantAlreadySuspended_ReturnsConflict()
    {
        var tenant = Tenant.Create("Escritorio Teste", CNPJ.Create("11444777000161").Value).Value;
        tenant.Suspend();
        _currentUser.TenantId.Returns(tenant.Id.Value);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(new SuspendTenantCommand(tenant.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_SUSPENDED));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantDoesNotExist_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        _currentUser.TenantId.Returns(tenantId);
        _tenantRepository.GetByIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var result = await CreateHandler().Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WhenTenantBelongsToAnotherTenant_ReturnsNotFoundWithoutSaving()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new SuspendTenantCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
