namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>
/// Operações de manutenção de audit_logs, implementadas pelas funções SECURITY DEFINER de
/// 014_audit_immutability.sql. Existe como abstração para o job ser testável sem Postgres.
/// </summary>
public interface IAuditLogMaintenanceStore
{
    Task<IReadOnlyList<AuditArchiveBatch>> ListPendingArchiveAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogArchiveRow>> ReadMonthAsync(Guid tenantId, DateOnly month, CancellationToken cancellationToken);

    /// <summary>Registra no manifesto e remove do banco o mês arquivado — o banco recusa se a contagem não bater.</summary>
    Task<int> CommitArchiveAsync(
        Guid tenantId, DateOnly month, int rowCount, string sha256, string blobUri, CancellationToken cancellationToken);

    /// <summary>Remove o que passou de 5 anos. Retorna quantas linhas.</summary>
    Task<long> PurgeExpiredAsync(CancellationToken cancellationToken);
}
