using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Identity;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.BuildingBlocks.Infrastructure.Time;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.IntegrationTests.Common;

/// <summary>
/// Monta um <see cref="LegalDbContext"/> com os três interceptors reais ligados
/// (Audit/Outbox/TenantContext), do mesmo jeito que AddLegalInfrastructure faz via DI — sem
/// precisar montar um IServiceProvider inteiro (ver TestDbContextFactory em
/// Platform.IntegrationTests).
/// </summary>
internal static class TestLegalDbContextFactory
{
    internal static LegalDbContext Create(
        string connectionString,
        ITenantContext? tenantContext = null,
        ICurrentUser? currentUser = null)
    {
        var effectiveTenantContext = tenantContext ?? new AsyncLocalTenantContext();
        var effectiveCurrentUser = currentUser ?? new AnonymousCurrentUser();

        var optionsBuilder = new DbContextOptionsBuilder<LegalDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();
        optionsBuilder.AddInterceptors(
            new AuditInterceptor(effectiveCurrentUser, new DateTimeProvider()),
            new OutboxInterceptor(),
            new TenantContextInterceptor(effectiveTenantContext));

        return new LegalDbContext(optionsBuilder.Options, effectiveTenantContext);
    }

    internal static ITenantContext TenantContextFor(Guid tenantId)
    {
        var tenantContext = new AsyncLocalTenantContext();
        tenantContext.SetTenant(tenantId);

        return tenantContext;
    }
}
