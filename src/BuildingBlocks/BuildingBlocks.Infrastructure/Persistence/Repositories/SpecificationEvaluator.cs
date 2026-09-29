using System.Linq.Expressions;
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
            query = ApplyOrder(query, specification.OrderBy, descending: false);
        }
        else if (specification.OrderByDescending is not null)
        {
            query = ApplyOrder(query, specification.OrderByDescending, descending: true);
        }

        if (specification.IsPagingEnabled)
        {
            query = query.Skip(specification.Skip).Take(specification.Take);
        }

        return query;
    }

    /// <summary>
    /// <see cref="ISpecification{T}.OrderBy"/> é tipado como <c>Expression&lt;Func&lt;T, object&gt;&gt;</c>
    /// para aceitar qualquer propriedade sem um parâmetro genérico a mais. O compilador insere
    /// um <c>Convert</c> de boxing quando a propriedade é um value type (DateTimeOffset, enum,
    /// int, ...), e o tradutor de LINQ do EF Core não sabe ordenar por uma expressão boxeada —
    /// falha com "could not be translated" em runtime, não em compilação.
    ///
    /// Desembrulhar o Convert e remontar o lambda com o tipo real da propriedade resolve para
    /// TODA specification do sistema, em vez de cada módulo descobrir o problema no primeiro
    /// OrderBy por data. Para tipos de referência (string, ...) o Convert não existe e o
    /// caminho é o mesmo de antes.
    /// </summary>
    private static IQueryable<TEntity> ApplyOrder<TEntity>(
        IQueryable<TEntity> query, Expression<Func<TEntity, object>> orderBy, bool descending)
        where TEntity : class
    {
        if (orderBy.Body is not UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            return descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        }

        var keyType = unary.Operand.Type;
        var typedLambda = Expression.Lambda(unary.Operand, orderBy.Parameters);

        var method = descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);

        var call = Expression.Call(
            typeof(Queryable),
            method,
            [typeof(TEntity), keyType],
            query.Expression,
            Expression.Quote(typedLambda));

        return query.Provider.CreateQuery<TEntity>(call);
    }
}
