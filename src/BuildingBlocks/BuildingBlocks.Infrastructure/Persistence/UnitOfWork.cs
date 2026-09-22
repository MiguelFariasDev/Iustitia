using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> sobre um DbContext do EF Core. Cada módulo
/// (Legal, Workflow, ...) terá seu próprio DbContext e, portanto, seu próprio UnitOfWork
/// registrado no escopo do módulo — hoje só existe Platform, então a resolução genérica
/// por <see cref="EfDbContext"/> é suficiente.
/// </summary>
public sealed class UnitOfWork(EfDbContext dbContext) : IUnitOfWork, IAsyncDisposable
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
        }
    }
}
