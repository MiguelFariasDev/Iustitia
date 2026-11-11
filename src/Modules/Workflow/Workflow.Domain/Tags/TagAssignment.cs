using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Workflow.Domain.Tags;

/// <summary>
/// Liga uma tag a um processo ou advogado. Não tem soft delete: tirar a tag é apagar a linha,
/// o que mantém a unicidade (tag, alvo) simples. O alvo é referenciado por id, sem FK.
/// </summary>
public sealed class TagAssignment : Entity<Guid>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public TagId TagId { get; private set; }

    public TagTarget Target { get; private set; }

    public Guid TargetId { get; private set; }

    private TagAssignment()
    {
        // EF Core.
    }

    public TagAssignment(Guid tenantId, TagId tagId, TagTarget target, Guid targetId)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        TagId = tagId;
        Target = target;
        TargetId = targetId;
    }
}
