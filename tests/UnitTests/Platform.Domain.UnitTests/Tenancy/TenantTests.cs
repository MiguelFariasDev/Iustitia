using Advocacia.Platform.Domain.Tenancy;
using Advocacia.Platform.Domain.Tenancy.Events;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.Tenancy;

public class TenantTests
{
    private static CNPJ ValidCnpj => CNPJ.Create("11222333000181").Value;

    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var result = Tenant.Create("Escritório Teste", ValidCnpj);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Escritório Teste");
        result.Value.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void Create_RaisesTenantCreatedEvent()
    {
        var result = Tenant.Create("Escritório Teste", ValidCnpj);

        result.Value.DomainEvents.Should().ContainSingle(e => e is TenantCreatedEvent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameMissing_Fails(string? name)
    {
        var result = Tenant.Create(name!, ValidCnpj);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_NAME_REQUIRED));
    }

    [Fact]
    public void Suspend_WhenActive_Succeeds()
    {
        var tenant = Tenant.Create("Escritório Teste", ValidCnpj).Value;
        tenant.ClearDomainEvents();

        var result = tenant.Suspend();

        result.IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be(TenantStatus.Suspended);
        tenant.DomainEvents.Should().ContainSingle(e => e is TenantSuspendedEvent);
    }

    [Fact]
    public void Suspend_WhenAlreadySuspended_Fails()
    {
        var tenant = Tenant.Create("Escritório Teste", ValidCnpj).Value;
        tenant.Suspend();

        var result = tenant.Suspend();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_SUSPENDED));
    }

    [Fact]
    public void Suspend_WhenCancelled_Fails()
    {
        var tenant = Tenant.Create("Escritório Teste", ValidCnpj).Value;
        tenant.Cancel();

        var result = tenant.Suspend();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CANCELLED));
    }

    [Fact]
    public void Reactivate_WhenSuspended_Succeeds()
    {
        var tenant = Tenant.Create("Escritório Teste", ValidCnpj).Value;
        tenant.Suspend();

        var result = tenant.Reactivate();

        result.IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void Reactivate_WhenNotSuspended_Fails()
    {
        var tenant = Tenant.Create("Escritório Teste", ValidCnpj).Value;

        var result = tenant.Reactivate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Fact]
    public void Cancel_WhenNotCancelled_Succeeds()
    {
        var tenant = Tenant.Create("Escritório Teste", ValidCnpj).Value;
        tenant.ClearDomainEvents();

        var result = tenant.Cancel();

        result.IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be(TenantStatus.Cancelled);
        tenant.DomainEvents.Should().ContainSingle(e => e is TenantCancelledEvent);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Fails()
    {
        var tenant = Tenant.Create("Escritório Teste", ValidCnpj).Value;
        tenant.Cancel();

        var result = tenant.Cancel();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CANCELLED));
    }

    [Fact]
    public void Update_WhenValid_ChangesFields()
    {
        var tenant = Tenant.Create("Nome Antigo", ValidCnpj).Value;

        var result = tenant.Update("Nome Novo", "https://logo.example/x.png", "{\"city\":\"SP\"}");

        result.IsSuccess.Should().BeTrue();
        tenant.Name.Should().Be("Nome Novo");
        tenant.LogoUrl.Should().Be("https://logo.example/x.png");
        tenant.AddressJson.Should().Be("{\"city\":\"SP\"}");
    }
}
