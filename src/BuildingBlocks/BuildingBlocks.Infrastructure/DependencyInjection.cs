using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.BuildingBlocks.Infrastructure.Identity;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.BuildingBlocks.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.BuildingBlocks.Infrastructure;

/// <summary>
/// Registra os serviços de infraestrutura compartilhados por todos os módulos
/// (Platform e, a partir da Fase 1, Legal/Workflow/...). Cada módulo chama este método
/// e, em seguida, registra seu próprio DbContext, UnitOfWork e repositórios concretos
/// (ver <c>Platform.Infrastructure.DependencyInjection.AddPlatformInfrastructure</c>).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Padrões seguros ("não autenticado" / "sem tenant"). Platform.Infrastructure
        // substitui por implementações reais baseadas em HttpContext/JWT na Etapa 0.3.
        services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
        services.AddScoped<ITenantContext, AsyncLocalTenantContext>();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6380";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        services.AddScoped<AuditInterceptor>();
        services.AddScoped<OutboxInterceptor>();
        services.AddScoped<TenantContextInterceptor>();

        return services;
    }
}
