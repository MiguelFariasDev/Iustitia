namespace Advocacia.Modules.Integrations.Anthropic.Application.Models;

/// <summary>Resposta de texto da Messages API, já com o consumo de tokens para auditoria.</summary>
/// <param name="Text">Concatenação dos blocos de texto. Pode conter dado do cliente: nunca logar.</param>
/// <param name="Model">Modelo que de fato respondeu (o campo "model" da resposta).</param>
/// <param name="StopReason">end_turn, max_tokens, refusal...</param>
/// <param name="RequestId">Cabeçalho request-id, para suporte junto à Anthropic.</param>
public sealed record AnthropicResponse(
    string Text,
    string Model,
    string? StopReason,
    string? RequestId,
    AnthropicUsage Usage);

/// <summary>Tokens faturáveis de uma chamada, separados pelas quatro tarifas da Anthropic.</summary>
public sealed record AnthropicUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens,
    int CacheReadInputTokens)
{
    public static readonly AnthropicUsage None = new(0, 0, 0, 0);
}
