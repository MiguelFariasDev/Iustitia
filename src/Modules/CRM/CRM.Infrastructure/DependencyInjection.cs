using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.CRM.Application;
using Advocacia.Modules.CRM.Application.Abstractions;
using Advocacia.Modules.CRM.Infrastructure.Persistence;
using Advocacia.Modules.CRM.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.CRM.Infrastructure;

/// <summary>Composition root do módulo CRM. Deve ser chamado DEPOIS de <c>AddPlatformInfrastructure</c>.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCrmInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddCrmApplication(configuration);

        services.AddDbContext<CrmDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<ICrmUnitOfWork, CrmUnitOfWork>();

        return services;
    }
}
