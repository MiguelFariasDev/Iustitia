using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advocacia.Modules.CRM.Application;

/// <summary>
/// Composition root de CRM.Application. Os behaviors abertos do MediatR NÃO são registrados
/// de novo aqui (ver ADR-030). O TryAdd do Mapster segue o mesmo motivo de Legal: o Worker
/// não chama AddPlatformApplication.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCrmApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(mediatr => mediatr.RegisterServicesFromAssembly(CrmApplicationAssembly.Reference));
        services.AddValidatorsFromAssembly(CrmApplicationAssembly.Reference);

        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(CrmApplicationAssembly.Reference);
        services.TryAddSingleton(typeAdapterConfig);
        services.TryAddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
