using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.HealthChecks;

/// <summary>
/// Verifica reachability da API pública de comunicações do CNJ (ver ADR-036).
///
/// Registrado com a tag "cnj" e NÃO com "ready": o CNJ é notoriamente instável e a captura
/// de publicações é um job assíncrono e idempotente — o Iustitia continua perfeitamente
/// utilizável com o DJEN fora do ar. Marcar a aplicação como "not ready" nesse caso faria o
/// Azure Container Apps retirar a instância de rotação por uma dependência de terceiro que
/// não afeta nenhuma requisição de usuário.
///
/// Uma falha aqui vira <see cref="HealthStatus.Degraded"/>, que é o que alimenta o alerta de
/// operação sem tirar a aplicação do ar.
/// </summary>
public sealed class CnjHealthCheck(ICnjClient cnjClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = await cnjClient.PingAsync(cancellationToken);

        return result.IsSuccess
            ? HealthCheckResult.Healthy("DJEN/CNJ acessível.")
            : HealthCheckResult.Degraded(
                $"DJEN/CNJ inacessível: {result.Error.Code}. A captura de publicações será retomada automaticamente.");
    }
}
