namespace Advocacia.Platform.Application.Features.Audit.GetAuditLog;

public sealed record GetAuditLogResponse(
    Guid Id,
    Guid UserId,
    string Action,
    string EntityType,
    string EntityId,
    string? BeforeJson,
    string? AfterJson,
    string? Ip,
    string? UserAgent,
    DateTimeOffset CreatedAt);
