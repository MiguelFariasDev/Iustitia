using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;

public static class JuscraperServiceCollectionExtensions
{
    /// <summary>
    /// Registra as duas fontes da consulta de processo (juscraper e DataJud) como serviços com
    /// chave e o composto <see cref="ConsultaProcessoTribunalComFallback"/> como implementação de
    /// <see cref="IConsultaProcessoTribunal"/> (ADR-075).
    /// </summary>
    public static IServiceCollection AddConsultaProcessoTribunal(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JuscraperOptions.SectionName);
        services.Configure<JuscraperOptions>(section);
        var options = section.Get<JuscraperOptions>() ?? new JuscraperOptions();

        AddJuscraperHttpClient(services, JuscraperClient.ConsultaHttpClient, options, options.ConsultaTimeout);
        AddJuscraperHttpClient(services, JuscraperClient.AcompanhamentoHttpClient, options, options.AcompanhamentoTimeout);

        services.AddSingleton<ConsultaTribunalTelemetry>();

        services.AddScoped<JuscraperConsultaProcessoTribunal>();
        services.AddScoped<DataJudConsultaProcessoTribunal>();
        services.AddKeyedScoped<IConsultaProcessoTribunal>(
            FonteConsulta.Juscraper, (provider, _) => provider.GetRequiredService<JuscraperConsultaProcessoTribunal>());
        services.AddKeyedScoped<IConsultaProcessoTribunal>(
            FonteConsulta.DataJud, (provider, _) => provider.GetRequiredService<DataJudConsultaProcessoTribunal>());
        services.AddScoped<IConsultaProcessoTribunal, ConsultaProcessoTribunalComFallback>();

        return services;
    }

    private static void AddJuscraperHttpClient(
        IServiceCollection services, string nome, JuscraperOptions options, TimeSpan tentativa)
    {
        services
            .AddHttpClient(nome, client =>
            {
                // Sem BaseUrl o juscraper está desligado e o cliente nunca é usado; um Uri vazio
                // aqui derrubaria a criação do cliente.
                if (options.Habilitado)
                {
                    client.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
                }

                // O tempo fica a cargo do pipeline de resiliência.
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                }
            })
            .AddStandardResilienceHandler(resiliencia => ConfigurarResiliencia(resiliencia, tentativa));
    }

    /// <summary>
    /// UMA nova tentativa no máximo: o juscraper já repete com backoff contra o portal, e repetir
    /// mais aqui multiplicaria as requisições ao tribunal. O circuito exige janela de amostragem de
    /// pelo menos 2x o tempo da tentativa (senão o Polly recusa a configuração).
    /// </summary>
    internal static void ConfigurarResiliencia(HttpStandardResilienceOptions resiliencia, TimeSpan tentativa)
    {
        resiliencia.AttemptTimeout.Timeout = tentativa;
        resiliencia.Retry.MaxRetryAttempts = 1;
        resiliencia.Retry.Delay = TimeSpan.FromSeconds(1);
        resiliencia.TotalRequestTimeout.Timeout = tentativa * 2 + TimeSpan.FromSeconds(5);
        resiliencia.CircuitBreaker.SamplingDuration = tentativa * 2 + TimeSpan.FromSeconds(1);
    }
}
