using System.Text.Json;
using System.Text.Json.Serialization;

namespace Advocacia.Modules.Integrations.Anthropic.Application.DTOs;

/// <summary>
/// Corpo da resposta de <c>POST /v1/messages</c>. <see cref="Content"/> é uma lista de blocos:
/// <c>text</c>, <c>thinking</c> e <c>tool_use</c> (o modelo pedindo uma ferramenta).
/// </summary>
public sealed record AnthropicResponseDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlockDto>? Content,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("stop_reason")] string? StopReason,
    [property: JsonPropertyName("usage")] AnthropicUsageDto? Usage);

/// <summary>Bloco de conteúdo. Campos por tipo: text → Text; tool_use → Id, Name, Input.</summary>
public sealed record AnthropicContentBlockDto(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("input")] JsonElement? Input);

public sealed record AnthropicUsageDto(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens,
    [property: JsonPropertyName("cache_creation_input_tokens")] int? CacheCreationInputTokens,
    [property: JsonPropertyName("cache_read_input_tokens")] int? CacheReadInputTokens);
