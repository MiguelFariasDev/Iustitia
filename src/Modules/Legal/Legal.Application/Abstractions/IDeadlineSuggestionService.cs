using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Ai;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Sugere o prazo que a publicação abre, dado o tipo de ato já classificado. Mesmas regras de
/// <see cref="IClassificationService"/>: é sugestão, sujeita a revisão humana.
/// </summary>
public interface IDeadlineSuggestionService
{
    /// <param name="rawContent">Inteiro teor — dado sigiloso: nunca logar.</param>
    /// <param name="actType">Tipo de ato vindo da classificação (um valor de <see cref="ActTypes"/>).</param>
    /// <param name="cancellationToken">Cancelamento da chamada.</param>
    Task<Result<DeadlineSuggestion>> SuggestAsync(
        string rawContent, string actType, CancellationToken cancellationToken = default);
}
