using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;

namespace Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;

/// <summary>
/// Porta de saída para a Messages API da Anthropic. Falhas de integração voltam como
/// <see cref="Result"/> de erro (faixa INTEGRATION do catálogo), nunca como exception.
/// Quem consome (ex.: ClassificationService, no módulo Legal) depende só desta interface.
/// </summary>
public interface IAnthropicClient
{
    Task<Result<AnthropicResponse>> SendAsync(AnthropicRequest request, CancellationToken cancellationToken = default);
}
