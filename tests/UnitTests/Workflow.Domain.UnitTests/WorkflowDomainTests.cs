using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Workflow.Domain.Agenda;
using Advocacia.Modules.Workflow.Domain.WorkTasks;
using FluentAssertions;

namespace Advocacia.Workflow.Domain.UnitTests;

public class WorkTaskTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Due = new(2026, 11, 12);

    private static WorkTask Create() => WorkTask.Create(Tenant, User, "Contestação", null, Due, null, Now).Value;

    [Fact]
    public void Create_StartsPendingWithTrimmedText()
    {
        var task = WorkTask.Create(Tenant, User, "  Contestação  ", "  prazo de 15 dias ", Due, Guid.NewGuid(), Now).Value;

        task.Status.Should().Be(WorkTaskStatus.Pending);
        task.Title.Should().Be("Contestação");
        task.Description.Should().Be("prazo de 15 dias");
        task.DueDate.Should().Be(Due);
        task.CompletedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutATitle_Fails(string? title)
    {
        WorkTask.Create(Tenant, User, title, null, Due, null, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithADefaultDate_Fails()
    {
        // 0001-01-01 é o valor de um DateOnly não preenchido: quase certamente esquecimento.
        var result = WorkTask.Create(Tenant, User, "x", null, default, null, Now);

        result.Error.Code.Should().Be(nameof(ErrorCode.TASK_DUE_DATE_INVALID));
    }

    [Fact]
    public void Create_WithAPastDueDate_IsAllowed_BecauseRegisteringAnOverdueDeadlineIsLegitimate()
    {
        WorkTask.Create(Tenant, User, "x", null, new DateOnly(2026, 1, 2), null, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_EnforcesTheLengthLimits()
    {
        WorkTask.Create(Tenant, User, new string('a', WorkTask.TitleMaxLength + 1), null, Due, null, Now).IsFailure.Should().BeTrue();
        WorkTask.Create(Tenant, User, "x", new string('a', WorkTask.DescriptionMaxLength + 1), Due, null, Now).IsFailure.Should().BeTrue();
        WorkTask.Create(Tenant, User, new string('a', WorkTask.TitleMaxLength), null, Due, null, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_TreatsAnEmptyProcessIdAsNone()
    {
        WorkTask.Create(Tenant, User, "x", null, Due, Guid.Empty, Now).Value.ProcessoId.Should().BeNull();
    }

    [Fact]
    public void Complete_MarksItCompleted_AndASecondTimeFails()
    {
        var task = Create();

        task.Complete(Now.AddHours(1)).IsSuccess.Should().BeTrue();
        task.Status.Should().Be(WorkTaskStatus.Completed);
        task.CompletedAt.Should().Be(Now.AddHours(1));

        var second = task.Complete(Now.AddDays(1));
        second.Error.Code.Should().Be(nameof(ErrorCode.TASK_ALREADY_COMPLETED));
        task.CompletedAt.Should().Be(Now.AddHours(1));
    }
}

public class CalendarEventTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 14, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Create_StoresTimesInUtc()
    {
        var calendarEvent = CalendarEvent.Create(
            Tenant, Owner, "Audiência", null, CalendarEventType.Hearing, Start, Start.AddHours(1), " Fórum ", null, Now).Value;

        calendarEvent.StartsAt.Should().Be(Start);
        calendarEvent.StartsAt.Offset.Should().Be(TimeSpan.Zero); // 14:00-03:00 == 17:00Z
        calendarEvent.EndsAt!.Value.Offset.Should().Be(TimeSpan.Zero);
        calendarEvent.Location.Should().Be("Fórum");
    }

    [Fact]
    public void Create_WithTheEndBeforeTheStart_Fails()
    {
        var result = CalendarEvent.Create(Tenant, Owner, "x", null, CalendarEventType.Meeting, Start, Start.AddMinutes(-1), null, null, Now);

        result.Error.Code.Should().Be(nameof(ErrorCode.AGENDA_EVENT_PERIOD_INVALID));
    }

    [Fact]
    public void Create_WithTheEndEqualToTheStart_IsAllowed()
    {
        CalendarEvent.Create(Tenant, Owner, "x", null, CalendarEventType.Other, Start, Start, null, null, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutATitle_Fails()
    {
        CalendarEvent.Create(Tenant, Owner, " ", null, CalendarEventType.Other, Start, null, null, null, Now).IsFailure.Should().BeTrue();
    }
}
