using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Tags;

namespace Advocacia.Modules.Workflow.Application.Abstractions;

public interface ITagRepository : IRepository<Tag, TagId>
{
    Task<IReadOnlyList<Tag>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tag do escritório com o mesmo nome, sem diferenciar maiúsculas.</summary>
    Task<Tag?> FindByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagAssignment>> ListAssignmentsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TagAssignment?> FindAssignmentAsync(
        TagId tagId, TagTarget target, Guid targetId, CancellationToken cancellationToken = default);

    void AddAssignment(TagAssignment assignment);

    void RemoveAssignment(TagAssignment assignment);

    /// <summary>Marca para remoção todas as ligações da tag (ao apagá-la).</summary>
    Task RemoveAssignmentsOfTagAsync(TagId tagId, CancellationToken cancellationToken = default);
}
