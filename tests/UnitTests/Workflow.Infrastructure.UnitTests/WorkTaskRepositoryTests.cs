using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Agenda;
using Advocacia.Modules.Workflow.Domain.WorkTasks;
using Advocacia.Modules.Workflow.Infrastructure.Persistence;
using Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Workflow.Infrastructure.UnitTests;

/// <summary>
/// Prova as consultas e o isolamento FEITO PELA APLICAÇÃO (tenant e usuário). O RLS do Postgres é
/// outra camada, provada no banco real (015_workflow.sql) — o EF em memória não o executa.
/// </summary>
public sealed class WorkTaskRepositoryTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _ana = Guid.NewGuid();
    private readonly Guid _bruno = Guid.NewGuid();
    private readonly AsyncLocalTenantContext _tenantContext = new();
    private readonly WorkflowDbContext _db;
    private readonly WorkTaskRepository _tasks;
    private readonly CalendarEventRepository _events;

    public WorkTaskRepositoryTests()
    {
        _tenantContext.SetTenant(_tenant);
        _db = new WorkflowDbContext(
            new DbContextOptionsBuilder<WorkflowDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, _tenantContext);
        _tasks = new WorkTaskRepository(_db);
        _events = new CalendarEventRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private WorkTask Seed(string title, DateOnly due, Guid? assignee = null, bool completed = false, Guid? tenant = null)
    {
        var task = WorkTask.Create(tenant ?? _tenant, assignee ?? _ana, title, null, due, null, Now).Value;
        if (completed)
        {
            task.Complete(Now);
        }

        _db.WorkTasks.Add(task);
        _db.SaveChanges();
        return task;
    }

    private Task<IReadOnlyList<WorkTask>> List(TaskDueFilter filter, WorkTaskStatus? status = null, Guid? user = null) =>
        _tasks.ListAsync(_tenant, user ?? _ana, new WorkTaskQuery(status, filter, Today), 0, 100);

    private static string[] Titles(IEnumerable<WorkTask> tasks) => tasks.Select(t => t.Title).ToArray();

    [Fact]
    public async Task Today_ReturnsOnlyTasksDueToday()
    {
        Seed("ontem", Today.AddDays(-1));
        Seed("hoje", Today);
        Seed("amanhã", Today.AddDays(1));

        Titles(await List(TaskDueFilter.Today)).Should().Equal("hoje");
    }

    [Fact]
    public async Task Overdue_ReturnsOnlyPendingPastDueTasks_NeverCompletedOnes()
    {
        Seed("atrasada", Today.AddDays(-2));
        Seed("vence hoje", Today);
        Seed("concluída", Today.AddDays(-5), completed: true);

        Titles(await List(TaskDueFilter.Overdue)).Should().Equal("atrasada");
        // Nem pedindo "Completed": concluída nunca está atrasada.
        (await List(TaskDueFilter.Overdue, WorkTaskStatus.Completed)).Should().BeEmpty();
    }

    [Fact]
    public async Task Next7Days_IncludesTodayAndDayPlus7_ButNotDayPlus8_NorThePast()
    {
        Seed("passado", Today.AddDays(-1));
        Seed("hoje", Today);
        Seed("em 7 dias", Today.AddDays(7));
        Seed("em 8 dias", Today.AddDays(8));

        Titles(await List(TaskDueFilter.Next7Days)).Should().Equal("hoje", "em 7 dias");
    }

    [Fact]
    public async Task Status_FiltersPendingAndCompleted()
    {
        Seed("a", Today);
        Seed("b", Today, completed: true);

        Titles(await List(TaskDueFilter.All, WorkTaskStatus.Pending)).Should().Equal("a");
        Titles(await List(TaskDueFilter.All, WorkTaskStatus.Completed)).Should().Equal("b");
        (await List(TaskDueFilter.All)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Listing_IsOrderedByDueDateThenTitle()
    {
        Seed("B", Today.AddDays(2));
        Seed("A", Today.AddDays(2));
        Seed("Z", Today);

        Titles(await List(TaskDueFilter.All)).Should().Equal("Z", "A", "B");
    }

    [Fact]
    public async Task Paging_NeitherRepeatsNorSkipsItems()
    {
        for (var i = 0; i < 5; i++)
        {
            Seed($"t{i}", Today); // mesmo vencimento e títulos distintos: o desempate decide
        }

        var query = new WorkTaskQuery(null, TaskDueFilter.All, Today);
        var first = await _tasks.ListAsync(_tenant, _ana, query, 0, 2);
        var second = await _tasks.ListAsync(_tenant, _ana, query, 2, 2);
        var third = await _tasks.ListAsync(_tenant, _ana, query, 4, 2);

        Titles(first.Concat(second).Concat(third)).Should().Equal("t0", "t1", "t2", "t3", "t4");
        (await _tasks.CountAsync(_tenant, _ana, query)).Should().Be(5);
    }

    [Fact]
    public async Task AUserNeverSeesAColleaguesTasks()
    {
        Seed("da Ana", Today);
        var brunos = Seed("do Bruno", Today, assignee: _bruno);

        Titles(await List(TaskDueFilter.All, user: _ana)).Should().Equal("da Ana");
        (await _tasks.GetForAssigneeAsync(_tenant, _ana, brunos.Id)).Should().BeNull();
        (await _tasks.GetForAssigneeAsync(_tenant, _bruno, brunos.Id)).Should().NotBeNull();
        (await _tasks.ListDueBetweenAsync(_tenant, _ana, Today, Today)).Should().ContainSingle().Which.Title.Should().Be("da Ana");
    }

    [Fact]
    public async Task TheTenantFilterHidesOtherOfficesTasks_EvenForTheSameUserId()
    {
        var other = Seed("de outro escritório", Today, tenant: Guid.NewGuid());
        _tenantContext.SetTenant(_tenant);

        (await List(TaskDueFilter.All)).Should().BeEmpty();
        (await _tasks.GetForAssigneeAsync(_tenant, _ana, other.Id)).Should().BeNull();
    }

    [Fact]
    public async Task CountCompletedSince_CountsOnlyCompletedOnesFromTheGivenMoment()
    {
        var old = Seed("antiga", Today, completed: true);
        Seed("pendente", Today);

        (await _tasks.CountCompletedSinceAsync(_tenant, _ana, Now.AddDays(-1))).Should().Be(1);
        (await _tasks.CountCompletedSinceAsync(_tenant, _ana, Now.AddDays(1))).Should().Be(0);
        old.CompletedAt.Should().Be(Now);
    }

    [Fact]
    public async Task ListDueBetween_IsInclusiveAtBothEnds_AndIncludesCompletedTasks()
    {
        Seed("antes", Today.AddDays(-1));
        Seed("primeiro", Today);
        Seed("último", Today.AddDays(3), completed: true);
        Seed("depois", Today.AddDays(4));

        var due = await _tasks.ListDueBetweenAsync(_tenant, _ana, Today, Today.AddDays(3));

        Titles(due).Should().BeEquivalentTo("primeiro", "último");
    }

    [Fact]
    public async Task Events_AreReturnedOnlyForTheirOwner_InsideTheHalfOpenRange()
    {
        var from = new DateTimeOffset(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 11, 14, 3, 0, 0, TimeSpan.Zero);
        CalendarEvent Event(string title, DateTimeOffset at, Guid owner) =>
            CalendarEvent.Create(_tenant, owner, title, null, CalendarEventType.Meeting, at, null, null, null, Now).Value;

        _db.CalendarEvents.AddRange(
            Event("no limite inferior", from, _ana),
            Event("dentro", from.AddHours(10), _ana),
            Event("no limite superior (fora)", to, _ana),
            Event("antes", from.AddSeconds(-1), _ana),
            Event("do Bruno", from.AddHours(5), _bruno));
        _db.SaveChanges();

        var result = await _events.ListStartingBetweenAsync(_tenant, _ana, from, to);

        result.Select(e => e.Title).Should().Equal("no limite inferior", "dentro");
    }
}
