using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Workflow.Domain.Tags;
using FluentAssertions;

namespace Advocacia.Modules.Workflow.Domain.UnitTests.Tags;

public class TagTests
{
    [Fact]
    public void Create_KeepsValidColorInLowercase()
    {
        var tag = Tag.Create(Guid.NewGuid(), " Urgente ", "#DC2626").Value;

        tag.Name.Should().Be("Urgente");
        tag.Color.Should().Be("#dc2626");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("red")]
    [InlineData("#12345")]
    public void Create_WithInvalidColor_FallsBackToDefault(string? color)
    {
        Tag.Create(Guid.NewGuid(), "Prioridade", color).Value.Color.Should().Be(Tag.DefaultColor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Fails(string name)
    {
        var result = Tag.Create(Guid.NewGuid(), name, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TAG_NAME_REQUIRED));
    }

    [Fact]
    public void Create_WithNameLongerThanLimit_Fails()
    {
        Tag.Create(Guid.NewGuid(), new string('a', Tag.NameMaxLength + 1), null).IsFailure.Should().BeTrue();
    }
}
