namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Propaga o CorrelationId da requisição/job atual (ver <see cref="LogEnrichmentContext"/>)
/// para chamadas HTTP de saída (CNJ, Anthropic, ...) via o header X-Correlation-Id —
/// registrado como handler padrão de todo HttpClient (ver
/// <c>ConfigureHttpClientDefaults</c> em Platform.Infrastructure), não precisa ser
/// adicionado manualmente a cada client tipado.
/// </summary>
public sealed class CorrelationIdPropagationHandler : DelegatingHandler
{
    public const string HeaderName = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (LogEnrichmentContext.CorrelationId is { } correlationId && !request.Headers.Contains(HeaderName))
        {
            request.Headers.Add(HeaderName, correlationId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
