using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Domain.Auditing;
using MapsterMapper;

namespace Advocacia.Platform.Application.Features.Audit.ListAuditLogs;

public sealed class ListAuditLogsHandler(IReadRepository<AuditLog, AuditLogId> auditLogRepository, IMapper mapper)
    : IQueryHandler<ListAuditLogsQuery, ListAuditLogsResponse>
{
    public async Task<Result<ListAuditLogsResponse>> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var specification = new ListAuditLogsSpecification(request.EntityType, request.UserId, page, pageSize);

        var items = await auditLogRepository.ListAsync(specification, cancellationToken);
        var totalCount = await auditLogRepository.CountAsync(specification, cancellationToken);

        var responseItems = items.Select(mapper.Map<AuditLogListItem>).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new ListAuditLogsResponse(responseItems, page, pageSize, totalCount, totalPages);
    }

    private sealed class ListAuditLogsSpecification : Specification<AuditLog>
    {
        public ListAuditLogsSpecification(string? entityType, Guid? userId, int page, int pageSize)
            : base(a =>
                (entityType == null || a.EntityType == entityType) &&
                (userId == null || a.UserId == userId))
        {
            ApplyOrderByDescending(a => a.CreatedAt);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }
}
