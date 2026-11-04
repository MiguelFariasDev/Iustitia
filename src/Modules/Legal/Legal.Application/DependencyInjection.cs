using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advocacia.Modules.Legal.Application;

/// <summary>
/// Composition root de Legal.Application. Registra os handlers/validators/profiles deste
/// assembly no MediatR, FluentValidation e Mapster.
///
/// Os behaviors abertos (Logging/Validation/Performance/Transaction) NÃO são registrados de
/// novo aqui: MediatR os aplica a todo IRequest do container, independentemente do assembly
/// de origem, e registrar de novo faria cada behavior rodar duas vezes (ver ADR-030).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLegalApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(mediatr => mediatr.RegisterServicesFromAssembly(LegalApplicationAssembly.Reference));

        services.AddValidatorsFromAssembly(LegalApplicationAssembly.Reference);

        // Mapster: o Scan é sempre necessário (adiciona os profiles DESTE assembly ao config
        // global), mas IMapper/TypeAdapterConfig podem já ter sido registrados pelo host via
        // AddPlatformApplication — daí o TryAdd.
        //
        // O TryAdd não é cosmético: o Worker NÃO chama AddPlatformApplication (não serve
        // requisições, só roda jobs e consumers), então sem este registro os handlers de
        // query de Legal ficam sem IMapper e o host falha na validação do container ao subir,
        // não em runtime — erro que só aparece rodando o Worker, nunca a Api.
        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(LegalApplicationAssembly.Reference);
        services.TryAddSingleton(typeAdapterConfig);
        services.TryAddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
