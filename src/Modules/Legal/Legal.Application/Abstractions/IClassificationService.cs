using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Ai;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Classifica uma publicação do DJEN (tipo de ato + prazo inicial) com IA. O resultado é só
/// uma SUGESTÃO: nada aqui altera a publicação nem cria prazo — isso depende da revisão
/// humana (ADR-012). Falhas voltam como <see cref="Result"/> de erro (INTEGRATION_* quando o
/// provedor falha, AI_INVALID_RESPONSE quando a resposta não passa na validação).
/// </summary>
public interface IClassificationService
{
    /// <param name="rawContent">Inteiro teor — dado sigiloso: nunca logar.</param>
    /// <param name="tribunal">Sigla do tribunal (ex.: TJCE); nulo quando desconhecido.</param>
    /// <param name="area">Área do direito do processo; nulo quando a publicação não está vinculada.</param>
    /// <param name="cancellationToken">Cancelamento da chamada.</param>
    Task<Result<ClassificationResult>> ClassifyAsync(
        string rawContent, string? tribunal, string? area, CancellationToken cancellationToken = default);
}
