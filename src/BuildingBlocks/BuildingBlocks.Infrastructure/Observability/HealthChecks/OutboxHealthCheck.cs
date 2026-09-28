using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability.HealthChecks;

/// <summary>
/// Degraded (não Unhealthy — um backlog na outbox não deve tirar a aplicação de rotação de
/// tráfego) quando a mensagem Pending mais antiga já está esperando há mais tempo do que
/// <see cref="HealthChecksOptions.OutboxPendingThresholdMinutes"/>, sinal de que o
/// OutboxProcessorJob parou de rodar ou está muito atrasado (ver ADR-034/017).
/// </summary>
public sealed class OutboxHealthCheck(EfDbContext dbContext, IOptions<HealthChecksOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var oldestPending = await dbContext.Set<OutboxMessage>()
            .Where(message => message.Status == OutboxStatus.Pending)
            .OrderBy(message => message.CreatedAt)
            .Select(message => (DateTimeOffset?)message.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (oldestPending is null)
        {
            return HealthCheckResult.Healthy("Nenhuma mensagem pendente na outbox.");
        }

        var age = DateTimeOffset.UtcNow - oldestPending.Value;
        var threshold = TimeSpan.FromMinutes(options.Value.OutboxPendingThresholdMinutes);

        return age > threshold
            ? HealthCheckResult.Degraded($"Mensagem pendente há {age.TotalMinutes:F0} minutos (limite: {threshold.TotalMinutes:F0}).")
            : HealthCheckResult.Healthy($"Mensagem pendente mais antiga: {age.TotalMinutes:F0} minutos.");
    }
}
