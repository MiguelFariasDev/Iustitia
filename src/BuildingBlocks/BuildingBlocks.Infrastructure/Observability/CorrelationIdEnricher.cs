using Serilog.Core;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Adiciona a propriedade "CorrelationId" a todo log event, quando definida no contexto
/// ambiente (ver <see cref="LogEnrichmentContext"/>) — escrita por CorrelationIdMiddleware
/// (requisições HTTP) ou CorrelationIdConsumeFilter (mensageria). Não gera um novo
/// CorrelationId por log event: isso cabe a quem inicia o fluxo (middleware/filter), não ao
/// enricher — gerar aqui produziria um id diferente por linha de log dentro do mesmo fluxo.
/// </summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (LogEnrichmentContext.CorrelationId is { } correlationId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }
    }
}
