namespace Advocacia.Modules.Integrations.Anthropic.Application.Models;

/// <summary>
/// Opções de UMA chamada. Tudo é opcional: o que ficar nulo cai no padrão de
/// <c>Anthropic:*</c> no appsettings (modelo padrão, esforço, teto de tokens).
///
/// Não confundir com a configuração do cliente (AnthropicSettings, na infraestrutura): esta
/// classe é o que o chamador decide por requisição.
/// </summary>
public sealed record AnthropicOptions
{
    /// <summary>ID exato do modelo (ex.: claude-sonnet-5-5). Nulo = <c>Anthropic:DefaultModel</c>.</summary>
    public string? Model { get; init; }

    /// <summary>Teto de tokens de saída. Nulo = <c>Anthropic:DefaultMaxTokens</c>.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Temperatura. Nula (o normal) omite o campo. Modelos atuais rejeitam valores não padrão
    /// — só informe com um modelo que aceite.
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>Esforço de raciocínio (low, medium, high, xhigh, max). Nulo = <c>Anthropic:Effort</c>.</summary>
    public string? Effort { get; init; }

    /// <summary>
    /// Marca o system prompt com <c>cache_control: ephemeral</c> (prompt caching — ADR-068). Só
    /// vale a pena para prompts estáveis e repetidos; o cache exige um tamanho mínimo de prefixo.
    /// </summary>
    public bool CacheSystemPrompt { get; init; } = true;
}
