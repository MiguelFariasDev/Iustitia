using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.GetTenantById;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Tenants;

public class GetTenantByIdHandlerTests
{
    private readonly IRepository<Tenant, TenantId> _tenantRepository = Substitute.For<IRepository<Tenant, TenantId>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private GetTenantByIdHandler CreateHandler() => new(_tenantRepository, _currentUser, TestMapper.Create());

    [Fact]
    public async Task Handle_WhenTenantExistsAndBelongsToCurrentUser_ReturnsTenantData()
    {
        var tenant = Tenant.Create("Escritorio Teste", CNPJ.Create("11444777000161").Value).Value;
        _currentUser.TenantId.Returns(tenant.Id.Value);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(new GetTenantByIdQuery(tenant.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(tenant.Id.Value);
        result.Value.Name.Should().Be("Escritorio Teste");
        result.Value.Status.Should().Be(nameof(TenantStatus.Active));
    }

    [Fact]
    public async Task Handle_WhenTenantDoesNotExist_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        _currentUser.TenantId.Returns(tenantId);
        _tenantRepository.GetByIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var result = await CreateHandler().Handle(new GetTenantByIdQuery(tenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WhenTenantBelongsToAnotherTenant_ReturnsNotFoundWithoutQueryingRepository()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new GetTenantByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
        await _tenantRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, cancellationToken: default);
    }
}
