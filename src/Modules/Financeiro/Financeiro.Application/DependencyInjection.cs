using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advocacia.Modules.Financeiro.Application;

/// <summary>Composition root de Financeiro.Application (ver Legal.Application para o porquê do TryAdd).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFinanceiroApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(mediatr => mediatr.RegisterServicesFromAssembly(FinanceiroApplicationAssembly.Reference));
        services.AddValidatorsFromAssembly(FinanceiroApplicationAssembly.Reference);

        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(FinanceiroApplicationAssembly.Reference);
        services.TryAddSingleton(typeAdapterConfig);
        services.TryAddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
