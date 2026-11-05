using Advocacia.Platform.Infrastructure.Auditing.Retention;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.Jobs.Jobs;

/// <summary>
/// Retenção de audit_logs (Provimento CFOAB/LGPD: mínimo de 5 anos). Duas etapas:
///   1. ARQUIVA: para cada (tenant, mês completo) com mais de 1 ano, grava um arquivo NDJSON.gz no
///      storage frio e só então confirma no banco, que remove as linhas do mês na mesma transação
///      — e recusa se a contagem não bater com o arquivo. Sem storage configurado, esta etapa
///      não faz nada (e nada é apagado).
///   2. EXPURGA: remove o que passou de 5 anos.
///
/// O que NUNCA depende desta classe: o prazo e a regra "só apaga o que está arquivado". Estão
/// no próprio banco (trigger de 014_audit_immutability.sql); um bug aqui não consegue apagar
/// auditoria dentro do prazo. Também não usa o DbSet de AuditLog: app_user não tem DELETE e o RLS
/// esconderia as linhas de outros tenants — tudo passa pelas funções de manutenção.
/// </summary>
public sealed class AuditLogCleanupJob(
    IAuditLogMaintenanceStore store,
    IAuditArchiveStore archiveStore,
    IOptions<AuditArchiveOptions> options,
    ILogger<AuditLogCleanupJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var archived = await ArchiveAsync(cancellationToken);
        var purged = await store.PurgeExpiredAsync(cancellationToken);

        logger.LogInformation(
            "AuditLogCleanupJob: {ArchivedMonths} meses arquivados, {PurgedRows} linhas expiradas (>5 anos) removidas.",
            archived, purged);
    }

    private async Task<int> ArchiveAsync(CancellationToken cancellationToken)
    {
        if (!archiveStore.IsConfigured)
        {
            logger.LogWarning("Arquivamento de auditoria desligado (AuditArchive:AccountUrl ausente): nada será arquivado nem removido por ele.");
            return 0;
        }

        var batches = await store.ListPendingArchiveAsync(options.Value.BatchLimit, cancellationToken);
        var archived = 0;
        var failures = 0;

        foreach (var batch in batches)
        {
            try
            {
                var rows = await store.ReadMonthAsync(batch.TenantId, batch.PeriodMonth, cancellationToken);
                if (rows.Count == 0)
                {
                    continue;
                }

                var (content, sha256) = AuditArchiveFormatter.Serialize(rows);
                var uri = await archiveStore.WriteAsync(
                    AuditArchiveFormatter.BlobName(batch.TenantId, batch.PeriodMonth), content, sha256, cancellationToken);

                // O banco só apaga se a contagem bater; antes disso o arquivo já está gravado.
                await store.CommitArchiveAsync(batch.TenantId, batch.PeriodMonth, rows.Count, sha256, uri, cancellationToken);
                archived++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Um mês com problema não trava os demais; fica pendente e é tentado na próxima execução.
                failures++;
                logger.LogError(ex, "Falha ao arquivar auditoria do tenant {TenantId}, mês {Month}.", batch.TenantId, batch.PeriodMonth);
            }
        }

        if (failures > 0)
        {
            throw new InvalidOperationException($"{failures} mês(es) de auditoria não puderam ser arquivados; veja os logs.");
        }

        return archived;
    }
}
