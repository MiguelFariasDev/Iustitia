using System.Text.Json;
using System.Text.Json.Serialization;
using Advocacia.Modules.Drafting.Application.Models;
using Microsoft.AspNetCore.Http;

namespace Advocacia.Modules.Drafting.Api;

/// <summary>
/// Escreve os eventos como NDJSON: UMA linha de JSON válido por evento (campo "tipo" identifica o evento),
/// descarregada (flush) a cada evento para o texto aparecer conforme é gerado.
///
/// Os cabeçalhos saem ANTES do primeiro evento — por isso acesso e estado são verificados antes
/// (AbrirStreamAsync) e viram HTTP 4xx normal. Depois de aberto o stream, uma falha só pode ser um
/// evento {"tipo":"erro"} (o status já é 200). Cliente que desconecta só encerra o ACOMPANHAMENTO:
/// a geração continua no worker (cancelar a geração é POST /pecas/{id}/cancelar).
/// </summary>
public sealed class NdjsonStreamResult(IAsyncEnumerable<GenerationEvent> events) : IResult
{
    public const string ContentType = "application/x-ndjson; charset=utf-8";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = ContentType;
        response.Headers.CacheControl = "no-store";
        // Proxies (nginx, Front Door) não podem acumular o stream antes de entregar.
        response.Headers["X-Accel-Buffering"] = "no";

        var cancellationToken = httpContext.RequestAborted;

        try
        {
            await foreach (var evento in events.WithCancellation(cancellationToken))
            {
                await WriteAsync(response, evento, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cliente foi embora: a geração já foi cancelada e registrada. Nada a escrever.
        }
    }

    internal static async Task WriteAsync(HttpResponse response, GenerationEvent evento, CancellationToken cancellationToken)
    {
        // GetType(): o serializador precisa do tipo concreto para incluir os campos do subtipo.
        var line = JsonSerializer.SerializeToUtf8Bytes(evento, evento.GetType(), Json);
        await response.Body.WriteAsync(line, cancellationToken);
        await response.Body.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
