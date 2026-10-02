using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Pipeline de resiliência do cliente do DataJud (Polly 8). Mesma estrutura do DJEN
/// (<see cref="Resilience.CnjResiliencePipeline"/>), com um limiar de circuito mais baixo:
/// aqui quem espera é uma pessoa preenchendo um formulário, não um job noturno, e insistir
/// numa API caída só faz o cadastro travar. Abrir cedo devolve o controle para o
/// preenchimento manual mais rápido.
/// </summary>
public static class DataJudResiliencePipeline
{
    public static IHttpStandardResiliencePipelineBuilder Configure(
        IHttpStandardResiliencePipelineBuilder builder, DataJudOptions options)
    {
        builder.Configure(resilience =>
        {
            resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds * (options.RetryCount + 1));
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            resilience.Retry.MaxRetryAttempts = options.RetryCount;
            resilience.Retry.Delay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds);
            resilience.Retry.BackoffType = DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = true;
            resilience.Retry.ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));

            resilience.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds);
            resilience.CircuitBreaker.MinimumThroughput = options.CircuitBreakerMinimumThroughput;
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds);
            resilience.CircuitBreaker.ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));

            // AttemptTimeout precisa caber na janela de amostragem do breaker, senão o Polly
            // rejeita a configuração na inicialização.
            if (resilience.CircuitBreaker.SamplingDuration < resilience.AttemptTimeout.Timeout * 2)
            {
                resilience.CircuitBreaker.SamplingDuration = resilience.AttemptTimeout.Timeout * 2;
            }
        });

        return builder;
    }

    // 4xx que não sejam 408/429 são erro nosso: repetir gasta a quota pública sem chance de
    // dar certo. 404 aqui é "índice inexistente", não "processo não achado" — o DataJud
    // responde 200 com hits vazios quando o processo não existe.
    private static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is HttpRequestException or TimeoutRejectedException
        || outcome.Result is { } response && IsTransient(response.StatusCode);

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}
