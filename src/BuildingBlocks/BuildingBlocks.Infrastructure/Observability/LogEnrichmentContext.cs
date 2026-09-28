namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Estado ambiente (AsyncLocal) usado pelos enrichers de log para CorrelationId/UserId —
/// funciona tanto em requisições HTTP quanto em consumers de mensageria e jobs, sem
/// depender de HttpContext (mesmo princípio de <see cref="Tenancy.AsyncLocalTenantContext"/>,
/// já usado para TenantId). Escrito por CorrelationIdMiddleware/CorrelationIdConsumeFilter
/// (CorrelationId) e por TenantContextMiddleware (UserId).
/// </summary>
public static class LogEnrichmentContext
{
    private static readonly AsyncLocal<Guid?> CorrelationIdValue = new();
    private static readonly AsyncLocal<Guid?> UserIdValue = new();

    public static Guid? CorrelationId
    {
        get => CorrelationIdValue.Value;
        set => CorrelationIdValue.Value = value;
    }

    public static Guid? UserId
    {
        get => UserIdValue.Value;
        set => UserIdValue.Value = value;
    }
}
