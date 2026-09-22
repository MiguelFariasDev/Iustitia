namespace Advocacia.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Marca entidades que devem ter criação/atualização carimbadas automaticamente
/// por um interceptor de auditoria no SaveChanges (ver BuildingBlocks.Infrastructure).
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    string? CreatedBy { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    string? UpdatedBy { get; set; }
}
