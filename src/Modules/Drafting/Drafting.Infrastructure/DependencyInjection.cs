using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.Modules.Drafting.Application;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Infrastructure.Qdrant;
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
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDraftingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddDraftingApplication();

        services.Configure<DraftingOptions>(configuration.GetSection(DraftingOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));
        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            // Qdrant:LimiteResultados é a fonte da verdade do número de trechos (contrato do appsettings).
            .Configure<IOptions<QdrantOptions>>((rag, qdrant) => rag.LimiteResultados = qdrant.Value.LimiteResultados);

        services.AddDbContext<DraftingDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<TenantContextInterceptor>());
        });

        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<IDraftingUnitOfWork, DraftingUnitOfWork>();

        // Um HttpClient tipado para as duas portas: o mesmo servidor Ollama gera texto e embeddings.
        // SEM retry automático: repetir um POST de geração de minutos dobraria o custo de GPU.
        services.AddHttpClient<OllamaService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
            var problem = options.Validate();
            if (problem is not null)
            {
                throw new InvalidOperationException(problem);
            }

            client.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
            // O prazo da geração é controlado no próprio serviço (CancelAfter), não pelo HttpClient.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddTransient<ILlmService>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());
        services.AddTransient<IEmbeddingService>(serviceProvider => serviceProvider.GetRequiredService<OllamaService>());

        // O cliente gRPC do Qdrant é thread-safe e caro de criar: um só por processo.
        services.AddSingleton<QdrantService>();
        services.AddSingleton<IVectorStore>(serviceProvider => serviceProvider.GetRequiredService<QdrantService>());

        return services;
    }
}
