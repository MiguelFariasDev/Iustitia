using Advocacia.BuildingBlocks.Application.Behaviors;
using Advocacia.Platform.Application.Behaviors;
using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.Application;

/// <summary>
/// Composition root de Platform.Application: MediatR (handlers + pipeline de behaviors),
/// FluentValidation e Mapster — tudo que é puramente "regra de aplicação", sem
/// dependência de ASP.NET Core/Swagger (isso fica em Platform.Api.AddPlatformApi, ver
/// ADR-030). Chamado pelo host (Hosts/Api/Program.cs) antes de AddPlatformApi.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Ordem dos behaviors = ordem de registro: o primeiro registrado é o mais externo
        // do pipeline (ver ADR-030). Logging precisa envolver tudo (inclusive falhas de
        // validação); Transaction precisa ser o mais interno, para só abrir transação
        // depois que a request já passou pela validação.
        services.AddMediatR(mediatrConfiguration =>
        {
            mediatrConfiguration.RegisterServicesFromAssembly(PlatformApplicationAssembly.Reference);
            mediatrConfiguration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            mediatrConfiguration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            mediatrConfiguration.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            // Audit por FORA da transação: só registra o que foi de fato confirmado (ADR-069).
            mediatrConfiguration.AddOpenBehavior(typeof(AuditBehavior<,>));
            mediatrConfiguration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.Configure<PerformanceBehaviorOptions>(configuration.GetSection(PerformanceBehaviorOptions.SectionName));

        // CascadeMode.Stop (nível de regra E de classe) garante que ValidationBehavior
        // sempre recebe no máximo UMA falha por request — necessária para o mapeamento
        // 1:1 de ValidationFailure.ErrorCode para ErrorCode do catálogo (ver ADR-031).
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;
        ValidatorOptions.Global.LanguageManager.Culture = new System.Globalization.CultureInfo("pt-BR");

        services.AddValidatorsFromAssembly(PlatformApplicationAssembly.Reference);

        // Mapster: profiles (IRegister) do assembly são descobertos automaticamente —
        // ver Features/*/Mapping/*Profile.cs.
        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(PlatformApplicationAssembly.Reference);
        services.AddSingleton(typeAdapterConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
