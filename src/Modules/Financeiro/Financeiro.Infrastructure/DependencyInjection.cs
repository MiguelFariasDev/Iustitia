using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Financeiro.Application;
using Advocacia.Modules.Financeiro.Application.Abstractions;
using Advocacia.Modules.Financeiro.Infrastructure.Persistence;
using Advocacia.Modules.Financeiro.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Financeiro.Infrastructure;

/// <summary>Composition root do módulo Financeiro. Deve ser chamado DEPOIS de <c>AddPlatformInfrastructure</c>.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFinanceiroInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddFinanceiroApplication(configuration);

        services.AddDbContext<FinanceiroDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<ILancamentoRepository, LancamentoRepository>();
        services.AddScoped<IFinanceiroUnitOfWork, FinanceiroUnitOfWork>();

        return services;
    }
}
