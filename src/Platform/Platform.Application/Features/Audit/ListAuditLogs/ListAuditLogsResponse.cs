namespace Advocacia.Platform.Application.Features.Audit.ListAuditLogs;

public sealed record AuditLogListItem(
    Guid Id,
    Guid UserId,
    string Action,
    string EntityType,
    string EntityId,
    DateTimeOffset CreatedAt);

public sealed record ListAuditLogsResponse(
    IReadOnlyList<AuditLogListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
