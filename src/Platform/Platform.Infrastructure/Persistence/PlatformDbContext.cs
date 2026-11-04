using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.Platform.Domain.Alerts;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.FeatureFlags;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Settings;
using Advocacia.Platform.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Platform (núcleo compartilhado — ver ADR-001). As configurações
/// Fluent API são descobertas automaticamente por <see cref="BaseDbContext"/> via
/// ApplyConfigurationsFromAssembly a partir do assembly deste tipo; os filtros globais
/// de soft delete e isolamento por tenant também são aplicados centralmente lá.
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
}
