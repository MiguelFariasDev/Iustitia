using System.Linq.Expressions;

namespace Advocacia.BuildingBlocks.Domain.Specifications;

/// <summary>
/// Base para especificações concretas. Subclasses definem o critério no construtor
/// e usam os métodos protegidos para compor includes, ordenação e paginação.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    protected Specification()
    {
    }

    protected Specification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    public Expression<Func<T, bool>>? Criteria { get; private set; }

    public List<Expression<Func<T, object>>> Includes { get; } = [];

    public List<string> IncludeStrings { get; } = [];

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int Skip { get; private set; }

    public int Take { get; private set; }

    public bool IsPagingEnabled { get; private set; }

    protected void ApplyCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    protected void AddInclude(Expression<Func<T, object>> includeExpression) => Includes.Add(includeExpression);

    protected void AddInclude(string includeString) => IncludeStrings.Add(includeString);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression) => OrderBy = orderByExpression;

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression) =>
        OrderByDescending = orderByDescendingExpression;

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }
}
