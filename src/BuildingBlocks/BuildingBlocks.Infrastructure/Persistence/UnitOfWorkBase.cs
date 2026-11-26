using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Unidade de trabalho sobre um DbContext do EF Core. Cada módulo tem o seu próprio contexto e,
/// portanto, a sua própria unidade de trabalho — um tipo fino que só fixa o contexto
/// (<c>LegalUnitOfWork : UnitOfWorkBase&lt;LegalDbContext&gt;, ILegalUnitOfWork</c>). A interface do módulo existe
/// porque o container resolve <see cref="IUnitOfWork"/> para um único contexto: sem ela, um handler do Legal
/// receberia a unidade de trabalho de outro módulo e salvaria no banco errado.
/// </summary>
public abstract class UnitOfWorkBase<TContext>(TContext dbContext) : IUnitOfWork, IAsyncDisposable
    where TContext : EfDbContext
{
    private IDbContextTransaction? _currentTransaction;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            return;
        }

        _currentTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            return;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
