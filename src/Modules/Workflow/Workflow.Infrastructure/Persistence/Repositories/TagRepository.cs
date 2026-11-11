using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Tags;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;

public sealed class TagRepository(WorkflowDbContext dbContext)
    : Repository<Tag, TagId>(dbContext), ITagRepository
{
    public async Task<IReadOnlyList<Tag>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await DbSet.Where(tag => tag.TenantId == tenantId).OrderBy(tag => tag.Name).ToListAsync(cancellationToken);

    public Task<Tag?> FindByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        var lowered = name.Trim().ToLower();

        return DbSet.FirstOrDefaultAsync(
            tag => tag.TenantId == tenantId && tag.Name.ToLower() == lowered, cancellationToken);
    }

    public async Task<IReadOnlyList<TagAssignment>> ListAssignmentsAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.TagAssignments.Where(a => a.TenantId == tenantId).ToListAsync(cancellationToken);

    public Task<TagAssignment?> FindAssignmentAsync(
        TagId tagId, TagTarget target, Guid targetId, CancellationToken cancellationToken = default) =>
        dbContext.TagAssignments.FirstOrDefaultAsync(
            a => a.TagId == tagId && a.Target == target && a.TargetId == targetId, cancellationToken);

    public void AddAssignment(TagAssignment assignment) => dbContext.TagAssignments.Add(assignment);

    public void RemoveAssignment(TagAssignment assignment) => dbContext.TagAssignments.Remove(assignment);

    public async Task RemoveAssignmentsOfTagAsync(TagId tagId, CancellationToken cancellationToken = default)
    {
        var assignments = await dbContext.TagAssignments.Where(a => a.TagId == tagId).ToListAsync(cancellationToken);
        dbContext.TagAssignments.RemoveRange(assignments);
    }
}
