using Advocacia.BuildingBlocks.Infrastructure.Observability;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Primeiro middleware customizado do pipeline (ver Program.cs): lê X-Correlation-Id da
/// requisição (ou gera um novo) e o define em <see cref="LogEnrichmentContext"/>, para que
/// todo log da requisição (via CorrelationIdEnricher — ver ADR-037) e toda mensagem
/// publicada a partir dela (via OutboxInterceptor/CorrelationIdConsumeFilter) carreguem o
/// mesmo id. Roda antes até de ExceptionHandlingMiddleware, para que respostas de erro
/// também tragam o header de volta ao cliente.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            && Guid.TryParse(headerValue, out var parsed)
                ? parsed
                : Guid.NewGuid();

        LogEnrichmentContext.CorrelationId = correlationId;
        context.Response.Headers[HeaderName] = correlationId.ToString();

        await next(context);
    }
}
