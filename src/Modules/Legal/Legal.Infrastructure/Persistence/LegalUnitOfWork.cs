using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Application.Abstractions;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence;

/// <summary><see cref="ILegalUnitOfWork"/> sobre o <see cref="LegalDbContext"/> — a lógica é a de <see cref="UnitOfWorkBase{TContext}"/>.</summary>
public sealed class LegalUnitOfWork(LegalDbContext dbContext) : UnitOfWorkBase<LegalDbContext>(dbContext), ILegalUnitOfWork;
