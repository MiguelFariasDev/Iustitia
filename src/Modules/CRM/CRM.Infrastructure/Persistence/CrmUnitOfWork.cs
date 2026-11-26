using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.Modules.CRM.Application.Abstractions;

namespace Advocacia.Modules.CRM.Infrastructure.Persistence;

/// <summary><see cref="ICrmUnitOfWork"/> sobre o <see cref="CrmDbContext"/> — a lógica é a de <see cref="UnitOfWorkBase{TContext}"/>.</summary>
public sealed class CrmUnitOfWork(CrmDbContext dbContext) : UnitOfWorkBase<CrmDbContext>(dbContext), ICrmUnitOfWork;
