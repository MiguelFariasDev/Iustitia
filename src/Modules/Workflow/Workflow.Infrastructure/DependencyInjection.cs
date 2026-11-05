using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Workflow.Application;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Infrastructure.Persistence;
using Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Workflow.Infrastructure;

/// <summary>
/// Composition root do módulo Workflow. Deve ser chamado DEPOIS de <c>AddPlatformInfrastructure</c>,
/// que registra o que é compartilhado (ITenantContext, IDateTimeProvider, interceptors, MediatR).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddWorkflowApplication(configuration);

        services.AddDbContext<WorkflowDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();

            // Os mesmos três interceptors dos demais contextos: auditoria, outbox e o SET
            // app.current_tenant para o RLS (ADR-004).
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<OutboxInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        services.AddScoped<IWorkflowUnitOfWork, WorkflowUnitOfWork>();

        return services;
    }
}
