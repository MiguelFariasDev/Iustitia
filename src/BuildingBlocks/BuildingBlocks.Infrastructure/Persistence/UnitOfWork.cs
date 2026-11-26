using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Unidade de trabalho do contexto genérico (<see cref="EfDbContext"/>), usada pela Platform.</summary>
public sealed class UnitOfWork(EfDbContext dbContext) : UnitOfWorkBase<EfDbContext>(dbContext);
