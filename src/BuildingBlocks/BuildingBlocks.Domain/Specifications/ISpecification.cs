using System.Linq.Expressions;

namespace Advocacia.BuildingBlocks.Domain.Specifications;

/// <summary>
/// Critério de consulta reutilizável e combinável, para não espalhar filtros/includes
/// duplicados por handlers de query. Interpretado pela camada de Infrastructure
/// (ex.: SpecificationEvaluator sobre IQueryable do EF Core).
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }

    List<Expression<Func<T, object>>> Includes { get; }

    List<string> IncludeStrings { get; }

    Expression<Func<T, object>>? OrderBy { get; }

    Expression<Func<T, object>>? OrderByDescending { get; }

    int Skip { get; }

    int Take { get; }

    bool IsPagingEnabled { get; }
}
