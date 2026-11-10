using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
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

        // Quem sabe processar cada tipo de item da fila (o coordenador escolhe pelo Tipo).
        services.AddScoped<IQueueWorkHandler, PecaProcessor>();
        services.AddScoped<IQueueWorkHandler, IndexacaoWorkHandler>();

        return services;
    }
}
