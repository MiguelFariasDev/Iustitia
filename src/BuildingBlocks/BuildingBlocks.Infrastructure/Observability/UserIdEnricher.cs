using Serilog.Core;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Adiciona "UserId" a todo log event, quando houver um usuário no contexto ambiente (ver
/// <see cref="LogEnrichmentContext"/>, escrito por TenantContextMiddleware) — jobs em
/// background normalmente não têm usuário associado, então esta propriedade fica ausente.
/// </summary>
public sealed class UserIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (LogEnrichmentContext.UserId is { } userId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
        }
    }
}
