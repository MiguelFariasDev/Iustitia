using Advocacia.BuildingBlocks.Infrastructure.Resilience;
using Microsoft.Extensions.Http.Resilience;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Pipeline de resiliência do cliente do DataJud: o padrão compartilhado (<see cref="StandardHttpResilience"/>) com
/// um limiar de circuito mais baixo nas opções — aqui quem espera é uma pessoa preenchendo um formulário, não um
/// job noturno, e insistir numa API caída só faz o cadastro travar.
///
/// 429 fica de fora da REPETIÇÃO (continua contando para o circuito): repetir um "limite excedido" na hora só
/// gasta a cota e o tempo de quem espera. O cliente pausa as consultas por um tempo (DataJudClient.PausaCacheKey).
/// 404 aqui é "índice inexistente", não "processo não achado": o DataJud responde 200 com hits vazios.
/// </summary>
public static class DataJudResiliencePipeline
{
    public static IHttpStandardResiliencePipelineBuilder Configure(
        IHttpStandardResiliencePipelineBuilder builder, DataJudOptions options) =>
        StandardHttpResilience.Configure(builder, new StandardHttpResilienceProfile(
            options.TimeoutSeconds,
            options.RetryCount,
            options.RetryBaseDelaySeconds,
            options.CircuitBreakerFailureRatio,
            options.CircuitBreakerSamplingDurationSeconds,
            options.CircuitBreakerMinimumThroughput,
            options.CircuitBreakerBreakDurationSeconds,
            RetryOnTooManyRequests: false));
}
