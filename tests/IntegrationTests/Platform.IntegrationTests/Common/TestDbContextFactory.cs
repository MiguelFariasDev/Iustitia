using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Identity;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.BuildingBlocks.Infrastructure.Time;
using Advocacia.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Monta um <see cref="PlatformDbContext"/> com os três interceptors reais
/// (Audit/Outbox/TenantContext) ligados, do mesmo jeito que a Etapa seguinte fará via
/// DI — sem precisar montar um <see cref="IServiceProvider"/> inteiro nos testes.
/// </summary>
public static class TestDbContextFactory
{
    public static PlatformDbContext Create(
        string connectionString,
        ITenantContext? tenantContext = null,
        ICurrentUser? currentUser = null)
    {
        var effectiveTenantContext = tenantContext ?? new AsyncLocalTenantContext();
        var effectiveCurrentUser = currentUser ?? new AnonymousCurrentUser();

        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();
        optionsBuilder.AddInterceptors(
            new AuditInterceptor(effectiveCurrentUser, new DateTimeProvider()),
            new OutboxInterceptor(),
            new TenantContextInterceptor(effectiveTenantContext));

        return new PlatformDbContext(optionsBuilder.Options, effectiveTenantContext);
    }
}
