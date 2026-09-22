using Microsoft.EntityFrameworkCore;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;

/// <summary>
/// Abstração de <see cref="BaseDbContext"/> para permitir dublês de teste em
/// componentes (como <c>UnitOfWork</c>) sem acoplar a um DbContext concreto.
/// </summary>
public interface IBaseDbContext
{
    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
