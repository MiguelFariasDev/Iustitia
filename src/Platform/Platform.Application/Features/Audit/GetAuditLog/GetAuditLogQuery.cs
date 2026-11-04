using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Audit.GetAuditLog;

public sealed record GetAuditLogQuery(Guid AuditLogId) : IQuery<GetAuditLogResponse>;
