using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Gpu;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;

public sealed class GpuInstanceLogRepository(DraftingDbContext dbContext) : IGpuInstanceLogRepository
{
    public void Add(GpuInstanceLog log) => dbContext.GpuInstanceLogs.Add(log);

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
