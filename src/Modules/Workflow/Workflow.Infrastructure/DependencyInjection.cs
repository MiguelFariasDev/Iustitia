using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Workflow.Application;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Infrastructure.Integrations;
using Advocacia.Modules.Workflow.Infrastructure.Jobs;
using Advocacia.Modules.Workflow.Infrastructure.Persistence;
using Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Workflow.Infrastructure;

/// <summary>Composition root do módulo Workflow. Deve ser chamado DEPOIS de <c>AddPlatformInfrastructure</c>.</summary>
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
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        services.AddScoped<IWorkflowUnitOfWork, WorkflowUnitOfWork>();
        services.AddScoped<IProcessLookup, SqlProcessLookup>();

        services.Configure<CalendarReminderJobOptions>(configuration.GetSection(CalendarReminderJobOptions.SectionName));
        services.AddScoped<CalendarReminderJob>();
        services.AddScoped<WorkflowRecurringJobsRegistrar>();

        return services;
    }
}
