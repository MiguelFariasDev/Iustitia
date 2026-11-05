namespace Advocacia.Modules.Integrations.Anthropic.Domain.Models;

/// <summary>
/// Tokens faturáveis de uma chamada, separados pelas quatro tarifas da Anthropic. Os tokens de
/// entrada NÃO incluem os de cache: <see cref="CacheCreationTokens"/> e
/// <see cref="CacheReadTokens"/> são cobrados à parte (ver ADR-068).
/// </summary>
public sealed record AnthropicUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens)
{
    public static readonly AnthropicUsage None = new(0, 0, 0, 0);
}
