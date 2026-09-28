using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability.HealthChecks;

/// <summary>
/// TODO (Fase 1 — integração com o DataJud/CNJ): verificar reachability/autenticação da
/// API pública do CNJ. Por ora, sempre Healthy e não registrado em nenhum grupo — existe só
/// para o módulo Integrations.CNJ ter uma estrutura pronta para implementar.
/// </summary>
public sealed class CnjHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("Integração com o CNJ ainda não implementada (Fase 1)."));
}
