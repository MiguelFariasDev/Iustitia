using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Casos;
using Advocacia.Modules.Drafting.Application.Conversao;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Application.Validacao;
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
        services.AddScoped<IGpuOrchestrator, GpuOrchestrator>();

        // Casos e anexos: autorização, isolamento e pipeline de conversão.
        services.AddScoped<ICasoAcessoService, CasoAcessoService>();
        services.AddScoped<ICasoService, CasoService>();
        services.AddScoped<IAnexoService, AnexoService>();
        services.AddScoped<ISegurancaIaService, SegurancaIaService>();
        services.AddScoped<IConversorDocumentoService, ConversorDocumentoService>();
        services.AddSingleton<IExtracaoMetadadosService, ExtracaoMetadadosService>();
        services.AddScoped<IClassificacaoDocumentoService, ClassificacaoDocumentoService>();
        services.AddSingleton<IChunkerJuridicoService, ChunkerJuridicoService>();

        // Recuperação: consulta estruturada, busca híbrida, reranking.
        services.AddScoped<IConsultaEnriquecidaService, ConsultaEnriquecidaService>();
        services.AddSingleton<IRerankerService, RerankerService>();
        services.AddScoped<IBuscaDocumentosCasoService, BuscaDocumentosCasoService>();
        services.AddScoped<IBuscaJuridicaService, BuscaJuridicaService>();
        services.AddScoped<IRagService, RagService>();

        // Peças: geração, conferência, revisão humana e auditoria.
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();
        services.AddScoped<IAuditoriaIaService, AuditoriaIaService>();
        services.AddScoped<INotificacaoService, NotificacaoService>();
        services.AddScoped<IGeracaoPecaService, GeracaoPecaService>();
        services.AddScoped<IValidacaoCitacaoService, ValidacaoCitacaoService>();
        services.AddScoped<IRevisaoPecaService, RevisaoPecaService>();

        // Acervo normativo (ADR-073): ingestão incremental de Markdown.
        services.AddSingleton<INormaSyncOrchestrator, NormaSyncOrchestrator>();
        services.AddScoped<INormaIngestionService, NormaIngestionService>();
        services.AddScoped<INormaAdminService, NormaAdminService>();

        // Quem sabe processar cada tipo de item da fila (o coordenador escolhe pelo Tipo).
        services.AddScoped<IQueueWorkHandler, PecaProcessor>();
        services.AddScoped<IQueueWorkHandler, ConversaoWorkHandler>();
        services.AddScoped<IQueueWorkHandler, IndexacaoWorkHandler>();

        return services;
    }
}
