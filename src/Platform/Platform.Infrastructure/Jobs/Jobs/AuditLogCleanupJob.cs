using Advocacia.Platform.Domain.Auditing;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.Platform.Infrastructure.Jobs.Jobs;

/// <summary>
/// Remove audit_logs fora do prazo de retenção legal (5 anos — LGPD/OAB, ver ADR-032/docs de
/// compliance). NUNCA remove registros dentro do prazo: o corte é fixo em 5 anos, não
/// configurável via appsettings, para que uma configuração incorreta não viole a exigência
/// legal. IgnoreQueryFilters é necessário porque AuditLog implementa IHasTenant e este job
/// precisa varrer todos os tenants.
/// </summary>
public sealed class AuditLogCleanupJob(EfDbContext dbContext, ILogger<AuditLogCleanupJob> logger)
{
    private const int RetentionYears = 5;

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddYears(-RetentionYears);

        var expiredLogs = await dbContext.Set<AuditLog>()
            .IgnoreQueryFilters()
            .Where(log => log.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expiredLogs.Count == 0)
        {
            return;
        }

        dbContext.Set<AuditLog>().RemoveRange(expiredLogs);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("AuditLogCleanupJob: {RemovedCount} audit logs removidos (retenção de {RetentionYears} anos expirada).", expiredLogs.Count, RetentionYears);
    }
}
