using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.Resilience;

/// <summary>
/// Orçamento de taxa do CNJ, compartilhado pelo PROCESSO inteiro (registrar como singleton).
/// Fica separado do <see cref="CnjRateLimitingHandler"/> porque o HttpClientFactory exige que
/// cada <see cref="DelegatingHandler"/> seja uma instância nova por pipeline (ele define o
/// <c>InnerHandler</c>); o estado que precisa ser global mora aqui.
/// </summary>
public sealed class CnjRateLimiter : IDisposable
{
    public CnjRateLimiter(IOptions<CnjOptions> options)
    {
        var cnjOptions = options.Value;
        QueueTimeout = TimeSpan.FromSeconds(cnjOptions.RateLimitQueueTimeoutSeconds);

        Limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
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

    public SlidingWindowRateLimiter Limiter { get; }

    public TimeSpan QueueTimeout { get; }

    public void Dispose() => Limiter.Dispose();
}
