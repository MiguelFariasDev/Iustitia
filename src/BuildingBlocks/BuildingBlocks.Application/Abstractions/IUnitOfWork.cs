namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Fronteira transacional explícita para os handlers de Command. A implementação
/// (ver BuildingBlocks.Infrastructure.Persistence.UnitOfWork) persiste as mudanças e,
/// na mesma transação, grava os eventos de domínio pendentes na tabela outbox.
/// Transações explícitas (Begin/Commit/Rollback) só são necessárias quando um handler
/// precisa coordenar múltiplas operações que devem ser atômicas além do SaveChanges
/// implícito de uma única invocação.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
