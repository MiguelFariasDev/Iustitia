using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Ollama;

/// <summary>
/// Cliente do Ollama: <c>POST /api/chat</c> em streaming NDJSON (geração) e <c>POST /api/embed</c>
/// (BGE-M3). Implementa as portas <see cref="ILlmService"/> e <see cref="IEmbeddingService"/>, então
/// trocar de modelo ou de provedor não toca em mais nada.
///
/// Erros viram <see cref="LlmServiceException"/> (rede, HTTP, linha inválida) ou
/// <see cref="TimeoutException"/> (prazo da geração estourou). Cancelamento do chamador continua
/// sendo <see cref="OperationCanceledException"/>. Nenhuma mensagem de erro carrega prompt ou texto.
/// </summary>
public sealed class OllamaService(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaService> logger)
    : ILlmService, IEmbeddingService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly OllamaOptions _options = options.Value;

    public string ModelName => _options.Modelo;

    public async IAsyncEnumerable<LlmChunk> ChatStreamAsync(
        LlmChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSegundos));

        var body = new ChatBody(
            _options.Modelo,
            request.Messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList(),
            Stream: true,
            // Modelos de raciocínio (qwen3): pensar gasta minutos de GPU e não vira texto de peça.
            Think: false,
            new ChatOptions(_options.Temperature, _options.NumCtx, _options.NumPredict),
            _options.KeepAlive);

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "api/chat") { Content = JsonContent.Create(body, options: Json) },
            HttpCompletionOption.ResponseHeadersRead,
            deadline,
            cancellationToken);

        await using var stream = await ReadAsync(
            () => response.Content.ReadAsStreamAsync(deadline.Token), deadline, cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var filter = new ThinkTagFilter();

        while (true)
        {
            var line = await ReadAsync(() => reader.ReadLineAsync(deadline.Token).AsTask(), deadline, cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var parsed = ParseChatLine(line);
            var text = filter.Push(parsed.Content);
            if (parsed.Done)
            {
                text += filter.Flush();
            }

            if (text.Length > 0 || parsed.Done)
            {
                yield return new LlmChunk(text, parsed.Done, parsed.PromptTokens, parsed.OutputTokens);
            }

            if (parsed.Done)
            {
                yield break;
            }
        }

        // O Ollama fechou a conexão sem a linha "done": a minuta pode estar truncada.
        throw new LlmServiceException("O Ollama encerrou o stream sem concluir a resposta.");
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Min(_options.TimeoutSegundos, 120)));

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "api/embed")
            {
                Content = JsonContent.Create(new EmbedBody(_options.ModeloEmbedding, inputs), options: Json),
            },
            HttpCompletionOption.ResponseContentRead,
            deadline,
            cancellationToken);

        var payload = await ReadAsync(
            () => response.Content.ReadFromJsonAsync<EmbedResult>(Json, deadline.Token), deadline, cancellationToken);

        if (payload?.Embeddings is not { } embeddings || embeddings.Count != inputs.Count)
        {
            throw new LlmServiceException("O Ollama devolveu embeddings em formato inesperado.");
        }

        return embeddings;
    }

    /// <summary>Interpreta UMA linha NDJSON do /api/chat. Pública para o teste de parsing.</summary>
    public static ChatLine ParseChatLine(string line)
    {
        ChatLineDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ChatLineDto>(line, Json);
        }
        catch (JsonException exception)
        {
            throw new LlmServiceException("Linha inválida no stream do Ollama.", exception);
        }

        if (dto is null)
        {
            throw new LlmServiceException("Linha vazia no stream do Ollama.");
        }

        if (!string.IsNullOrEmpty(dto.Error))
        {
            throw new LlmServiceException($"O Ollama reportou erro durante a geração: {Truncate(dto.Error, 200)}");
        }

        return new ChatLine(dto.Message?.Content ?? string.Empty, dto.Done, dto.PromptEvalCount, dto.EvalCount);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> build,
        HttpCompletionOption completion,
        CancellationTokenSource deadline,
        CancellationToken caller)
    {
        using var message = build();
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            message.Headers.Authorization = new("Bearer", _options.ApiKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, completion, deadline.Token);
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        {
            throw new TimeoutException("O Ollama não respondeu dentro do prazo.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Ollama: falha de rede ({Tipo}).", exception.GetType().Name);
            throw new LlmServiceException("Não foi possível conectar ao Ollama.", exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            logger.LogWarning("Ollama: resposta HTTP {Status}.", status);
            throw new LlmServiceException($"O Ollama respondeu HTTP {status}.");
        }

        return response;
    }

    private static async Task<T> ReadAsync<T>(Func<Task<T>> read, CancellationTokenSource deadline, CancellationToken caller)
    {
        try
        {
            return await read();
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("O Ollama parou de responder dentro do prazo.");
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            throw new LlmServiceException("A conexão com o Ollama foi interrompida.", exception);
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    public sealed record ChatLine(string Content, bool Done, int? PromptTokens, int? OutputTokens);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatOptions(double Temperature, [property: JsonPropertyName("num_ctx")] int NumCtx, [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record ChatBody(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        bool Stream,
        bool Think,
        ChatOptions Options,
        [property: JsonPropertyName("keep_alive")] string KeepAlive);

    private sealed record EmbedBody(string Model, IReadOnlyList<string> Input);

    private sealed record EmbedResult(List<float[]>? Embeddings);

    private sealed class ChatLineDto
    {
        public ChatMessage? Message { get; set; }

        public bool Done { get; set; }

        public string? Error { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }
}
