namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>
/// Destino do arquivamento de audit_logs com mais de 1 ano (seção "AuditArchive"). Em produção:
/// <c>AccountUrl</c> da conta de Blob e acesso por Managed Identity (sem chave). Sem
/// <c>AccountUrl</c> o arquivamento fica DESLIGADO e nenhuma linha é removida do banco por ele —
/// só a retenção de 5 anos (imposta pelo SQL) continua valendo. Ver ADR-069.
/// </summary>
public sealed class AuditArchiveOptions
{
    public const string SectionName = "AuditArchive";

    /// <summary>Ex.: https://stadvocaciaprod.blob.core.windows.net</summary>
    public string AccountUrl { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "audit-archive";

    /// <summary>Quantos (tenant, mês) arquivar por execução — limita o tempo do job.</summary>
    public int BatchLimit { get; set; } = 50;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccountUrl);
}
