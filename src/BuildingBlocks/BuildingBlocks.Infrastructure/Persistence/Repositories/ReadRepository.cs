using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Microsoft.EntityFrameworkCore;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;

/// <summary>Implementação de <see cref="IReadRepository{TEntity, TId}"/> sobre EF Core.</summary>
public sealed class ReadRepository<TEntity, TId>(EfDbContext dbContext) : IReadRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    private readonly DbSet<TEntity> _dbSet = dbContext.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(entity => entity.Id.Equals(id), cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        SpecificationEvaluator.Apply(_dbSet.AsQueryable(), specification).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        await SpecificationEvaluator.Apply(_dbSet.AsQueryable(), specification).ToListAsync(cancellationToken);

    public Task<int> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        return query.CountAsync(cancellationToken);
    }
}
