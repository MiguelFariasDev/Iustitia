using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Serilog.Core;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Adiciona "TenantId" a todo log event que tenha um tenant no contexto ambiente.
/// <see cref="AsyncLocalTenantContext"/> guarda o valor num <c>AsyncLocal</c> estático —
/// instanciar aqui um objeto novo lê o mesmo estado ambiente que ITenantContext (via DI)
/// usa em qualquer outro lugar da requisição/job atual, sem precisar resolver o serviço
/// scoped a partir do provider raiz usado para configurar o Serilog.
/// </summary>
public sealed class TenantIdEnricher : ILogEventEnricher
{
    private static readonly AsyncLocalTenantContext TenantContext = new();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (TenantContext.HasTenant)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", TenantContext.TenantId));
        }
    }
}
