namespace Advocacia.Modules.Integrations.Anthropic.Application.Models;

/// <summary>
/// Pedido de uma resposta de texto à Messages API. Deliberadamente sem temperatura/top_p:
/// os modelos atuais rejeitam valores não padrão desses parâmetros, e a determinismo
/// do JSON de saída vem do prompt e da validação, não de amostragem.
/// </summary>
/// <param name="Model">ID exato do modelo (ex.: claude-sonnet-5-5), vindo de Anthropic:Models.</param>
/// <param name="SystemPrompt">Instruções estáveis — é o trecho elegível a prompt caching.</param>
/// <param name="UserPrompt">Conteúdo variável da chamada (contém dado do cliente: nunca logar).</param>
/// <param name="MaxTokens">Teto de tokens de saída.</param>
/// <param name="CacheSystemPrompt">Marca o system prompt com cache_control (ADR-024).</param>
public sealed record AnthropicRequest(
    string Model,
    string SystemPrompt,
    string UserPrompt,
    int MaxTokens,
    bool CacheSystemPrompt = true);
