using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Domain.Auditing;
using MapsterMapper;

namespace Advocacia.Platform.Application.Features.Audit.GetAuditLog;

public sealed class GetAuditLogHandler(IReadRepository<AuditLog, AuditLogId> auditLogRepository, IMapper mapper)
    : IQueryHandler<GetAuditLogQuery, GetAuditLogResponse>
{
    public async Task<Result<GetAuditLogResponse>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var auditLog = await auditLogRepository.GetByIdAsync(AuditLogId.From(request.AuditLogId), cancellationToken);
        if (auditLog is null)
        {
            return Result.Failure<GetAuditLogResponse>(ErrorFactory.From(ErrorCode.AUDIT_LOG_NOT_FOUND));
        }

        return mapper.Map<GetAuditLogResponse>(auditLog);
    }
}
