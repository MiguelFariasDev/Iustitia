using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Workflow.Domain.Tasks;
using FluentAssertions;

namespace Advocacia.Modules.Workflow.Domain.UnitTests.Tasks;

public class WorkTaskTests
{
    [Fact]
    public void Create_WithTitle_StartsAsTodoAndTrimsTitle()
    {
        var result = WorkTask.Create(Guid.NewGuid(), "  Juntar procuração ", null, new DateOnly(2026, 12, 1), null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Juntar procuração");
        result.Value.Status.Should().Be(WorkTaskStatus.Todo);
        result.Value.DueDate.Should().Be(new DateOnly(2026, 12, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithoutTitle_Fails(string? title)
    {
        var result = WorkTask.Create(Guid.NewGuid(), title, null, null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.WORK_TASK_TITLE_REQUIRED));
    }

    [Fact]
    public void ChangeStatus_MovesTheTask()
    {
        var task = WorkTask.Create(Guid.NewGuid(), "Revisar minuta", null, null, null, null).Value;

        task.ChangeStatus(WorkTaskStatus.Done);

        task.Status.Should().Be(WorkTaskStatus.Done);
    }
}
