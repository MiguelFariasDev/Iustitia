using Advocacia.BuildingBlocks.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;

/// <summary>Aplica um <see cref="ISpecification{T}"/> a um <see cref="IQueryable{T}"/> do EF Core.</summary>
public static class SpecificationEvaluator
{
    public static IQueryable<TEntity> Apply<TEntity>(IQueryable<TEntity> query, ISpecification<TEntity> specification)
        where TEntity : class
    {
        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (specification.OrderBy is not null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending is not null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        if (specification.IsPagingEnabled)
        {
            query = query.Skip(specification.Skip).Take(specification.Take);
        }

        return query;
    }
}
