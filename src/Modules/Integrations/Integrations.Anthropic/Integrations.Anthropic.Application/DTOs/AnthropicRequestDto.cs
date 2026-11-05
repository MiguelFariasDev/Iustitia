using System.Text.Json;
using System.Text.Json.Serialization;

namespace Advocacia.Modules.Integrations.Anthropic.Application.DTOs;

/// <summary>
/// Corpo de <c>POST /v1/messages</c>. É o contrato de fio: nomes em snake_case, campos nulos
/// omitidos na serialização.
///
/// <see cref="System"/> é uma LISTA de blocos (e não string) justamente para poder levar
/// <c>cache_control</c> — prompt caching (ADR-068). <see cref="Tools"/> e
/// <see cref="ToolChoice"/> habilitam tool use.
/// </summary>
public sealed record AnthropicRequestDto(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessageDto> Messages,
    [property: JsonPropertyName("system")] IReadOnlyList<AnthropicSystemBlockDto>? System = null,
    [property: JsonPropertyName("temperature")] double? Temperature = null,
    [property: JsonPropertyName("tools")] IReadOnlyList<AnthropicToolDto>? Tools = null,
    [property: JsonPropertyName("tool_choice")] AnthropicToolChoiceDto? ToolChoice = null,
    [property: JsonPropertyName("output_config")] AnthropicOutputConfigDto? OutputConfig = null);

public sealed record AnthropicMessageDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

/// <summary>Bloco do system prompt. Com <see cref="CacheControl"/> preenchido, o prefixo até aqui é cacheado.</summary>
public sealed record AnthropicSystemBlockDto(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("cache_control")] AnthropicCacheControlDto? CacheControl = null)
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";
}

/// <summary><c>{ "type": "ephemeral" }</c> — cache de curta duração (ADR-068).</summary>
public sealed record AnthropicCacheControlDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "ephemeral";
}

public sealed record AnthropicToolDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("input_schema")] JsonElement InputSchema);

/// <summary>
/// Como o modelo escolhe ferramentas: "auto" (padrão — o modelo decide), "any" ou "tool".
/// Atenção: modelos atuais rejeitam "any"/"tool" (forçar ferramenta); prefira "auto".
/// </summary>
public sealed record AnthropicToolChoiceDto(
    [property: JsonPropertyName("type")] string Type = "auto",
    [property: JsonPropertyName("name")] string? Name = null);

public sealed record AnthropicOutputConfigDto(
    [property: JsonPropertyName("effort")] string Effort);
