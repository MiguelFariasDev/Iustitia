using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;

namespace Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;

/// <summary>
/// Porta de saída para a Messages API da Anthropic. Falhas voltam como <see cref="Result"/> de
/// erro da faixa AI do catálogo (AI_UNAUTHORIZED, AI_RATE_LIMITED, AI_TIMEOUT...), nunca como
/// exception — só o cancelamento pelo chamador propaga como OperationCanceledException.
///
/// Este cliente NÃO grava auditoria: quem chama sabe o tenant, o usuário e a finalidade, e
/// registra o uso via <see cref="IAnthropicUsageLogger"/>. Também NUNCA loga prompt nem resposta.
/// </summary>
public interface IAnthropicClient
{
    /// <summary>Envia um prompt de usuário com um system prompt e devolve a resposta de texto.</summary>
    /// <param name="prompt">Mensagem do usuário — pode conter dado sigiloso.</param>
    /// <param name="systemPrompt">Instruções estáveis; é o trecho elegível a prompt caching.</param>
    /// <param name="options">Modelo, teto de tokens, esforço e cache desta chamada.</param>
    /// <param name="cancellationToken">Cancelamento da chamada.</param>
    Task<Result<AnthropicResponse>> SendMessageAsync(
        string prompt, string systemPrompt, AnthropicOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia uma conversa oferecendo ferramentas ao modelo. Se o modelo pedir uma ferramenta, a
    /// resposta traz <see cref="AnthropicResponse.ToolUses"/> e o chamador decide executar e
    /// responder. Usa os padrões de <c>Anthropic:*</c>.
    /// </summary>
    /// <param name="messages">Conversa até aqui, começando por uma mensagem "user".</param>
    /// <param name="tools">Ferramentas disponíveis.</param>
    /// <param name="cancellationToken">Cancelamento da chamada.</param>
    Task<Result<AnthropicResponse>> SendMessageWithToolsAsync(
        IReadOnlyList<AnthropicMessage> messages,
        IReadOnlyList<AnthropicTool> tools,
        CancellationToken cancellationToken = default);
}
