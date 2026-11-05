using Advocacia.Modules.Workflow.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence;

/// <summary>
/// <see cref="IWorkflowUnitOfWork"/> sobre o <see cref="WorkflowDbContext"/>. Não reaproveita o
/// UnitOfWork do BuildingBlocks: aquele é tipado em <c>DbContext</c> e o container o resolve para o
/// PlatformDbContext (ver <see cref="IWorkflowUnitOfWork"/>).
/// </summary>
public sealed class WorkflowUnitOfWork(WorkflowDbContext dbContext) : IWorkflowUnitOfWork, IAsyncDisposable
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
