using System.Linq.Expressions;
using System.Reflection;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;

/// <summary>
/// DbContext base para todo módulo do sistema (um DbContext por módulo, mesmo banco
/// físico — ver ADR-001, Modular Monolith). Responsável por:
///   - Aplicar via <c>ApplyConfigurationsFromAssembly</c> as configurações Fluent API
///     do assembly concreto (ex.: Platform.Infrastructure).
///   - Aplicar automaticamente dois filtros globais a qualquer entidade que implemente
///     as interfaces correspondentes: soft delete (<see cref="ISoftDeletable"/>) e
///     isolamento por tenant (<see cref="IHasTenant"/>). Entidades que implementam as
///     duas ao mesmo tempo (ex.: User) recebem os dois filtros combinados com AND.
/// O isolamento por tenant aqui é a segunda camada de defesa — a primeira e definitiva
/// é o Row-Level Security do Postgres (ver ADR-004 e TenantContextInterceptor).
/// </summary>
public abstract class BaseDbContext : EfDbContext, IBaseDbContext
{
    private static readonly MethodInfo ConfigureGlobalFiltersMethod =
        typeof(BaseDbContext).GetMethod(nameof(ConfigureGlobalFilters), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// tenantContext é obrigatório (não aceita null nem tem valor padrão) de propósito:
    /// o EF Core cacheia o modelo compilado (com os HasQueryFilter já fechados) por TIPO
    /// de DbContext, não por instância — se algum construtor no processo passasse null
    /// "só para uma migration", o filtro de tenant nunca entraria no modelo cacheado e
    /// TODAS as instâncias seguintes, mesmo com um tenantContext real, ficariam sem
    /// isolamento por tenant no C# (o RLS do Postgres continuaria protegendo, mas essa
    /// camada de defesa em profundidade seria perdida silenciosamente).
    /// </summary>
    protected BaseDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Ponto de acesso usado DENTRO do filtro global via <c>this.</c> — nunca copiar para
    /// uma variável local antes de usar num HasQueryFilter. O EF Core rebinda automaticamente
    /// o `this` de um HasQueryFilter para a instância de DbContext que está de fato
    /// executando a query, mesmo com o modelo cacheado por tipo (não por instância); uma
    /// variável local capturada, em vez disso, fica "presa" para sempre à primeira
    /// instância que construiu o modelo, ignorando o tenant de qualquer instância seguinte.
    /// </summary>
    private ITenantContext TenantContextForFilters => _tenantContext;

    /// <summary>
    /// Acesso seguro ao tenant atual para uso DENTRO do filtro global (mesma regra de
    /// `this.`-binding do member acima). EF Core traduz "!HasTenant || TenantId == X" para
    /// SQL parametrizado — o parâmetro X é avaliado como valor sempre, mesmo quando o OR já
    /// seria satisfeito por "!HasTenant" (não há curto-circuito real, é uma expressão de
    /// runtime, não um branch em C#). Sem esse getter, uma requisição anônima (sem tenant
    /// definido, ex.: Login/CreateTenant) lançaria a exceção de "TenantId sem tenant
    /// definido" do <see cref="ITenantContext"/> só por o EF precisar montar o parâmetro,
    /// mesmo que o filtro nunca fosse de fato restringir nada nesse caso.
    /// </summary>
    private Guid TenantIdForFilters => TenantContextForFilters.HasTenant ? TenantContextForFilters.TenantId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);
            var hasTenant = typeof(IHasTenant).IsAssignableFrom(entityType.ClrType);

            if (isSoftDeletable || hasTenant)
            {
                ConfigureGlobalFiltersMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Monta e aplica o HasQueryFilter combinado para uma entidade concreta. Precisa ser
    /// invocado via reflection (MakeGenericMethod) porque HasQueryFilter é genérico por
    /// tipo de entidade e o tipo só é conhecido em runtime ao iterar o modelo.
    /// </summary>
    private void ConfigureGlobalFilters<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        Expression<Func<TEntity, bool>>? filter = null;

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            // EF.Property (não um cast para a interface) é obrigatório aqui: o tradutor de
            // LINQ do EF Core não consegue mapear "((ISoftDeletable)entity).IsDeleted" de
            // volta para a coluna da entidade concreta e ignora o filtro silenciosamente.
            Expression<Func<TEntity, bool>> softDeleteFilter =
                entity => !EF.Property<bool>(entity, nameof(ISoftDeletable.IsDeleted));
            filter = softDeleteFilter;
        }

        if (typeof(IHasTenant).IsAssignableFrom(typeof(TEntity)))
        {
            Expression<Func<TEntity, bool>> tenantFilter = entity =>
                !this.TenantContextForFilters.HasTenant
                || EF.Property<Guid>(entity, nameof(IHasTenant.TenantId)) == this.TenantIdForFilters;

            filter = filter is null ? tenantFilter : CombineWithAnd(filter, tenantFilter);
        }

        if (filter is not null)
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
        }
    }

    private static Expression<Func<TEntity, bool>> CombineWithAnd<TEntity>(
        Expression<Func<TEntity, bool>> left, Expression<Func<TEntity, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body);
        var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(leftBody!, rightBody!), parameter);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
