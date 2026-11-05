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
/// O EF em memória não traduz LINQ para SQL: um filtro que ele aceita pode não virar SQL válido no
/// Postgres. Estes testes rodam as consultas dos repositórios contra o banco de verdade (como
/// superusuário — o RLS é provado à parte, em 015_workflow.sql).
/// </summary>
public sealed class PostgresTranslationTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly WorkflowDbContext? _db;

    public PostgresTranslationTests()
    {
        var connection = Environment.GetEnvironmentVariable(RequiresDatabaseFactAttribute.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        var tenantContext = new AsyncLocalTenantContext();
        tenantContext.SetTenant(_tenant);
        var options = new DbContextOptionsBuilder<WorkflowDbContext>().UseNpgsql(connection).UseSnakeCaseNamingConvention().Options;
        _db = new WorkflowDbContext(options, tenantContext);
    }

    public void Dispose() => _db?.Dispose();

    private WorkTask Seed(string title, DateOnly due, bool completed = false)
    {
        var task = WorkTask.Create(_tenant, _user, title, null, due, null, Now).Value;
        if (completed)
        {
            task.Complete(Now);
        }

        _db!.WorkTasks.Add(task);
        _db.SaveChanges();
        return task;
    }

    [RequiresDatabaseFact]
    public async Task EveryTaskFilter_TranslatesToValidSqlAndReturnsTheRightRows()
    {
        Seed("atrasada", Today.AddDays(-2));
        Seed("hoje", Today);
        Seed("em 7", Today.AddDays(7));
        Seed("em 8", Today.AddDays(8));
        Seed("concluída", Today.AddDays(-9), completed: true);
        var repo = new WorkTaskRepository(_db!);

        async Task<string[]> Run(TaskDueFilter filter, WorkTaskStatus? status = null) =>
            (await repo.ListAsync(_tenant, _user, new WorkTaskQuery(status, filter, Today), 0, 50)).Select(t => t.Title).ToArray();

        (await Run(TaskDueFilter.All)).Should().Equal("concluída", "atrasada", "hoje", "em 7", "em 8");
        (await Run(TaskDueFilter.Today)).Should().Equal("hoje");
        (await Run(TaskDueFilter.Overdue)).Should().Equal("atrasada");
        (await Run(TaskDueFilter.Next7Days)).Should().Equal("hoje", "em 7");
        (await Run(TaskDueFilter.All, WorkTaskStatus.Completed)).Should().Equal("concluída");
        (await repo.CountAsync(_tenant, _user, new WorkTaskQuery(WorkTaskStatus.Pending, TaskDueFilter.All, Today))).Should().Be(4);
        (await repo.CountCompletedSinceAsync(_tenant, _user, Now.AddDays(-1))).Should().Be(1);
    }

    [RequiresDatabaseFact]
    public async Task TaskAndEventRangeQueries_TranslateToValidSql()
    {
        Seed("no intervalo", Today);
        Seed("fora", Today.AddDays(40));
        var from = new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero);
        _db!.CalendarEvents.Add(CalendarEvent.Create(_tenant, _user, "audiência", null, CalendarEventType.Hearing, from.AddHours(14), null, null, null, Now).Value);
        _db.SaveChanges();

        var tasks = await new WorkTaskRepository(_db).ListDueBetweenAsync(_tenant, _user, Today, Today.AddDays(5));
        var events = await new CalendarEventRepository(_db).ListStartingBetweenAsync(_tenant, _user, from, from.AddDays(1));

        tasks.Select(t => t.Title).Should().Equal("no intervalo");
        events.Select(e => e.Title).Should().Equal("audiência");
    }

    [RequiresDatabaseFact]
    public async Task DueDateIsStoredAsADate_NotAnInstant()
    {
        var task = Seed("dia exato", new DateOnly(2026, 11, 12));

        var raw = await _db!.Database
            .SqlQuery<string>($"select to_char(due_date, 'YYYY-MM-DD') as \"Value\" from work_tasks where id = {task.Id.Value}")
            .SingleAsync();

        raw.Should().Be("2026-11-12");
    }
}
