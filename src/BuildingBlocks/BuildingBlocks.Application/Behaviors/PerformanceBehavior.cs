using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Mede a duração de todo Command/Query e loga um warning quando ultrapassa o limiar
/// configurável (appsettings, seção "Performance", ver <see cref="PerformanceBehaviorOptions"/>;
/// padrão 500ms). Não interrompe a execução — é só um sinal de observabilidade.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger, IOptions<PerformanceBehaviorOptions> options)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        var threshold = options.Value.ThresholdMilliseconds;
        if (stopwatch.ElapsedMilliseconds > threshold)
        {
            logger.LogWarning(
                "{RequestName} demorou {ElapsedMilliseconds}ms (limite: {ThresholdMilliseconds}ms)",
                typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, threshold);
        }

        return response;
    }
}
