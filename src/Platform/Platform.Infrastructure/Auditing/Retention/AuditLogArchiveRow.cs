namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>Uma linha de audit_logs como é gravada no arquivo frio (uma por linha do NDJSON).</summary>
public sealed record AuditLogArchiveRow(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Action,
    string EntityType,
    string EntityId,
    string? Before,
    string? After,
    string? Ip,
    string? UserAgent,
    DateTimeOffset CreatedAt);

/// <summary>Um (tenant, mês) pronto para arquivar.</summary>
public sealed record AuditArchiveBatch(Guid TenantId, DateOnly PeriodMonth, long RowCount);
