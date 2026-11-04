using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure;

/// <summary>
/// Cliente HTTP da Messages API (cliente customizado, como permite o claude.md, seção 2).
/// A resiliência é do pipeline registrado no HttpClient — ver
/// <see cref="DependencyInjection.AddAnthropicIntegration"/>.
///
/// NUNCA loga prompt nem resposta: ambos carregam o inteiro teor de publicações processuais
/// (sigilo profissional / LGPD). O log carrega só modelo, status HTTP, tokens e request-id.
/// O registro completo de auditoria (tokens, custo, latência) é do chamador, em ai_usage_logs.
/// </summary>
public sealed class AnthropicClient(
    HttpClient httpClient,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicClient> logger) : IAnthropicClient
{
    public const string HttpClientName = "Anthropic.Messages";

    private const string Provider = "Anthropic";
    private const string MessagesPath = "v1/messages";

    private readonly AnthropicOptions _options = options.Value;

    public async Task<Result<AnthropicResponse>> SendAsync(
        AnthropicRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogError("Anthropic: Anthropic:ApiKey não configurada — chamada recusada.");
            return Result.Failure<AnthropicResponse>(ErrorFactory.From(ErrorCode.INTEGRATION_AUTH_FAILED, Provider));
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, MessagesPath)
        {
            Content = JsonContent.Create(BuildBody(request), options: SerializerOptions),
        };
        message.Headers.Add("x-api-key", _options.ApiKey);
        message.Headers.Add("anthropic-version", _options.ApiVersion);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Anthropic: timeout na chamada ao modelo {Model}.", request.Model);
            return Result.Failure<AnthropicResponse>(ErrorFactory.From(ErrorCode.INTEGRATION_TIMEOUT, Provider));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Anthropic: falha de rede na chamada ao modelo {Model}.", request.Model);
            return Result.Failure<AnthropicResponse>(ErrorFactory.From(ErrorCode.INTEGRATION_UNAVAILABLE, Provider));
        }

        using (response)
        {
            var requestId = response.Headers.TryGetValues("request-id", out var values) ? values.FirstOrDefault() : null;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Anthropic: HTTP {StatusCode} do modelo {Model} (request-id {RequestId}).",
                    (int)response.StatusCode, request.Model, requestId);
                return Result.Failure<AnthropicResponse>(MapStatusCode(response.StatusCode));
            }

            MessagesResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<MessagesResponse>(SerializerOptions, cancellationToken);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Anthropic: corpo de resposta ilegível (request-id {RequestId}).", requestId);
                return Result.Failure<AnthropicResponse>(ErrorFactory.From(ErrorCode.INTEGRATION_INVALID_RESPONSE, Provider));
            }

            if (payload is null)
            {
                return Result.Failure<AnthropicResponse>(ErrorFactory.From(ErrorCode.INTEGRATION_INVALID_RESPONSE, Provider));
            }

            // Só blocos "text": com thinking ligado a resposta traz também blocos "thinking".
            var text = new StringBuilder();
            foreach (var block in payload.Content ?? [])
            {
                if (block.Type == "text" && block.Text is not null)
                {
                    text.Append(block.Text);
                }
            }

            var usage = payload.Usage is null
                ? AnthropicUsage.None
                : new AnthropicUsage(
                    payload.Usage.InputTokens,
                    payload.Usage.OutputTokens,
                    payload.Usage.CacheCreationInputTokens ?? 0,
                    payload.Usage.CacheReadInputTokens ?? 0);

            logger.LogInformation(
                "Anthropic: resposta do modelo {Model} — {InputTokens} tokens de entrada, {OutputTokens} de saída, stop={StopReason}, request-id {RequestId}.",
                payload.Model ?? request.Model, usage.InputTokens, usage.OutputTokens, payload.StopReason, requestId);

            return Result.Success(new AnthropicResponse(
                text.ToString(), payload.Model ?? request.Model, payload.StopReason, requestId, usage));
        }
    }

    private MessagesRequest BuildBody(AnthropicRequest request) => new()
    {
        Model = request.Model,
        MaxTokens = request.MaxTokens,
        System =
        [
            new SystemBlock
            {
                Text = request.SystemPrompt,
                CacheControl = request.CacheSystemPrompt ? new CacheControl() : null,
            },
        ],
        Messages = [new RequestMessage { Role = "user", Content = request.UserPrompt }],
        OutputConfig = string.IsNullOrWhiteSpace(_options.Effort) ? null : new OutputConfig { Effort = _options.Effort },
    };

    private static Error MapStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            ErrorFactory.From(ErrorCode.INTEGRATION_AUTH_FAILED, Provider),
        HttpStatusCode.TooManyRequests =>
            ErrorFactory.From(ErrorCode.INTEGRATION_RATE_LIMITED, Provider),
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
            ErrorFactory.From(ErrorCode.INTEGRATION_TIMEOUT, Provider),
        >= HttpStatusCode.InternalServerError or (HttpStatusCode)529 =>
            ErrorFactory.From(ErrorCode.INTEGRATION_UNAVAILABLE, Provider),
        _ => ErrorFactory.From(ErrorCode.INTEGRATION_INVALID_RESPONSE, Provider),
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class MessagesRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("system")]
        public List<SystemBlock> System { get; init; } = [];

        [JsonPropertyName("messages")]
        public List<RequestMessage> Messages { get; init; } = [];

        [JsonPropertyName("output_config")]
        public OutputConfig? OutputConfig { get; init; }
    }

    private sealed class SystemBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;

        [JsonPropertyName("cache_control")]
        public CacheControl? CacheControl { get; init; }
    }

    private sealed class CacheControl
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "ephemeral";
    }

    private sealed class RequestMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class OutputConfig
    {
        [JsonPropertyName("effort")]
        public string Effort { get; init; } = string.Empty;
    }

    private sealed class MessagesResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; init; }

        [JsonPropertyName("content")]
        public List<ResponseBlock>? Content { get; init; }

        [JsonPropertyName("usage")]
        public ResponseUsage? Usage { get; init; }
    }

    private sealed class ResponseBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class ResponseUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; init; }

        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; init; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; init; }
    }
}
