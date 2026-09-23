using Advocacia.BuildingBlocks.Domain.Abstractions;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Domain.UnitTests.Abstractions;

public class EntityTests
{
    private sealed class SampleEntity(Guid id) : Entity<Guid>(id);

    [Fact]
    public void Equals_WhenSameIdAndType_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var first = new SampleEntity(id);
        var second = new SampleEntity(id);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentId_ReturnsFalse()
    {
        var first = new SampleEntity(Guid.NewGuid());
        var second = new SampleEntity(Guid.NewGuid());

        first.Should().NotBe(second);
    }

    [Fact]
    public void GetHashCode_WhenSameId_IsEqual()
    {
        var id = Guid.NewGuid();
        var first = new SampleEntity(id);
        var second = new SampleEntity(id);

        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
