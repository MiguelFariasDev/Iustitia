using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Errors;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using static Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper.JuscraperFalha;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;

/// <summary>
/// Chamadas HTTP ao juscraper. Recebe um <see cref="HttpClient"/> já configurado (endereço,
/// X-Api-Key e resiliência) por <see cref="JuscraperServiceCollectionExtensions"/>: um cliente
/// para a consulta do cadastro (60 s) e outro para o acompanhamento (3 min).
///
/// NUNCA loga o corpo de uma resposta de sucesso (partes e andamentos de processo real). Nos
/// erros, loga só tribunal, status HTTP e código do juscraper.
/// </summary>
internal sealed class JuscraperClient(HttpClient httpClient, ILogger logger)
{
    public const string ConsultaHttpClient = "juscraper-consulta";

    public const string AcompanhamentoHttpClient = "juscraper-acompanhamento";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>GET /v1/processos/{numero}. <paramref name="digitos"/> são os 20 dígitos do CNJ.</summary>
    public Task<JuscraperResposta<JuscraperResultado>> ConsultarAsync(
        string digitos, int? maxMovimentacoes, string tribunal, CancellationToken cancellationToken)
    {
        var caminho = maxMovimentacoes is { } max
            ? $"v1/processos/{digitos}?max_movimentacoes={max}"
            : $"v1/processos/{digitos}";

        return EnviarAsync<JuscraperResultado>(caminho, tribunal, cancellationToken);
    }

    /// <summary>
    /// GET /v1/processos/{numero}/movimentacoes. Sem <paramref name="desde"/>, o histórico inteiro.
    /// NUNCA usar max_movimentacoes aqui: o dia mais antigo poderia vir cortado e o id deixaria de
    /// ser estável.
    /// </summary>
    public Task<JuscraperResposta<JuscraperMovimentacoes>> MovimentacoesAsync(
        string digitos, DateOnly? desde, string tribunal, CancellationToken cancellationToken)
    {
        var caminho = desde is { } data
            ? $"v1/processos/{digitos}/movimentacoes?desde={data:yyyy-MM-dd}"
            : $"v1/processos/{digitos}/movimentacoes";

        return EnviarAsync<JuscraperMovimentacoes>(caminho, tribunal, cancellationToken);
    }

    private async Task<JuscraperResposta<T>> EnviarAsync<T>(string caminho, string tribunal, CancellationToken cancellationToken)
        where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(caminho, cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            // Circuito aberto: falha imediata, sem esperar timeout — quem chama vai direto ao fallback.
            logger.LogWarning("juscraper: circuito aberto — consulta ao {Tribunal} não enviada.", tribunal);
            return JuscraperResposta<T>.Falhou(new JuscraperFalha(Motivos.CircuitoAberto, ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE)));
        }
        catch (Exception exception) when (
            exception is TaskCanceledException or TimeoutRejectedException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("juscraper: consulta ao {Tribunal} estourou o tempo.", tribunal);
            return JuscraperResposta<T>.Falhou(new JuscraperFalha(Motivos.Timeout, ErrorFactory.From(ErrorCode.CNJ_TIMEOUT)));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("juscraper: serviço inacessível para o {Tribunal} ({Erro}).", tribunal, exception.HttpRequestError);
            return JuscraperResposta<T>.Falhou(new JuscraperFalha(Motivos.Rede, ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE)));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return JuscraperResposta<T>.Falhou(await TraduzirErroAsync(response, tribunal, cancellationToken));
            }

            T? valor;
            try
            {
                valor = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                logger.LogWarning("juscraper: resposta ilegível para o {Tribunal}.", tribunal);
                valor = null;
            }

            return valor is null
                ? JuscraperResposta<T>.Falhou(new JuscraperFalha(Motivos.RespostaIlegivel, ErrorFactory.From(ErrorCode.CNJ_INVALID_RESPONSE)))
                : JuscraperResposta<T>.Ok(valor);
        }
    }

    private async Task<JuscraperFalha> TraduzirErroAsync(
        HttpResponseMessage response, string tribunal, CancellationToken cancellationToken)
    {
        JuscraperProblema? problema = null;
        try
        {
            problema = await response.Content.ReadFromJsonAsync<JuscraperProblema>(Json, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Sem ProblemDetails (proxy, gateway): cai no mapeamento por status abaixo.
        }

        var status = (int)response.StatusCode;
        var falha = problema?.Codigo switch
        {
            Codigos.NumeroCnjInvalido => new JuscraperFalha(Codigos.NumeroCnjInvalido, ErrorFactory.From(ErrorCode.CNJ_INVALID_REQUEST)),
            Codigos.TribunalNaoSuportado or Codigos.CaptchaExigido or Codigos.AcessoBloqueado
                => new JuscraperFalha(problema!.Codigo!, ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, tribunal)),
            Codigos.TribunalIndisponivel => new JuscraperFalha(Codigos.TribunalIndisponivel, ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE)),
            Codigos.RespostaInesperada => new JuscraperFalha(Codigos.RespostaInesperada, ErrorFactory.From(ErrorCode.CNJ_INVALID_RESPONSE)),
            Codigos.NaoAutorizado => new JuscraperFalha(Codigos.NaoAutorizado, ErrorFactory.From(ErrorCode.CNJ_AUTH_FAILED)),
            _ => response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    => new JuscraperFalha(Codigos.NaoAutorizado, ErrorFactory.From(ErrorCode.CNJ_AUTH_FAILED)),
                HttpStatusCode.TooManyRequests
                    => new JuscraperFalha(Motivos.LimiteExcedido, ErrorFactory.From(ErrorCode.CNJ_RATE_LIMITED)),
                HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout
                    => new JuscraperFalha(Motivos.Timeout, ErrorFactory.From(ErrorCode.CNJ_TIMEOUT)),
                _ => new JuscraperFalha(Motivos.ErroDoServidor, ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE)),
            },
        };

        // Chave errada ou ausente é erro de CONFIGURAÇÃO: precisa aparecer no alerta, não sumir
        // entre avisos de portal fora do ar.
        if (falha.NaoAutorizado)
        {
            logger.LogError("juscraper: X-Api-Key recusada (HTTP {Status}) — confira Juscraper:ApiKey.", status);
        }
        else
        {
            logger.LogWarning("juscraper: {Tribunal} respondeu HTTP {Status} {Codigo}.", tribunal, status, falha.Motivo);
        }

        return falha;
    }
}
