using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;

namespace Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;

/// <summary>
/// Catálogo de prompts versionados (arquivos .md). A porta fica aqui, e não no módulo Legal,
/// porque quem guarda os arquivos é o Workflow.Infrastructure (ver ADR-071): nenhum módulo de
/// negócio depende da infraestrutura do outro.
/// </summary>
public interface IPromptCatalog
{
    /// <param name="name">Nome do prompt sem extensão (classification, deadline).</param>
    Result<PromptTemplate> Get(string name);
}
