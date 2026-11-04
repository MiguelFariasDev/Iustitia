using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Auditing;

/// <summary>
/// Registro de auditoria imutável — retenção mínima de 5 anos (LGPD/OAB).
/// É uma Entity, não um AggregateRoot: nunca é editado após criado, apenas consultado.
/// </summary>
public sealed class AuditLog : Entity<AuditLogId>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public AuditAction Action { get; private set; }

    public string EntityType { get; private set; } = null!;

    public string EntityId { get; private set; } = null!;

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    public string? Ip { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AuditLog()
    {
        // EF Core.
    }

    private AuditLog(
        AuditLogId id,
        Guid tenantId,
        Guid userId,
        AuditAction action,
        string entityType,
        string entityId,
        string? beforeJson,
        string? afterJson,
        string? ip,
        string? userAgent,
        DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        Ip = ip;
        UserAgent = userAgent;
        CreatedAt = createdAt;
    }

    public static Result<AuditLog> Create(
        Guid tenantId,
        Guid userId,
        AuditAction action,
        string entityType,
        string entityId,
        string? beforeJson,
        string? afterJson,
        string? ip,
        string? userAgent,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Result.Failure<AuditLog>(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            return Result.Failure<AuditLog>(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        return Result.Success(new AuditLog(
            AuditLogId.New(), tenantId, userId, action, entityType, entityId, beforeJson, afterJson, ip, userAgent, createdAt));
    }
}
