using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.UpdateTenant;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Tenants;

public class UpdateTenantHandlerTests
{
    private readonly IRepository<Tenant, TenantId> _tenantRepository = Substitute.For<IRepository<Tenant, TenantId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private UpdateTenantHandler CreateHandler() => new(_tenantRepository, _unitOfWork, _auditService, _currentUser);

    [Fact]
    public async Task Handle_WhenTenantExistsAndBelongsToCurrentUser_UpdatesAndAudits()
    {
        var tenant = Tenant.Create("Nome Antigo", CNPJ.Create("11444777000161").Value).Value;
        _currentUser.TenantId.Returns(tenant.Id.Value);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(
            new UpdateTenantCommand(tenant.Id.Value, "Nome Novo", "https://logo.png", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Nome Novo");
        tenant.Name.Should().Be("Nome Novo");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditService.Received(1).RecordAsync(
            AuditAction.Updated, nameof(Tenant), tenant.Id.Value.ToString(),
            before: Arg.Any<object>(), after: Arg.Any<object>(),
            performedByUserId: null, tenantId: null, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantDoesNotExist_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        _currentUser.TenantId.Returns(tenantId);
        _tenantRepository.GetByIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var result = await CreateHandler().Handle(
            new UpdateTenantCommand(tenantId, "Nome", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WhenTenantBelongsToAnotherTenant_ReturnsNotFoundWithoutSaving()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new UpdateTenantCommand(Guid.NewGuid(), "Nome Novo", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyName_ReturnsValidationFailureWithoutSaving()
    {
        var tenant = Tenant.Create("Nome Antigo", CNPJ.Create("11444777000161").Value).Value;
        _currentUser.TenantId.Returns(tenant.Id.Value);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(
            new UpdateTenantCommand(tenant.Id.Value, "   ", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NAME_REQUIRED));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
