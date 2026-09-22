using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;

namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Acesso somente leitura para <see cref="Entity{TId}"/> que não são raízes de agregado
/// (ex.: AuditLog — registro imutável, nunca criado/alterado via repositório genérico de
/// escrita). Para agregados, use <see cref="IRepository{TEntity, TId}"/>.
/// </summary>
public interface IReadRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    Task<int> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);
}
