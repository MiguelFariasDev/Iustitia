using Advocacia.BuildingBlocks.Domain.Abstractions;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Abstractions;

public class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        public decimal Amount { get; } = amount;

        public string Currency { get; } = currency;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void Equals_WhenSameComponents_ReturnsTrue()
    {
        var first = new Money(100m, "BRL");
        var second = new Money(100m, "BRL");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentComponents_ReturnsFalse()
    {
        var first = new Money(100m, "BRL");
        var second = new Money(200m, "BRL");

        first.Should().NotBe(second);
    }

    [Fact]
    public void GetHashCode_WhenSameComponents_IsEqual()
    {
        var first = new Money(100m, "BRL");
        var second = new Money(100m, "BRL");

        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
