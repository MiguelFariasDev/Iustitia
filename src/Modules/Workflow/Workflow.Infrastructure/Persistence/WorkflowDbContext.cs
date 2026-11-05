using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.Workflow.Domain.Agenda;
using Advocacia.Modules.Workflow.Domain.WorkTasks;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Workflow — um por módulo, mesmo banco físico (ADR-001). Os filtros globais de
/// soft delete e de tenant vêm de <see cref="BaseDbContext"/>; as configurações Fluent API são
/// descobertas deste assembly.
///
/// Não mapeia <c>outbox_messages</c> (como Legal faz): os agregados daqui não levantam eventos de
/// domínio ainda, e o OutboxInterceptor só toca a tabela quando há evento. Quando o primeiro evento
/// surgir, é preciso mapear a tabela aqui, com ExcludeFromMigrations (ver Legal.Infrastructure).
/// </summary>
public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();

    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
}
