using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Clients;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Observability;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Resilience;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure;

/// <summary>
/// Composition root da integração com a Anthropic. Chamado pelos hosts (Api/Worker), DEPOIS de
/// <c>AddPlatformInfrastructure</c> (que registra ITenantContext, IDateTimeProvider e os
/// interceptors). Módulos de negócio dependem só das portas de Integrations.Anthropic.Application.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAnthropicIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AnthropicSettings.SectionName);
        services.Configure<AnthropicSettings>(section);
        services.Configure<AnthropicModelsOptions>(configuration.GetSection(AnthropicModelsOptions.SectionName));

        var settings = section.Get<AnthropicSettings>() ?? new AnthropicSettings();

        var httpClientBuilder = services.AddHttpClient<IAnthropicClient, AnthropicHttpClient>(AnthropicHttpClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
            // Timeout a cargo do pipeline de resiliência (ver o mesmo comentário no CNJ).
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        AnthropicResiliencePipeline.Configure(httpClientBuilder.AddStandardResilienceHandler(), settings);

        // Singleton: encapsula um Meter (instrumentos devem ser criados uma vez por processo).
        services.AddSingleton<AnthropicMetrics>();
        services.AddSingleton<IAiCostCalculator, AiCostCalculator>();

        // Auditoria de uso (ai_usage_logs). Só o TenantContextInterceptor: é ele que faz o
        // SET app.current_tenant sem o qual o RLS nega a escrita (ADR-004). Auditoria/outbox não
        // se aplicam a este agregado.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddDbContext<AnthropicDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<IAnthropicUsageLogger, AnthropicUsageLogger>();
        services.AddScoped<IAnthropicUsageQuery, AnthropicUsageQuery>();

        return services;
    }
}
