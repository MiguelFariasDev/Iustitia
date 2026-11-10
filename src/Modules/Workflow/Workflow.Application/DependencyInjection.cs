using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advocacia.Modules.Workflow.Application;

/// <summary>Composition root de Workflow.Application (ver Legal.Application para o porquê do TryAdd).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(mediatr => mediatr.RegisterServicesFromAssembly(WorkflowApplicationAssembly.Reference));
        services.AddValidatorsFromAssembly(WorkflowApplicationAssembly.Reference);

        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(WorkflowApplicationAssembly.Reference);
        services.TryAddSingleton(typeAdapterConfig);
        services.TryAddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
