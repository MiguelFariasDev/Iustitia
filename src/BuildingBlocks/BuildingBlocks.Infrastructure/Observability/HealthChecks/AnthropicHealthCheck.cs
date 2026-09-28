using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability.HealthChecks;

/// <summary>
/// TODO (Fase 3 — integração com a API da Anthropic): verificar reachability/autenticação
/// do provedor de IA. Por ora, sempre Healthy e não registrado em nenhum grupo — existe só
/// para o módulo Integrations.Anthropic ter uma estrutura pronta para implementar.
/// </summary>
public sealed class AnthropicHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("Integração com Anthropic ainda não implementada (Fase 3)."));
}
