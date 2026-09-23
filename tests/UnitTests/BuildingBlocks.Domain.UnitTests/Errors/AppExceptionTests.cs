using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Errors.Exceptions;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Errors;

public class AppExceptionTests
{
    [Fact]
    public void Constructor_WithoutCustomMessage_UsesCatalogMessage()
    {
        var exception = new AppException(ErrorCode.TENANT_NOT_FOUND);

        exception.Code.Should().Be(ErrorCode.TENANT_NOT_FOUND);
        exception.Group.Should().Be(ErrorGroup.Tenant);
        exception.HttpStatus.Should().Be(404);
        exception.Message.Should().Be(ErrorCatalog.Get(ErrorCode.TENANT_NOT_FOUND).Message);
    }

    [Fact]
    public void Constructor_WithCustomMessage_OverridesCatalogMessage()
    {
        var exception = new AppException(ErrorCode.TENANT_NOT_FOUND, "Mensagem customizada para este caso.");

        exception.Message.Should().Be("Mensagem customizada para este caso.");
        exception.Code.Should().Be(ErrorCode.TENANT_NOT_FOUND);
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesIt()
    {
        var inner = new InvalidOperationException("causa raiz");

        var exception = new AppException(ErrorCode.INTERNAL_UNEXPECTED, inner: inner);

        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void NotFoundException_UsesCommonNotFound()
    {
        var exception = new NotFoundException();

        exception.Code.Should().Be(ErrorCode.COMMON_NOT_FOUND);
        exception.HttpStatus.Should().Be(404);
    }

    [Fact]
    public void ConflictException_UsesCommonAlreadyExists()
    {
        var exception = new ConflictException();

        exception.Code.Should().Be(ErrorCode.COMMON_ALREADY_EXISTS);
        exception.HttpStatus.Should().Be(409);
    }

    [Fact]
    public void ForbiddenException_UsesAuthorizationForbidden()
    {
        var exception = new ForbiddenException();

        exception.Code.Should().Be(ErrorCode.AUTHORIZATION_FORBIDDEN);
        exception.HttpStatus.Should().Be(403);
    }

    [Fact]
    public void IntegrationException_UsesTheGivenIntegrationCode()
    {
        var exception = new IntegrationException(ErrorCode.INTEGRATION_TIMEOUT, "Timeout ao chamar o CNJ.");

        exception.Code.Should().Be(ErrorCode.INTEGRATION_TIMEOUT);
        exception.Group.Should().Be(ErrorGroup.Integration);
        exception.Message.Should().Be("Timeout ao chamar o CNJ.");
    }
}
