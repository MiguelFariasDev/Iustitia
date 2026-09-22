using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Microsoft.EntityFrameworkCore;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação genérica de <see cref="IRepository{TEntity, TId}"/> sobre EF Core.
/// GetByIdAsync usa uma query (não DbSet.Find) de propósito: Find ignora os filtros
/// globais (soft delete e tenant) configurados no BaseDbContext, o que romperia o
/// isolamento multi-tenant.
/// </summary>
public class Repository<TEntity, TId>(EfDbContext dbContext) : IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
{
    protected DbSet<TEntity> DbSet { get; } = dbContext.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(entity => entity.Id.Equals(id), cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        SpecificationEvaluator.Apply(DbSet.AsQueryable(), specification).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        await SpecificationEvaluator.Apply(DbSet.AsQueryable(), specification).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await DbSet.ToListAsync(cancellationToken);

    public Task<int> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        return query.CountAsync(cancellationToken);
    }

    public Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        return query.AnyAsync(cancellationToken);
    }

    public void Add(TEntity entity) => DbSet.Add(entity);

    public void Update(TEntity entity) => DbSet.Update(entity);

    public void Remove(TEntity entity) => DbSet.Remove(entity);
}
