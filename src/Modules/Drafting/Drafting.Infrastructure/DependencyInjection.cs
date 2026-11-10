using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Drafting.Application;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Infrastructure.BackgroundJobs;
using Advocacia.Modules.Drafting.Infrastructure.Extracao;
using Advocacia.Modules.Drafting.Infrastructure.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Metricas;
using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Infrastructure.Qdrant;
using Advocacia.Modules.Drafting.Infrastructure.VastAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure;

/// <summary>
/// Composition root do módulo Drafting (geração assistida de peças com RAG local, ADR-072).
/// Deve ser chamado DEPOIS de <c>AddPlatformInfrastructure</c>.
///
/// Trocar de modelo, de provedor de LLM ou de banco vetorial = registrar outra implementação de
/// <see cref="ILlmService"/>, <see cref="IEmbeddingService"/> ou <see cref="IVectorStore"/> aqui.
/// A API usa este método; o host de jobs chama também <see cref="AddDraftingWorker"/>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDraftingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddDraftingApplication();

        services.Configure<DraftingOptions>(configuration.GetSection(DraftingOptions.SectionName));
        services.Configure<FilaOptions>(configuration.GetSection(FilaOptions.SectionName));
        services.Configure<CustoOptions>(configuration.GetSection(CustoOptions.SectionName));
        services.Configure<IngestaoOptions>(configuration.GetSection(IngestaoOptions.SectionName));
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
        services.Configure<VastAiOptions>(configuration.GetSection(VastAiOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));
        // Os modelos que a GPU precisa ter instalados para ser considerada pronta.
        services.AddOptions<OllamaModelsOptions>()
            .Configure<IOptions<OllamaOptions>>((models, ollama) =>
                models.Required = [ollama.Value.Modelo, ollama.Value.ModeloEmbedding]);

        services.AddDbContext<DraftingDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<IIndexedDocumentRepository, IndexedDocumentRepository>();
        services.AddScoped<IGpuInstanceLogRepository, GpuInstanceLogRepository>();
        services.AddScoped<IDraftingUnitOfWork, DraftingUnitOfWork>();
        services.AddScoped<IGeracaoPecaFila, GeracaoPecaFila>();
        services.AddScoped<IPecaMetricasService, PecaMetricasService>();
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();

        // Endereço atual do Ollama (fixo ou descoberto pela Vast.ai) e o cliente que o usa.
        services.AddSingleton<IOllamaEndpoint, OllamaEndpoint>();
        // SEM retry automático e SEM timeout do HttpClient: o prazo da geração é controlado no próprio
        // serviço (CancelAfter), e repetir um POST de minutos dobraria o custo de GPU.
        services.AddHttpClient<OllamaService>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<IOllamaService>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        services.AddTransient<ILlmService>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        services.AddTransient<ILlmHealth>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        // Embeddings passam por um cache por hash do conteúdo: o mesmo texto não paga GPU duas vezes.
        services.AddTransient<IEmbeddingService>(serviceProvider => new CachedEmbeddingService(
            serviceProvider.GetRequiredService<OllamaService>(), serviceProvider.GetRequiredService<EmbeddingCache>()));

        // API da Vast.ai: só HTTPS (validado no serviço) e timeout curto por chamada.
        services.AddHttpClient<IVastAiService, VastAiService>(client => client.Timeout = TimeSpan.FromSeconds(30));

        // O cliente gRPC do Qdrant é thread-safe e caro de criar: um só por processo.
        services.AddSingleton<QdrantService>();
        services.AddSingleton<IVectorStore>(serviceProvider => serviceProvider.GetRequiredService<QdrantService>());

        return services;
    }

    /// <summary>Só para o host de jobs: o laço que consome a fila e liga/desliga a GPU.</summary>
    public static IServiceCollection AddDraftingWorker(this IServiceCollection services)
    {
        services.AddHostedService<GeracaoPecaWorker>();

        return services;
    }
}
