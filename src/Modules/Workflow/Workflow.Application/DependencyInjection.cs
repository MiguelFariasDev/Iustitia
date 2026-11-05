using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Workflow.Application;

/// <summary>
/// Composition root de Workflow.Application. Os behaviors abertos do MediatR (Logging, Validation…)
/// NÃO são registrados de novo aqui: valem para todo IRequest do container, e registrá-los de novo
/// faria cada um rodar duas vezes (ADR-030).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(mediatr => mediatr.RegisterServicesFromAssembly(WorkflowApplicationAssembly.Reference));
        services.AddValidatorsFromAssembly(WorkflowApplicationAssembly.Reference);

        services.Configure<WorkflowOptions>(configuration.GetSection(WorkflowOptions.SectionName));
        services.AddScoped<BusinessCalendar>();

        return services;
    }
}
