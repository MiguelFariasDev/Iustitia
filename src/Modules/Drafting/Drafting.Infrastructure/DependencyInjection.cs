using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Drafting.Application;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Infrastructure.Normas;
using Advocacia.Modules.Drafting.Infrastructure.BackgroundJobs;
using Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;
using Advocacia.Modules.Drafting.Infrastructure.Exportacao;
using Advocacia.Modules.Drafting.Infrastructure.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Metricas;
using Advocacia.Modules.Drafting.Infrastructure.Ocr;
using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Infrastructure.Qdrant;
using Advocacia.Modules.Drafting.Infrastructure.Storage;
using Advocacia.Modules.Drafting.Infrastructure.Transcricao;
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
/// <see cref="ILlmService"/>, <see cref="IEmbeddingService"/> ou <see cref="ICasoVectorStore"/> aqui.
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
        services.Configure<AnexosOptions>(configuration.GetSection(AnexosOptions.SectionName));
        services.Configure<ConversaoOptions>(configuration.GetSection(ConversaoOptions.SectionName));
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.Configure<TranscricaoOptions>(configuration.GetSection(TranscricaoOptions.SectionName));
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
        services.Configure<VastAiOptions>(configuration.GetSection(VastAiOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        // A mesma seção, só com o que a Application precisa para dimensionar o contexto.
        services.Configure<JanelaModeloOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));
        // Os modelos que a GPU precisa ter instalados para ser considerada pronta.
        services.AddOptions<OllamaModelsOptions>()
            .Configure<IOptions<OllamaOptions>>((models, ollama) =>
                models.Required = new[] { ollama.Value.Modelo, ollama.Value.ModeloEmbedding, ollama.Value.ModeloExtracao }
                    .OfType<string>().Distinct().ToArray());

        services.AddDbContext<DraftingDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<ICasoRepository, CasoRepository>();
        services.AddScoped<ICasoDocumentoRepository, CasoDocumentoRepository>();
        services.AddScoped<IEventoSegurancaRepository, EventoSegurancaRepository>();
        services.AddScoped<IGpuInstanceLogRepository, GpuInstanceLogRepository>();
        services.AddScoped<IDraftingUnitOfWork, DraftingUnitOfWork>();
        services.AddScoped<IGeracaoPecaFila, GeracaoPecaFila>();
        services.AddScoped<IPecaMetricasService, PecaMetricasService>();

        // Conversão de anexos (worker, só CPU): um conversor por formato.
        services.AddSingleton<IOcrService, TesseractOcrService>();
        services.AddHttpClient<ITranscricaoService, HttpTranscricaoService>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<LibreOfficeConversor>();
        services.AddScoped<IConversorFormato, PdfConversor>();
        services.AddScoped<IConversorFormato, DocxConversor>();
        services.AddScoped<IConversorFormato, DocConversor>();
        services.AddScoped<IConversorFormato, TextoConversor>();
        services.AddScoped<IConversorFormato, ImagemConversor>();
        services.AddScoped<IConversorFormato, PlanilhaConversor>();
        services.AddScoped<IConversorFormato, EmailConversor>();
        services.AddScoped<IConversorFormato, AudioConversor>();
        services.AddSingleton<IExportadorDocx, ExportadorDocx>();
        services.AddScoped<ICasoArquivoStore, CasoArquivoStore>();

        // Endereço atual do Ollama (fixo ou descoberto pela Vast.ai) e o cliente que o usa.
        services.AddSingleton<IOllamaEndpoint, OllamaEndpoint>();
        // SEM retry automático e SEM timeout do HttpClient: o prazo da geração é controlado no próprio
        // serviço (CancelAfter), e repetir um POST de minutos dobraria o custo de GPU.
        services.AddHttpClient<OllamaService>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<IOllamaService>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        services.AddTransient<ILlmService>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        services.AddTransient<ILlmEstruturado>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        services.AddTransient<ILlmHealth>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        // Embeddings passam por um cache por hash do conteúdo: o mesmo texto não paga GPU duas vezes.
        services.AddTransient<IEmbeddingService>(serviceProvider => new CachedEmbeddingService(
            serviceProvider.GetRequiredService<OllamaService>(), serviceProvider.GetRequiredService<EmbeddingCache>()));

        // API da Vast.ai: só HTTPS (validado no serviço) e timeout curto por chamada.
        services.AddHttpClient<IVastAiService, VastAiService>(client => client.Timeout = TimeSpan.FromSeconds(30));

        // Acervo normativo público (ADR-073): configuração, persistência, storage, Qdrant e git.
        services.Configure<NormasIngestaoOptions>(configuration.GetSection(NormasIngestaoOptions.SectionName));
        services.Configure<ChunkingOptions>(configuration.GetSection(ChunkingOptions.SectionName));
        services.Configure<BuscaNormasOptions>(configuration.GetSection(BuscaNormasOptions.SectionName));
        services.AddScoped<NormaRepository>();
        services.AddScoped<INormaStore>(serviceProvider => serviceProvider.GetRequiredService<NormaRepository>());
        services.AddScoped<INormaSearchIndex>(serviceProvider => serviceProvider.GetRequiredService<NormaRepository>());
        if (string.Equals(configuration["Ingestao:Modo"], "Git", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IRepositorySync, GitSparseRepositorySync>();
        }
        else
        {
            services.AddSingleton<IRepositorySync, LocalDirectoryRepositorySync>();
        }

        if (string.Equals(configuration["Ingestao:Storage:Provedor"], "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IObjectStorage, S3ObjectStorage>(client => client.Timeout = TimeSpan.FromMinutes(2));
        }
        else
        {
            services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        }

        // Um cliente gRPC por processo. Dois acessos com regras diferentes: acervo público e anexos (tenant + caso).
        services.AddSingleton<QdrantConexao>();
        services.AddSingleton<IQdrantService, QdrantService>();
        services.AddSingleton<INormaVectorStore, NormaQdrantStore>();
        services.AddSingleton<ICasoVectorStore, CasoQdrantStore>();

        return services;
    }

    /// <summary>Só para o host de jobs: o laço que consome a fila e liga/desliga a GPU.</summary>
    public static IServiceCollection AddDraftingWorker(this IServiceCollection services)
    {
        services.AddHostedService<GeracaoPecaWorker>();
        services.AddHostedService<NormaIngestaoWorker>();

        return services;
    }
}
