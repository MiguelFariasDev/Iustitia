namespace Advocacia.Modules.Integrations.Anthropic.Domain.Models;

/// <summary>
/// Resposta da Messages API. <see cref="Content"/> é a concatenação dos blocos de texto (blocos
/// <c>thinking</c> não entram) e pode conter dado do cliente: nunca logar. Quando
/// <see cref="StopReason"/> é "tool_use", <see cref="ToolUses"/> traz o que o modelo pediu.
/// </summary>
/// <param name="Id">Id da mensagem na Anthropic (msg_...).</param>
/// <param name="Model">Modelo que de fato respondeu.</param>
/// <param name="Content">Texto da resposta.</param>
/// <param name="Usage">Tokens consumidos.</param>
/// <param name="StopReason">end_turn, max_tokens, tool_use, refusal...</param>
/// <param name="ToolUses">Ferramentas pedidas pelo modelo; vazio quando não houve.</param>
/// <param name="RequestId">Cabeçalho request-id, para suporte junto à Anthropic.</param>
public sealed record AnthropicResponse(
    string Id,
    string Model,
    string Content,
    AnthropicUsage Usage,
    string? StopReason,
    IReadOnlyList<AnthropicToolUse> ToolUses,
    string? RequestId = null)
{
    public bool RequestsToolUse => ToolUses.Count > 0;
}
