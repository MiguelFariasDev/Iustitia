using System.Diagnostics;
using Advocacia.BuildingBlocks.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Advocacia.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Behavior mais externo do pipeline (ver ADR-030): loga início e fim (com duração) de
/// todo Command/Query, com user_id/tenant_id (via <see cref="ICurrentUser"/>) e
/// correlation_id como propriedades estruturadas — nunca como texto livre, e nunca
/// incluindo os dados da própria request (que podem conter senha, token, etc.). O
/// correlation_id vem de <see cref="Activity.Current"/> (W3C trace context, já propagado
/// pelo ASP.NET Core por requisição) em vez de <c>HttpContext</c>, para não introduzir uma
/// dependência de Application em Microsoft.AspNetCore.Http.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger, ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestName"] = requestName,
            ["UserId"] = currentUser.UserId,
            ["TenantId"] = currentUser.TenantId,
            ["CorrelationId"] = correlationId,
        });

        logger.LogInformation("Iniciando {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();
            logger.LogInformation(
                "Concluído {RequestName} em {ElapsedMilliseconds}ms", requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(
                exception, "Falha em {RequestName} após {ElapsedMilliseconds}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
