using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Audit.ListAuditLogs;

public sealed record ListAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? EntityType = null,
    Guid? UserId = null) : IQuery<ListAuditLogsResponse>;
