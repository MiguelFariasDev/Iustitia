using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Workflow.Infrastructure;

/// <summary>
/// Composition root do Workflow.Infrastructure. Nesta etapa só hospeda o catálogo de prompts
/// versionados; a revisão humana e a fila de trabalho chegam nas Etapas 3.3 e 3.4.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPromptCatalog, EmbeddedPromptCatalog>();

        return services;
    }
}
