using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.Modules.Financeiro.Application.Abstractions;

namespace Advocacia.Modules.Financeiro.Infrastructure.Persistence;

/// <summary><see cref="IFinanceiroUnitOfWork"/> sobre o <see cref="FinanceiroDbContext"/> — a lógica é a de <see cref="UnitOfWorkBase{TContext}"/>.</summary>
public sealed class FinanceiroUnitOfWork(FinanceiroDbContext dbContext) : UnitOfWorkBase<FinanceiroDbContext>(dbContext), IFinanceiroUnitOfWork;
