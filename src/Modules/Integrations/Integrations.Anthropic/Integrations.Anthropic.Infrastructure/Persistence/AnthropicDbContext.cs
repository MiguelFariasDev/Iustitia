using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.Integrations.Anthropic.Domain.Usage;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Integrations.Anthropic — um DbContext por módulo, mesmo banco físico
/// (ADR-001). Dono da tabela <c>ai_usage_logs</c>. Os filtros globais de tenant vêm de
/// <see cref="BaseDbContext"/>; o RLS está em infra/supabase/migrations/015_ai_usage_logs.sql.
/// </summary>
public sealed class AnthropicDbContext(DbContextOptions<AnthropicDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
}
