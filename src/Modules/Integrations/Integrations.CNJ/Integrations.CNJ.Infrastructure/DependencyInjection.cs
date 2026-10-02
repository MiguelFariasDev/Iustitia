using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.HealthChecks;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure;

/// <summary>
/// Composition root da integração com o DJEN/CNJ. Chamado pelos hosts (Api/Worker) e por
/// <c>AddLegalInfrastructure</c>, que depende de <see cref="ICnjClient"/> para o job de
/// captura. Registrar duas vezes é inofensivo (AddHttpClient é idempotente por nome).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCnjIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CnjOptions.SectionName);
        services.Configure<CnjOptions>(section);

        var options = section.Get<CnjOptions>() ?? new CnjOptions();

        var httpClientBuilder = services.AddHttpClient<ICnjClient, DjenClient>(DjenClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            // Timeout fica a cargo do pipeline de resiliência (AttemptTimeout/TotalRequestTimeout);
            // InfiniteTimeSpan aqui evita que o timeout do HttpClient corte o retry pela metade.
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Ordem do pipeline (de fora para dentro): rate limiter -> resiliência -> rede.
        // O limitador precisa vir ANTES da resiliência para que o retry só enxergue falhas
        // que não sejam auto-infligidas por estourar o limite do CNJ (ver
        // CnjRateLimitingHandler e ADR-059).
        //
        // Singleton: o orçamento de taxa é do PROCESSO inteiro. Registrado como transient
        // (o padrão de AddHttpMessageHandler), cada HttpClient teria o seu próprio e o
        // limite global não seria respeitado.
        services.AddSingleton<CnjRateLimitingHandler>();
        httpClientBuilder.AddHttpMessageHandler(serviceProvider =>
            serviceProvider.GetRequiredService<CnjRateLimitingHandler>());

        CnjResiliencePipeline.Configure(httpClientBuilder.AddStandardResilienceHandler(), options);

        // Tag "cnj" e não "ready": ver o comentário em CnjHealthCheck. AddHealthChecks é
        // idempotente e devolve o mesmo builder já usado por AddAdvocaciaHealthChecks.
        services.AddHealthChecks().AddCheck<CnjHealthCheck>("cnj", tags: ["cnj"]);

        AddDataJud(services, configuration);

        return services;
    }

    /// <summary>
    /// API Pública do DataJud — metadados de processo, usados no auto-preenchimento do
    /// cadastro. É um serviço SEPARADO do DJEN: outro endereço, outra autenticação, outro
    /// formato (Elasticsearch DSL). USO RESTRITO A PORTFÓLIO (ver ADR-064).
    /// </summary>
    private static void AddDataJud(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(DataJudOptions.SectionName);
        services.Configure<DataJudOptions>(section);

        var options = section.Get<DataJudOptions>() ?? new DataJudOptions();

        services.AddSingleton<DataJudTelemetry>();

        var builder = services.AddHttpClient<IDataJudClient, DataJudClient>(DataJudClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            // Timeout fica a cargo do pipeline de resiliência — ver o mesmo comentário no DJEN.
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // O DataJud usa o esquema "APIKey", não "Bearer". Sem chave configurada o header
            // não é enviado: a API responde 401 e o erro chega como CNJ_AUTH_FAILED, que é
            // mais legível do que um header vazio virando 400.
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("APIKey", options.ApiKey);
            }
        });

        DataJudResiliencePipeline.Configure(builder.AddStandardResilienceHandler(), options);
    }
}
