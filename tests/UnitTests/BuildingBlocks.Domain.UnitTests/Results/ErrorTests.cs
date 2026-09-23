using Advocacia.BuildingBlocks.Domain.Results;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Results;

public class ErrorTests
{
    [Theory]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    public void FactoryMethods_SetExpectedErrorType(ErrorType expectedType)
    {
        var error = expectedType switch
        {
            ErrorType.Failure => Error.Failure("code", "message"),
            ErrorType.Validation => Error.Validation("code", "message"),
            ErrorType.NotFound => Error.NotFound("code", "message"),
            ErrorType.Conflict => Error.Conflict("code", "message"),
            ErrorType.Unauthorized => Error.Unauthorized("code", "message"),
            ErrorType.Forbidden => Error.Forbidden("code", "message"),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedType)),
        };

        error.Type.Should().Be(expectedType);
        error.Code.Should().Be("code");
        error.Message.Should().Be("message");
    }

    [Fact]
    public void None_HasEmptyCodeAndMessage()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }

    [Fact]
    public void Equals_WhenSameCodeMessageAndType_ReturnsTrue()
    {
        var first = Error.Validation("x", "y");
        var second = Error.Validation("x", "y");

        first.Should().Be(second);
    }
}
