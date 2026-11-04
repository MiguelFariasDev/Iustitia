using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure;

/// <summary>
/// Composition root da integração com a Anthropic. Chamado pelos hosts (Api/Worker); o módulo
/// Legal depende só das portas (Integrations.Anthropic.Application).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAnthropicIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AnthropicOptions.SectionName);
        services.Configure<AnthropicOptions>(section);
        services.Configure<AnthropicModelsOptions>(configuration.GetSection(AnthropicModelsOptions.SectionName));

        var options = section.Get<AnthropicOptions>() ?? new AnthropicOptions();

        var httpClientBuilder = services.AddHttpClient<IAnthropicClient, AnthropicClient>(AnthropicClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            // Timeout a cargo do pipeline de resiliência (ver o mesmo comentário no CNJ).
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        AnthropicResiliencePipeline.Configure(httpClientBuilder.AddStandardResilienceHandler(), options);

        services.AddSingleton<IAiCostCalculator, AiCostCalculator>();

        return services;
    }
}
