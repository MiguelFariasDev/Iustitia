using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;
using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Transcricao;

/// <summary>Cliente de um servidor Whisper próprio. Só https (ou loopback), como Ollama e Qdrant.</summary>
public sealed class HttpTranscricaoService(
    HttpClient http, IOptions<TranscricaoOptions> options, ILogger<HttpTranscricaoService> logger) : ITranscricaoService
{
    public bool Disponivel => !string.IsNullOrWhiteSpace(options.Value.BaseUrl);

    public async Task<Result<ResultadoTranscricao>> TranscreverAsync(
        byte[] audio, string nomeArquivo, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        if (!Disponivel)
        {
            return Result.Failure<ResultadoTranscricao>(
                ErrorFactory.From(ErrorCode.DRAFTING_CONVERSION_UNAVAILABLE, "áudio (transcrição não configurada)"));
        }

        if (TransportGuard.Validate("Drafting:Transcricao", o.BaseUrl, o.ApiKey, o.AllowInsecureHttp) is { } problema)
        {
            throw new InvalidOperationException(problema);
        }

        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSegundos));

        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(audio);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        // O nome real pode conter dado pessoal: vai só a extensão.
        form.Add(arquivo, "file", "audio" + Path.GetExtension(nomeArquivo));
        form.Add(new StringContent(o.Modelo), "model");
        form.Add(new StringContent(o.Idioma), "language");
        form.Add(new StringContent("verbose_json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.BaseUrl.TrimEnd('/') + "/"), o.Caminho)) { Content = form };
        if (!string.IsNullOrWhiteSpace(o.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        }

        try
        {
            using var resposta = await http.SendAsync(request, prazo.Token);
            if ((int)resposta.StatusCode is >= 400 and < 500)
            {
                logger.LogWarning("Transcrição: HTTP {Status}.", (int)resposta.StatusCode);

                return Result.Failure<ResultadoTranscricao>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
            }

            resposta.EnsureSuccessStatusCode();
            var corpo = await resposta.Content.ReadFromJsonAsync<Resposta>(prazo.Token);

            return corpo is null || (string.IsNullOrWhiteSpace(corpo.Text) && corpo.Segments is not { Count: > 0 })
                ? Result.Failure<ResultadoTranscricao>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE))
                : Result.Success(Converter(corpo));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("A transcrição não terminou no prazo.");
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException("O servidor de transcrição devolveu uma resposta inválida.", ex);
        }
    }

    private static ResultadoTranscricao Converter(Resposta corpo)
    {
        var segmentos = corpo.Segments is { Count: > 0 }
            ? corpo.Segments
                .Select(s => new SegmentoTranscricao(
                    TimeSpan.FromSeconds(s.Start), TimeSpan.FromSeconds(s.End), (s.Text ?? string.Empty).Trim(),
                    // avg_logprob é a média do log da probabilidade dos tokens: exp devolve a escala 0–1.
                    s.AvgLogprob is { } log ? Math.Clamp(Math.Exp(log), 0, 1) : null))
                .Where(s => s.Texto.Length > 0)
                .ToList()
            : [new SegmentoTranscricao(TimeSpan.Zero, TimeSpan.Zero, corpo.Text!.Trim(), null)];
        var medidos = segmentos.Where(s => s.Confianca is not null).ToList();

        return new ResultadoTranscricao(segmentos, medidos.Count == 0 ? null : medidos.Average(s => s.Confianca!.Value));
    }

    private sealed record Resposta(string? Text, List<Segmento>? Segments);

    private sealed record Segmento(double Start, double End, string? Text, [property: JsonPropertyName("avg_logprob")] double? AvgLogprob);
}
