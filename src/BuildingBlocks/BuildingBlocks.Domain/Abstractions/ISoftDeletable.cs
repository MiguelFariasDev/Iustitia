namespace Advocacia.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Marca entidades com exclusão lógica. O filtro global de soft delete é aplicado
/// no BaseDbContext (ver BuildingBlocks.Infrastructure), nunca manualmente em cada query.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }

    string? DeletedBy { get; set; }
}
