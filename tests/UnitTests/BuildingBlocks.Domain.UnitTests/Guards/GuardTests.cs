using Advocacia.BuildingBlocks.Domain.Guards;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Guards;

public class GuardTests
{
    [Fact]
    public void AgainstNull_WhenNull_Throws()
    {
        var act = () => Guard.AgainstNull<string>(null, "param");

        act.Should().Throw<ArgumentNullException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNull_WhenNotNull_ReturnsValue()
    {
        var result = Guard.AgainstNull("value", "param");

        result.Should().Be("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AgainstNullOrEmpty_WhenNullOrEmpty_Throws(string? value)
    {
        var act = () => Guard.AgainstNullOrEmpty(value, "param");

        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AgainstNullOrWhiteSpace_WhenNullOrWhiteSpace_Throws(string? value)
    {
        var act = () => Guard.AgainstNullOrWhiteSpace(value, "param");

        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNegative_WhenNegativeInt_Throws()
    {
        var act = () => Guard.AgainstNegative(-1, "param");

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNegative_WhenNegativeDecimal_Throws()
    {
        var act = () => Guard.AgainstNegative(-1m, "param");

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNegative_WhenNonNegative_ReturnsValue()
    {
        Guard.AgainstNegative(0, "param").Should().Be(0);
        Guard.AgainstNegative(5m, "param").Should().Be(5m);
    }

    [Fact]
    public void AgainstEmpty_WhenGuidEmpty_Throws()
    {
        var act = () => Guard.AgainstEmpty(Guid.Empty, "param");

        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstOutOfRange_WhenOutOfRange_Throws()
    {
        var act = () => Guard.AgainstOutOfRange(10, 0, 5, "param");

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstOutOfRange_WhenWithinRange_DoesNotThrow()
    {
        var act = () => Guard.AgainstOutOfRange(3, 0, 5, "param");

        act.Should().NotThrow();
    }
}
