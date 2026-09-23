using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Errors;

public class ErrorFactoryTests
{
    [Fact]
    public void From_WithoutArgs_ReturnsErrorWithCatalogMessage()
    {
        var error = ErrorFactory.From(ErrorCode.TENANT_NOT_FOUND);

        error.Code.Should().Be(nameof(ErrorCode.TENANT_NOT_FOUND));
        error.Message.Should().Be(ErrorCatalog.Get(ErrorCode.TENANT_NOT_FOUND).Message);
        error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void From_WithArgs_FormatsMessagePlaceholders()
    {
        var error = ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, "Email");

        error.Message.Should().Be("O campo 'Email' é obrigatório.");
    }

    [Fact]
    public void From_ReturnsSameHttpRelevantTypeAsCatalogDefinition()
    {
        foreach (var code in Enum.GetValues<ErrorCode>())
        {
            var definition = ErrorCatalog.Get(code);
            var error = ErrorFactory.From(code);

            error.Type.Should().Be(definition.Type);
            error.Code.Should().Be(definition.Code);
        }
    }
}
