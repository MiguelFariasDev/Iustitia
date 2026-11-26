using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.Modules.Atendimentos.Application.Abstractions;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence;

/// <summary><see cref="IAtendimentoUnitOfWork"/> sobre o <see cref="AtendimentoDbContext"/> — a lógica é a de <see cref="UnitOfWorkBase{TContext}"/>.</summary>
public sealed class AtendimentoUnitOfWork(AtendimentoDbContext dbContext) : UnitOfWorkBase<AtendimentoDbContext>(dbContext), IAtendimentoUnitOfWork;
