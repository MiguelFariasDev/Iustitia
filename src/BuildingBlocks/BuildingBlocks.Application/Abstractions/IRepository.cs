using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;

namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Repositório genérico sobre uma raiz de agregado. Módulos podem estender com
/// repositórios especializados quando precisarem de consultas próprias do domínio
/// (ex.: IProcessRepository), mas a persistência básica é sempre via esta interface.
/// </summary>
public interface IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    Task<int> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    void Add(TEntity entity);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}
