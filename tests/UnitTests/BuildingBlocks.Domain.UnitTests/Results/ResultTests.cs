using Advocacia.BuildingBlocks.Domain.Results;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Results;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        var error = Error.Validation("test.invalid", "Inválido.");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_WhenSuccessWithError_Throws()
    {
        var act = () => new ThrowingResult(true, Error.Validation("x", "y"));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WhenFailureWithoutError_Throws()
    {
        var act = () => new ThrowingResult(false, Error.None);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericValue_WhenFailure_Throws()
    {
        var result = Result.Failure<int>(Error.NotFound("x", "y"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitOperator_WrapsValueAsSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    // Expõe o construtor protected internal apenas para o teste de invariantes acima.
    private sealed class ThrowingResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
