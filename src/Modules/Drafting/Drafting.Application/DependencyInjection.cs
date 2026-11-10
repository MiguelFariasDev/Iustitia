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
        services.AddScoped<IRagService, RagService>();
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();
        services.AddScoped<IDraftingAuditService, DraftingAuditService>();
        services.AddScoped<IPecaGenerationService, PecaGenerationService>();
        services.AddScoped<IDocumentIndexer, DocumentIndexer>();

        return services;
    }
}
