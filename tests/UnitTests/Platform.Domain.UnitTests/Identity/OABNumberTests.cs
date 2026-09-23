using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.Identity;

public class OABNumberTests
{
    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var result = OABNumber.Create("123.456", "sp");

        result.IsSuccess.Should().BeTrue();
        result.Value.Number.Should().Be("123456");
        result.Value.StateCode.Should().Be("SP");
        result.Value.ToString().Should().Be("123456/SP");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNumberMissing_Fails(string? number)
    {
        var result = OABNumber.Create(number, "SP");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_OAB_INVALID));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    public void Create_WhenStateInvalid_Fails(string? stateCode)
    {
        var result = OABNumber.Create("123456", stateCode);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_OAB_INVALID));
    }

    [Fact]
    public void Create_WhenNumberHasNoDigits_Fails()
    {
        var result = OABNumber.Create("abc", "SP");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_OAB_INVALID));
    }

    [Fact]
    public void Equals_WhenSameNumberAndState_ReturnsTrue()
    {
        var first = OABNumber.Create("123456", "sp").Value;
        var second = OABNumber.Create("123.456", "SP").Value;

        first.Should().Be(second);
    }
}
