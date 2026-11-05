using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Workflow.Application;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Application.Features.Agenda;
using Advocacia.Modules.Workflow.Application.Features.WorkTasks;
using Advocacia.Modules.Workflow.Domain.Agenda;
using Advocacia.Modules.Workflow.Domain.WorkTasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Workflow.Application.UnitTests;

public abstract class WorkflowTestBase
{
    protected readonly Guid TenantId = Guid.NewGuid();
    protected readonly Guid UserId = Guid.NewGuid();
    protected readonly ICurrentUser CurrentUser = Substitute.For<ICurrentUser>();
    protected readonly IDateTimeProvider Clock = Substitute.For<IDateTimeProvider>();
    protected readonly IWorkTaskRepository Tasks = Substitute.For<IWorkTaskRepository>();
    protected readonly ICalendarEventRepository Events = Substitute.For<ICalendarEventRepository>();
    protected readonly IWorkflowUnitOfWork UnitOfWork = Substitute.For<IWorkflowUnitOfWork>();

    // Quinta-feira 2026-11-05 às 23:30 em São Paulo == 2026-11-06 02:30 UTC. De propósito: é a hora em
    // que "hoje" em UTC e "hoje" no escritório divergem.
    protected static readonly DateTimeOffset NowUtc = new(2026, 11, 6, 2, 30, 0, TimeSpan.Zero);
    protected static readonly DateOnly Today = new(2026, 11, 5);

    protected WorkflowTestBase()
    {
        CurrentUser.TenantId.Returns(TenantId);
        CurrentUser.UserId.Returns(UserId);
        Clock.UtcNow.Returns(NowUtc);
    }

    protected BusinessCalendar Calendar => new(Clock, Options.Create(new WorkflowOptions()));

    protected WorkTask NewTask(string title, DateOnly due, bool completed = false)
    {
        var task = WorkTask.Create(TenantId, UserId, title, null, due, null, NowUtc).Value;
        if (completed)
        {
            task.Complete(NowUtc);
        }

        return task;
    }
}

public class BusinessCalendarTests : WorkflowTestBase
{
    [Fact]
    public void Today_IsTheDayInTheOfficeTimeZone_NotInUtc()
    {
        // 02:30Z do dia 6 ainda é a noite do dia 5 em São Paulo.
        Calendar.Today.Should().Be(Today);
    }

    [Fact]
    public void StartOfDayUtc_AccountsForTheOffset()
    {
        Calendar.StartOfDayUtc(Today).Should().Be(new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void LocalDate_PlacesAnEveningHearingOnItsOwnDay()
    {
        // 22h de 13/11 em São Paulo (01h do dia 14 em UTC) tem de cair na agenda do dia 13.
        var instant = new DateTimeOffset(2026, 11, 14, 1, 0, 0, TimeSpan.Zero);

        Calendar.LocalDate(instant).Should().Be(new DateOnly(2026, 11, 13));
    }

    [Fact]
    public void AnUnknownTimeZone_FailsAtConstruction()
    {
        var act = () => new BusinessCalendar(Clock, Options.Create(new WorkflowOptions { TimeZoneId = "Marte/Olympus" }));

        act.Should().Throw<TimeZoneNotFoundException>();
    }
}

public class WorkTaskHandlersTests : WorkflowTestBase
{
    [Theory]
    [InlineData(null, TaskDueFilter.All)]
    [InlineData("", TaskDueFilter.All)]
    [InlineData("all", TaskDueFilter.All)]
    [InlineData("today", TaskDueFilter.Today)]
    [InlineData("TODAY", TaskDueFilter.Today)]
    [InlineData("overdue", TaskDueFilter.Overdue)]
    [InlineData("next_7_days", TaskDueFilter.Next7Days)]
    public async Task List_ParsesTheFilter_AndUsesTheOfficeToday(string? filter, TaskDueFilter expected)
    {
        Tasks.ListAsync(default, default, default!, default, default).ReturnsForAnyArgs([]);
        var handler = new ListWorkTasksHandler(Tasks, CurrentUser, Calendar);

        var result = await handler.Handle(new ListWorkTasksQuery(null, filter), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await Tasks.Received(1).CountAsync(
            TenantId, UserId, Arg.Is<WorkTaskQuery>(q => q.Filter == expected && q.Today == Today), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("tomorrow", null)]
    [InlineData("next_7_days ", null)]
    [InlineData(null, "Done")]
    [InlineData(null, "5")]
    public async Task List_WithAnUnknownFilterOrStatus_Fails(string? filter, string? status)
    {
        var handler = new ListWorkTasksHandler(Tasks, CurrentUser, Calendar);

        var result = await handler.Handle(new ListWorkTasksQuery(status, filter), CancellationToken.None);

        // "next_7_days " com espaço é aceito (Trim), então só os outros falham.
        if (filter == "next_7_days ")
        {
            result.IsSuccess.Should().BeTrue();
            return;
        }

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TASK_FILTER_INVALID));
    }

    [Fact]
    public async Task List_ParsesTheStatus_AndPaginates()
    {
        Tasks.ListAsync(default, default, default!, default, default).ReturnsForAnyArgs([NewTask("a", Today)]);
        Tasks.CountAsync(default, default, default!, default).ReturnsForAnyArgs(120);
        var handler = new ListWorkTasksHandler(Tasks, CurrentUser, Calendar);

        var result = await handler.Handle(new ListWorkTasksQuery("pending", null, Page: 2, PageSize: 50), CancellationToken.None);

        result.Value.TotalCount.Should().Be(120);
        result.Value.TotalPages.Should().Be(3);
        await Tasks.Received(1).ListAsync(
            TenantId, UserId, Arg.Is<WorkTaskQuery>(q => q.Status == WorkTaskStatus.Pending), 50, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_FlagsOnlyPendingPastDueTasksAsOverdue()
    {
        Tasks.ListAsync(default, default, default!, default, default).ReturnsForAnyArgs(
        [
            NewTask("atrasada", Today.AddDays(-1)),
            NewTask("hoje", Today),
            NewTask("concluida antiga", Today.AddDays(-3), completed: true),
        ]);
        var handler = new ListWorkTasksHandler(Tasks, CurrentUser, Calendar);

        var result = await handler.Handle(new ListWorkTasksQuery(), CancellationToken.None);

        result.Value.Items.Where(i => i.IsOverdue).Select(i => i.Title).Should().Equal("atrasada");
    }

    [Fact]
    public async Task List_ClampsAbusivePageSizes()
    {
        var handler = new ListWorkTasksHandler(Tasks, CurrentUser, Calendar);

        var result = await handler.Handle(new ListWorkTasksQuery(PageSize: 100000, Page: -4), CancellationToken.None);

        result.Value.PageSize.Should().Be(50);
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public async Task Summary_CountsPendingTodayOverdueAndNext7_AllForPendingTasks()
    {
        Tasks.CountAsync(default, default, default!, default).ReturnsForAnyArgs(0);
        Tasks.CountAsync(TenantId, UserId, Arg.Is<WorkTaskQuery>(q => q.Filter == TaskDueFilter.All), Arg.Any<CancellationToken>()).Returns(9);
        Tasks.CountAsync(TenantId, UserId, Arg.Is<WorkTaskQuery>(q => q.Filter == TaskDueFilter.Today), Arg.Any<CancellationToken>()).Returns(2);
        Tasks.CountAsync(TenantId, UserId, Arg.Is<WorkTaskQuery>(q => q.Filter == TaskDueFilter.Overdue), Arg.Any<CancellationToken>()).Returns(3);
        Tasks.CountAsync(TenantId, UserId, Arg.Is<WorkTaskQuery>(q => q.Filter == TaskDueFilter.Next7Days), Arg.Any<CancellationToken>()).Returns(5);
        Tasks.CountCompletedSinceAsync(TenantId, UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(7);
        var handler = new GetWorkTasksSummaryHandler(Tasks, CurrentUser, Calendar);

        var result = await handler.Handle(new GetWorkTasksSummaryQuery(), CancellationToken.None);

        result.Value.Should().Be(new WorkTasksSummaryResponse(9, 2, 3, 5, 7));
        await Tasks.Received().CountAsync(
            TenantId, UserId, Arg.Is<WorkTaskQuery>(q => q.Status == WorkTaskStatus.Pending), Arg.Any<CancellationToken>());
        // O mês começa no dia 1 do mês do ESCRITÓRIO (00h de São Paulo == 03h UTC).
        await Tasks.Received(1).CountCompletedSinceAsync(
            TenantId, UserId, new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AssignsTheTaskToTheCaller_AndSaves()
    {
        var handler = new CreateWorkTaskHandler(Tasks, UnitOfWork, CurrentUser, Clock, Calendar);

        var result = await handler.Handle(
            new CreateWorkTaskCommand("Réplica", null, Today.AddDays(5), null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
        Tasks.Received(1).Add(Arg.Is<WorkTask>(t => t.AssigneeUserId == UserId && t.TenantId == TenantId));
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithAnInvalidDate_FailsWithoutSaving()
    {
        var handler = new CreateWorkTaskHandler(Tasks, UnitOfWork, CurrentUser, Clock, Calendar);

        var result = await handler.Handle(new CreateWorkTaskCommand("x", null, default, null), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.TASK_DUE_DATE_INVALID));
        await UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Complete_CompletesTheCallersTask()
    {
        var task = NewTask("x", Today);
        Tasks.GetForAssigneeAsync(TenantId, UserId, task.Id, Arg.Any<CancellationToken>()).Returns(task);
        var handler = new CompleteWorkTaskHandler(Tasks, UnitOfWork, CurrentUser, Clock);

        var result = await handler.Handle(new CompleteWorkTaskCommand(task.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        task.Status.Should().Be(WorkTaskStatus.Completed);
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_OfAnotherUsersTask_IsNotFound()
    {
        // O repositório filtra pelo assignee: a tarefa do colega "não existe" para este usuário.
        Tasks.GetForAssigneeAsync(default, default, default, default).ReturnsForAnyArgs((WorkTask?)null);
        var handler = new CompleteWorkTaskHandler(Tasks, UnitOfWork, CurrentUser, Clock);

        var result = await handler.Handle(new CompleteWorkTaskCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.TASK_NOT_FOUND));
    }

    [Fact]
    public async Task Complete_AnAlreadyCompletedTask_Fails()
    {
        var task = NewTask("x", Today, completed: true);
        Tasks.GetForAssigneeAsync(TenantId, UserId, task.Id, Arg.Any<CancellationToken>()).Returns(task);
        var handler = new CompleteWorkTaskHandler(Tasks, UnitOfWork, CurrentUser, Clock);

        var result = await handler.Handle(new CompleteWorkTaskCommand(task.Id.Value), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.TASK_ALREADY_COMPLETED));
        await UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task EveryHandler_WithoutATenant_IsRefused()
    {
        CurrentUser.TenantId.Returns((Guid?)null);

        (await new ListWorkTasksHandler(Tasks, CurrentUser, Calendar).Handle(new ListWorkTasksQuery(), default))
            .Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        (await new GetWorkTasksSummaryHandler(Tasks, CurrentUser, Calendar).Handle(new GetWorkTasksSummaryQuery(), default))
            .IsFailure.Should().BeTrue();
        (await new CompleteWorkTaskHandler(Tasks, UnitOfWork, CurrentUser, Clock).Handle(new CompleteWorkTaskCommand(Guid.NewGuid()), default))
            .IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("Título", true)]
    [InlineData("", false)]
    public void CreateValidator_RequiresATitle(string title, bool valid)
    {
        new CreateWorkTaskValidator().Validate(new CreateWorkTaskCommand(title, null, Today, null)).IsValid.Should().Be(valid);
    }
}

public class AgendaHandlersTests : WorkflowTestBase
{
    private GetAgendaHandler Handler() => new(Tasks, Events, CurrentUser, Calendar);

    private CalendarEvent NewEvent(string title, DateTimeOffset startsAt) =>
        CalendarEvent.Create(TenantId, UserId, title, null, CalendarEventType.Hearing, startsAt, null, "Fórum", null, NowUtc).Value;

    [Fact]
    public async Task Agenda_MergesDeadlinesAndEvents_OrderedByDay_DeadlinesFirstThenEventsByTime()
    {
        var day = new DateOnly(2026, 11, 13);
        Tasks.ListDueBetweenAsync(default, default, default, default, default).ReturnsForAnyArgs(
        [
            NewTask("Prazo do dia 13", day),
            NewTask("Prazo do dia 12", day.AddDays(-1)),
        ]);
        Events.ListStartingBetweenAsync(default, default, default, default, default).ReturnsForAnyArgs(
        [
            NewEvent("Tarde", new DateTimeOffset(2026, 11, 13, 14, 0, 0, TimeSpan.FromHours(-3))),
            NewEvent("Manhã", new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.FromHours(-3))),
        ]);

        var result = await Handler().Handle(new GetAgendaQuery(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)), default);

        result.Value.Items.Select(i => i.Title).Should().Equal("Prazo do dia 12", "Prazo do dia 13", "Manhã", "Tarde");
        result.Value.Items.Single(i => i.Title == "Prazo do dia 13").Should().Match<AgendaItemResponse>(
            i => i.AllDay && i.Kind == "Task" && i.Type == "Deadline" && i.StartsAt == null && i.Status == "Pending");
        result.Value.Items.Single(i => i.Title == "Tarde").Should().Match<AgendaItemResponse>(
            i => !i.AllDay && i.Kind == "Event" && i.Type == "Hearing" && i.Date == day && i.Location == "Fórum");
    }

    [Fact]
    public async Task Agenda_GroupsAnEveningEventOnItsLocalDay()
    {
        Tasks.ListDueBetweenAsync(default, default, default, default, default).ReturnsForAnyArgs([]);
        // 22h de 13/11 em São Paulo == 01h do dia 14 em UTC.
        Events.ListStartingBetweenAsync(default, default, default, default, default)
            .ReturnsForAnyArgs([NewEvent("Noite", new DateTimeOffset(2026, 11, 14, 1, 0, 0, TimeSpan.Zero))]);

        var result = await Handler().Handle(new GetAgendaQuery(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)), default);

        result.Value.Items.Single().Date.Should().Be(new DateOnly(2026, 11, 13));
    }

    [Fact]
    public async Task Agenda_QueriesTheRangeInOfficeTime_EndInclusive()
    {
        Tasks.ListDueBetweenAsync(default, default, default, default, default).ReturnsForAnyArgs([]);
        Events.ListStartingBetweenAsync(default, default, default, default, default).ReturnsForAnyArgs([]);

        await Handler().Handle(new GetAgendaQuery(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)), default);

        await Tasks.Received(1).ListDueBetweenAsync(TenantId, UserId, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Arg.Any<CancellationToken>());
        // De 00h do dia 1 (03h UTC) até 00h do dia 1/12 (03h UTC), exclusivo: o dia 30 inteiro entra.
        await Events.Received(1).ListStartingBetweenAsync(
            TenantId, UserId,
            new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 1, 3, 0, 0, TimeSpan.Zero),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Agenda_EndBeforeStart_Fails()
    {
        var result = await Handler().Handle(new GetAgendaQuery(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 9)), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.AGENDA_PERIOD_INVALID));
    }

    [Fact]
    public async Task Agenda_RangeLimit_Is62Days_Inclusive()
    {
        Tasks.ListDueBetweenAsync(default, default, default, default, default).ReturnsForAnyArgs([]);
        Events.ListStartingBetweenAsync(default, default, default, default, default).ReturnsForAnyArgs([]);
        var start = new DateOnly(2026, 11, 1);

        (await Handler().Handle(new GetAgendaQuery(start, start.AddDays(61)), default)).IsSuccess.Should().BeTrue();   // 62 dias
        (await Handler().Handle(new GetAgendaQuery(start, start.AddDays(62)), default)).IsFailure.Should().BeTrue();   // 63 dias
        (await Handler().Handle(new GetAgendaQuery(start, start.AddYears(20)), default)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateEvent_StoresItForTheCaller()
    {
        var handler = new CreateCalendarEventHandler(Events, UnitOfWork, CurrentUser, Clock, Calendar);
        var start = new DateTimeOffset(2026, 11, 13, 14, 0, 0, TimeSpan.FromHours(-3));

        var result = await handler.Handle(
            new CreateCalendarEventCommand("Audiência", "hearing", start, start.AddHours(1), "Fórum", null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be("Hearing");
        Events.Received(1).Add(Arg.Is<CalendarEvent>(e => e.OwnerUserId == UserId && e.TenantId == TenantId));
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Deadline")]
    [InlineData("9")]
    [InlineData("")]
    public async Task CreateEvent_WithAnInvalidType_Fails(string type)
    {
        var handler = new CreateCalendarEventHandler(Events, UnitOfWork, CurrentUser, Clock, Calendar);

        var result = await handler.Handle(
            new CreateCalendarEventCommand("x", type, NowUtc, null, null, null, null), default);

        result.IsFailure.Should().BeTrue();
        await UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateEvent_WithTheEndBeforeTheStart_Fails()
    {
        var handler = new CreateCalendarEventHandler(Events, UnitOfWork, CurrentUser, Clock, Calendar);

        var result = await handler.Handle(
            new CreateCalendarEventCommand("x", "Meeting", NowUtc, NowUtc.AddMinutes(-5), null, null, null), default);

        result.Error.Code.Should().Be(nameof(ErrorCode.AGENDA_EVENT_PERIOD_INVALID));
    }
}
