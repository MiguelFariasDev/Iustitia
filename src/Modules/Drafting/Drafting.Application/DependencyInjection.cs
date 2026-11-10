using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Drafting.Application;

/// <summary>Composition root de Drafting.Application.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDraftingApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddSingleton<EmbeddingCache>();
        services.AddSingleton<IGpuStateService, GpuStateService>();
        services.AddSingleton<FilaCoordinator>();

        services.AddScoped<IRagService, RagService>();
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();
        services.AddScoped<IDraftingAuditService, DraftingAuditService>();
        services.AddScoped<INotificacaoService, NotificacaoService>();
        services.AddScoped<IGeracaoPecaService, GeracaoPecaService>();
        services.AddScoped<IDocumentIndexer, DocumentIndexer>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        services.AddScoped<IGpuOrchestrator, GpuOrchestrator>();

        // Acervo normativo (ADR-073): ingestão incremental de Markdown, busca híbrida, reranking e validação de citações.
        services.AddSingleton(serviceProvider => new NormaChunker(serviceProvider.GetRequiredService<IOptions<ChunkingOptions>>().Value));
        services.AddSingleton<INormaReranker, RuleBasedNormaReranker>();
        services.AddSingleton<INormaSyncOrchestrator, NormaSyncOrchestrator>();
        services.AddScoped<INormaIngestionService, NormaIngestionService>();
        services.AddScoped<IHybridSearchService, HybridSearchService>();
        services.AddScoped<NormaCitationService>();
        services.AddScoped<INormaAdminService, NormaAdminService>();

        // Quem sabe processar cada tipo de item da fila (o coordenador escolhe pelo Tipo).
        services.AddScoped<IQueueWorkHandler, PecaProcessor>();
        services.AddScoped<IQueueWorkHandler, IndexacaoWorkHandler>();

        return services;
    }
}
