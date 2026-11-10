using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Usage;

public sealed class AnthropicUsageQuery(AnthropicDbContext dbContext) : IAnthropicUsageQuery
{
    public async Task<IReadOnlyDictionary<Guid, int>> CountFailuresByReferenceAsync(
        Guid tenantId,
        string purpose,
        string errorCode,
        IReadOnlyCollection<Guid> referenceIds,
        CancellationToken cancellationToken = default)
    {
        if (referenceIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = referenceIds.Select(id => (Guid?)id).ToList();

        var counts = await dbContext.AiUsageLogs
            .Where(log => log.TenantId == tenantId
                && log.Purpose == purpose
                && log.ErrorCode == errorCode
                && ids.Contains(log.ReferenceId))
            .GroupBy(log => log.ReferenceId)
            .Select(group => new { ReferenceId = group.Key, Failures = group.Count() })
            .ToListAsync(cancellationToken);

        return counts
            .Where(row => row.ReferenceId.HasValue)
            .ToDictionary(row => row.ReferenceId!.Value, row => row.Failures);
    }
}
