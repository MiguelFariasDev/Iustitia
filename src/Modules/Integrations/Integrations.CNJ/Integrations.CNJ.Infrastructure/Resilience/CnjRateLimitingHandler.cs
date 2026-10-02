using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.Resilience;

/// <summary>
/// Throttling do lado do cliente para respeitar o rate limit do DJEN sem depender de tomar
/// 429 primeiro.
///
/// Por que existe (medido na Etapa 1.3 — ver docs/poc/cnj-poc-report.md): o DJEN aceita
/// 20 requisições e devolve 429 na 21ª, recuperando em ~5s. O retry do Polly TRATA o 429,
/// mas tratar é pior que evitar: cada 429 consome uma das 3 tentativas de retry daquela
/// requisição, então uma varredura longa passa a falhar de verdade por ter gasto o
/// orçamento de retry com rate limit em vez de com instabilidade real.
///
/// Fica ANTES do handler de resiliência no pipeline, para que o retry só veja falhas que
/// não sejam auto-infligidas.
///
/// Limitação conhecida: o limitador é por PROCESSO. Com mais de uma instância do Worker,
/// cada uma teria seu próprio orçamento e a soma estouraria o limite do CNJ — nesse cenário
/// o limite precisa virar distribuído (Redis) ou a captura precisa rodar em instância
/// única. Ver ADR-059.
/// </summary>
public sealed class CnjRateLimitingHandler : DelegatingHandler
{
    private readonly SlidingWindowRateLimiter _rateLimiter;
    private readonly TimeSpan _queueTimeout;
    private readonly ILogger<CnjRateLimitingHandler> _logger;

    public CnjRateLimitingHandler(IOptions<CnjOptions> options, ILogger<CnjRateLimitingHandler> logger)
    {
        var cnjOptions = options.Value;
        _logger = logger;
        _queueTimeout = TimeSpan.FromSeconds(cnjOptions.RateLimitQueueTimeoutSeconds);

        _rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = cnjOptions.RateLimitPermitsPerWindow,
            Window = TimeSpan.FromSeconds(cnjOptions.RateLimitWindowSeconds),

            // Janela dividida em segmentos: o orçamento é liberado gradualmente em vez de
            // de uma vez no virar da janela, o que evitaria uma rajada sincronizada logo
            // após cada reset — exatamente o padrão que dispara o 429 do outro lado.
            SegmentsPerWindow = cnjOptions.RateLimitWindowSeconds,

            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,

            // Fila grande: numa varredura de 200 páginas queremos que as requisições
            // ESPEREM, não que sejam rejeitadas.
            QueueLimit = int.MaxValue,
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_queueTimeout);

        RateLimitLease lease;
        try
        {
            lease = await _rateLimiter.AcquireAsync(permitCount: 1, timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Esperou o timeout inteiro na fila — sinal de que a captura está pedindo muito
            // mais vazão do que o CNJ concede. Devolver 429 aqui faz o pipeline de
            // resiliência tratar como transiente, que é exatamente o que é.
            _logger.LogWarning(
                "CNJ: requisição descartada após aguardar {TimeoutSeconds}s no limitador de taxa local.",
                _queueTimeout.TotalSeconds);

            return new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
        }

        using (lease)
        {
            return await base.SendAsync(request, cancellationToken);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rateLimiter.Dispose();
        }

        base.Dispose(disposing);
    }
}
