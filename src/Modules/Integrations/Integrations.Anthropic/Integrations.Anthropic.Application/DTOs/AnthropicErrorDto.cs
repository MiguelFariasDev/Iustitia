using System.Text.Json.Serialization;

namespace Advocacia.Modules.Integrations.Anthropic.Application.DTOs;

/// <summary>
/// Corpo de erro da Anthropic: <c>{"type":"error","error":{"type":"...","message":"..."}}</c>.
/// <see cref="ErrorType"/> (ex.: invalid_request_error, rate_limit_error) é seguro para log;
/// <see cref="ErrorMessage"/> NÃO é: a Anthropic pode repetir trecho do pedido na mensagem.
/// </summary>
public sealed record AnthropicErrorDto(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("error")] AnthropicErrorDetailDto? Error)
{
    [JsonIgnore]
    public string? ErrorType => Error?.Type;

    [JsonIgnore]
    public string? ErrorMessage => Error?.Message;
}

public sealed record AnthropicErrorDetailDto(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("message")] string? Message);
