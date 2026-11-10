using Microsoft.EntityFrameworkCore;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>
/// Chama as funções SECURITY DEFINER de 014_audit_immutability.sql. Tudo parametrizado
/// (<c>SqlQuery</c> com string interpolada gera parâmetros, não concatenação). Não usa o DbSet de
/// AuditLog: o RLS esconderia as linhas de outros tenants de app_user, e as funções existem
/// justamente para atravessar isso de forma controlada.
/// </summary>
public sealed class AuditLogMaintenanceStore(EfDbContext dbContext) : IAuditLogMaintenanceStore
{
    public async Task<IReadOnlyList<AuditArchiveBatch>> ListPendingArchiveAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<PendingRow>($"""
                SELECT tenant_id AS "TenantId", period_month AS "PeriodMonth", row_count AS "RowCount"
                FROM audit_archive_candidates({limit})
                """)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new AuditArchiveBatch(row.TenantId, row.PeriodMonth, row.RowCount)).ToList();
    }

    public async Task<IReadOnlyList<AuditLogArchiveRow>> ReadMonthAsync(
        Guid tenantId, DateOnly month, CancellationToken cancellationToken) =>
        await dbContext.Database
            .SqlQuery<AuditLogArchiveRow>($"""
                SELECT id AS "Id", tenant_id AS "TenantId", user_id AS "UserId", action AS "Action",
                       entity_type AS "EntityType", entity_id AS "EntityId",
                       before::text AS "Before", after::text AS "After",
                       ip AS "Ip", user_agent AS "UserAgent", created_at AS "CreatedAt"
                FROM audit_archive_read({tenantId}, {month})
                """)
            .ToListAsync(cancellationToken);

    public async Task<int> CommitArchiveAsync(
        Guid tenantId, DateOnly month, int rowCount, string sha256, string blobUri, CancellationToken cancellationToken)
    {
        var result = await dbContext.Database
            .SqlQuery<int>($"SELECT audit_archive_commit({tenantId}, {month}, {rowCount}, {sha256}, {blobUri}) AS \"Value\"")
            .ToListAsync(cancellationToken);

        return result.Single();
    }

    public async Task<long> PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        var result = await dbContext.Database
            .SqlQuery<long>($"SELECT audit_purge_expired() AS \"Value\"")
            .ToListAsync(cancellationToken);

        return result.Single();
    }

    private sealed record PendingRow(Guid TenantId, DateOnly PeriodMonth, long RowCount);
}
