using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.Platform.Infrastructure.Jobs.Jobs;

/// <summary>
/// Verifica a saúde das dependências externas (Postgres, Redis) e o tamanho da fila da
/// outbox, alertando via log estruturado quando algo está fora do esperado — a métrica
/// "outbox.pending.count" e os alertas correspondentes no Application Insights (ver
/// docs/architecture/jobs.md) são configurados sobre esses logs/métricas customizadas.
/// </summary>
public sealed class HealthCheckJob(
    EfDbContext dbContext,
    ICacheService cacheService,
    IOutboxProcessor outboxProcessor,
    ILogger<HealthCheckJob> logger)
{
    private const int OutboxPendingAlertThreshold = 1000;
    private const string RedisPingKey = "health-check:ping";

    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var databaseHealthy = await CheckDatabaseAsync(cancellationToken);
        var redisHealthy = await CheckRedisAsync(cancellationToken);
        var pendingCount = await outboxProcessor.GetPendingCountAsync(cancellationToken);

        logger.LogInformation(
            "HealthCheckJob: database={DatabaseHealthy}, redis={RedisHealthy}, outbox.pending.count={PendingCount}.",
            databaseHealthy, redisHealthy, pendingCount);

        if (!databaseHealthy)
        {
            logger.LogCritical("HealthCheckJob: falha ao conectar ao PostgreSQL.");
        }

        if (!redisHealthy)
        {
            logger.LogCritical("HealthCheckJob: falha ao conectar ao Redis.");
        }

        if (pendingCount > OutboxPendingAlertThreshold)
        {
            logger.LogWarning(
                "HealthCheckJob: outbox com {PendingCount} mensagens pendentes (limite de alerta: {Threshold}).",
                pendingCount, OutboxPendingAlertThreshold);
        }
    }

    private async Task<bool> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HealthCheckJob: exceção ao verificar conexão com o PostgreSQL.");
            return false;
        }
    }

    private async Task<bool> CheckRedisAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cacheService.SetAsync(RedisPingKey, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HealthCheckJob: exceção ao verificar conexão com o Redis.");
            return false;
        }
    }
}
