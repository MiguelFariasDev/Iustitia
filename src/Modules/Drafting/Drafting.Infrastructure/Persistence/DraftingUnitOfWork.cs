using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Application.Abstractions;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence;

/// <summary><see cref="IDraftingUnitOfWork"/> sobre o <see cref="DraftingDbContext"/> — a lógica é a de <see cref="UnitOfWorkBase{TContext}"/>.</summary>
public sealed class DraftingUnitOfWork(DraftingDbContext dbContext) : UnitOfWorkBase<DraftingDbContext>(dbContext), IDraftingUnitOfWork;
