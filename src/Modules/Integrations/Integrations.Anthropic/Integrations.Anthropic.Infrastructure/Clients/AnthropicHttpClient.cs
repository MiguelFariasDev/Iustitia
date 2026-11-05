using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.DTOs;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Clients;

/// <summary>
/// Cliente HTTP tipado da Messages API (<c>POST v1/messages</c>). A resiliência (timeout, retry
/// com backoff exponencial e jitter, circuit breaker) é do pipeline registrado no HttpClient —
/// ver <see cref="DependencyInjection.AddAnthropicIntegration"/> —, não deste código.
///
/// Os erros da Anthropic são traduzidos para a faixa AI do catálogo (ver MapStatusCode).
///
/// NUNCA loga, põe em span, métrica ou mensagem de erro o texto do prompt, da resposta ou o
/// campo "message" do erro da Anthropic: tudo isso pode conter dado processual sigiloso
/// (ADR-069). Só modelo, status HTTP, tipo do erro, tokens e request-id.
/// </summary>
public sealed class AnthropicHttpClient(
    HttpClient httpClient,
    IOptions<AnthropicSettings> settings,
    IAiCostCalculator costCalculator,
    AnthropicMetrics metrics,
    ILogger<AnthropicHttpClient> logger) : IAnthropicClient
{
    public const string HttpClientName = "Anthropic.Messages";

    private const string MessagesPath = "messages";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AnthropicSettings _settings = settings.Value;

    public Task<Result<AnthropicResponse>> SendMessageAsync(
        string prompt, string systemPrompt, AnthropicOptions options, CancellationToken cancellationToken = default)
    {
        var system = string.IsNullOrWhiteSpace(systemPrompt)
            ? null
            : new List<AnthropicSystemBlockDto>
            {
                new(systemPrompt, options.CacheSystemPrompt ? new AnthropicCacheControlDto() : null),
            };

        var request = new AnthropicRequestDto(
            options.Model ?? _settings.DefaultModel,
            options.MaxTokens ?? _settings.DefaultMaxTokens,
            [new AnthropicMessageDto(AnthropicMessage.UserRole, prompt)],
            system,
            options.Temperature,
            OutputConfig: BuildOutputConfig(options.Effort));

        return ExecuteAsync(request, cancellationToken);
    }

    public Task<Result<AnthropicResponse>> SendMessageWithToolsAsync(
        IReadOnlyList<AnthropicMessage> messages,
        IReadOnlyList<AnthropicTool> tools,
        CancellationToken cancellationToken = default)
    {
        List<AnthropicToolDto> toolDtos;
        try
        {
            toolDtos = tools
                .Select(tool => new AnthropicToolDto(tool.Name, tool.Description, ParseSchema(tool.InputSchema)))
                .ToList();
        }
        catch (JsonException)
        {
            // Schema da ferramenta inválido é erro de programação de quem a definiu.
            logger.LogError("Anthropic: o InputSchema de uma ferramenta não é um JSON válido.");
            return Task.FromResult(Result.Failure<AnthropicResponse>(ErrorFactory.From(ErrorCode.AI_INVALID_REQUEST)));
        }

        var request = new AnthropicRequestDto(
            _settings.DefaultModel,
            _settings.DefaultMaxTokens,
            messages.Select(message => new AnthropicMessageDto(message.Role, message.Content)).ToList(),
            Tools: toolDtos,
            ToolChoice: toolDtos.Count > 0 ? new AnthropicToolChoiceDto() : null,
            OutputConfig: BuildOutputConfig(null));

        return ExecuteAsync(request, cancellationToken);
    }

    private async Task<Result<AnthropicResponse>> ExecuteAsync(AnthropicRequestDto request, CancellationToken cancellationToken)
    {
        using var activity = AnthropicActivitySource.Source.StartActivity(AnthropicActivitySource.SendMessageSpanName);
        activity?.SetTag("anthropic.model", request.Model);
        activity?.SetTag("anthropic.tools.count", request.Tools?.Count ?? 0);
        activity?.SetTag("anthropic.cache_control", request.System?.Any(block => block.CacheControl is not null) == true);

        var stopwatch = Stopwatch.StartNew();
        var outcome = await SendAsync(request, cancellationToken);
        stopwatch.Stop();

        var usage = outcome.IsSuccess ? outcome.Value.Usage : AnthropicUsage.None;
        var model = outcome.IsSuccess ? outcome.Value.Model : request.Model;
        var cost = outcome.IsSuccess ? costCalculator.Calculate(model, usage) : 0m;
        var errorCode = outcome.IsFailure ? outcome.Error.Code : null;

        metrics.Record(
            model,
            outcome.IsSuccess,
            errorCode,
            usage.InputTokens + usage.CacheCreationTokens + usage.CacheReadTokens,
            usage.OutputTokens,
            (double)cost,
            stopwatch.Elapsed.TotalMilliseconds);

        activity?.SetTag("anthropic.tokens.input", usage.InputTokens);
        activity?.SetTag("anthropic.tokens.output", usage.OutputTokens);
        activity?.SetTag("anthropic.tokens.cache_creation", usage.CacheCreationTokens);
        activity?.SetTag("anthropic.tokens.cache_read", usage.CacheReadTokens);
        activity?.SetTag("anthropic.cost.usd", (double)cost);

        if (outcome.IsFailure)
        {
            activity?.SetTag("anthropic.error_code", errorCode);
            activity?.SetStatus(ActivityStatusCode.Error, errorCode);
        }

        return outcome;
    }

    private async Task<Result<AnthropicResponse>> SendAsync(AnthropicRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            logger.LogError("Anthropic: Anthropic:ApiKey não configurada — chamada recusada.");
            return Fail(ErrorCode.AI_UNAUTHORIZED);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, MessagesPath)
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };
        message.Headers.Add("x-api-key", _settings.ApiKey);
        message.Headers.Add("anthropic-version", _settings.ApiVersion);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (
            !cancellationToken.IsCancellationRequested
            && exception is TaskCanceledException or TimeoutRejectedException or TimeoutException)
        {
            logger.LogWarning("Anthropic: timeout na chamada ao modelo {Model}.", request.Model);
            return Fail(ErrorCode.AI_TIMEOUT);
        }
        catch (BrokenCircuitException)
        {
            logger.LogWarning("Anthropic: circuit breaker aberto — chamada ao modelo {Model} não enviada.", request.Model);
            return Fail(ErrorCode.AI_UNAVAILABLE);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Anthropic: falha de rede na chamada ao modelo {Model}.", request.Model);
            return Fail(ErrorCode.AI_UNAVAILABLE);
        }

        using (response)
        {
            var requestId = response.Headers.TryGetValues("request-id", out var values) ? values.FirstOrDefault() : null;

            if (!response.IsSuccessStatusCode)
            {
                var errorType = await ReadErrorTypeAsync(response, cancellationToken);
                logger.LogWarning(
                    "Anthropic: HTTP {StatusCode} ({ErrorType}) do modelo {Model} (request-id {RequestId}).",
                    (int)response.StatusCode, errorType, request.Model, requestId);

                return Result.Failure<AnthropicResponse>(MapStatusCode(response.StatusCode));
            }

            AnthropicResponseDto? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<AnthropicResponseDto>(SerializerOptions, cancellationToken);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Anthropic: corpo de resposta ilegível (request-id {RequestId}).", requestId);
                return Fail(ErrorCode.AI_PARSE_ERROR);
            }

            if (payload is null)
            {
                return Fail(ErrorCode.AI_PARSE_ERROR);
            }

            var mapped = Map(payload, request.Model, requestId);

            logger.LogInformation(
                "Anthropic: resposta do modelo {Model} — {InputTokens} tokens de entrada, {OutputTokens} de saída, stop={StopReason}, request-id {RequestId}.",
                mapped.Model, mapped.Usage.InputTokens, mapped.Usage.OutputTokens, mapped.StopReason, requestId);

            return Result.Success(mapped);
        }
    }

    private static AnthropicResponse Map(AnthropicResponseDto payload, string requestedModel, string? requestId)
    {
        // Só blocos "text" viram Content: com thinking ligado a resposta traz também blocos "thinking".
        var text = new StringBuilder();
        var toolUses = new List<AnthropicToolUse>();

        foreach (var block in payload.Content ?? [])
        {
            if (block.Type == "text" && block.Text is not null)
            {
                text.Append(block.Text);
            }
            else if (block.Type == "tool_use" && block.Id is not null && block.Name is not null)
            {
                toolUses.Add(new AnthropicToolUse(block.Id, block.Name, block.Input?.GetRawText() ?? "{}"));
            }
        }

        var usage = payload.Usage is null
            ? AnthropicUsage.None
            : new AnthropicUsage(
                payload.Usage.InputTokens,
                payload.Usage.OutputTokens,
                payload.Usage.CacheCreationInputTokens ?? 0,
                payload.Usage.CacheReadInputTokens ?? 0);

        return new AnthropicResponse(
            payload.Id ?? string.Empty,
            payload.Model ?? requestedModel,
            text.ToString(),
            usage,
            payload.StopReason,
            toolUses,
            requestId);
    }

    /// <summary>Lê só o "type" do erro (seguro para log); nunca a "message".</summary>
    private static async Task<string?> ReadErrorTypeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<AnthropicErrorDto>(SerializerOptions, cancellationToken);
            return error?.ErrorType;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Error MapStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ErrorFactory.From(ErrorCode.AI_UNAUTHORIZED),
        HttpStatusCode.TooManyRequests => ErrorFactory.From(ErrorCode.AI_RATE_LIMITED),
        (HttpStatusCode)529 => ErrorFactory.From(ErrorCode.AI_OVERLOADED),
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ErrorFactory.From(ErrorCode.AI_TIMEOUT),
        >= HttpStatusCode.InternalServerError => ErrorFactory.From(ErrorCode.AI_UNAVAILABLE),
        _ => ErrorFactory.From(ErrorCode.AI_INVALID_REQUEST),
    };

    private AnthropicOutputConfigDto? BuildOutputConfig(string? effort)
    {
        var resolved = string.IsNullOrWhiteSpace(effort) ? _settings.Effort : effort;

        return string.IsNullOrWhiteSpace(resolved) ? null : new AnthropicOutputConfigDto(resolved);
    }

    private static JsonElement ParseSchema(string schema)
    {
        using var document = JsonDocument.Parse(schema);

        return document.RootElement.Clone();
    }

    private static Result<AnthropicResponse> Fail(ErrorCode code) =>
        Result.Failure<AnthropicResponse>(ErrorFactory.From(code));
}
