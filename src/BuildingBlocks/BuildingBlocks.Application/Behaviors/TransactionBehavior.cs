using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using MediatR;

namespace Advocacia.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Behavior mais interno do pipeline (ver ADR-030): abre uma transação via
/// <see cref="IUnitOfWork"/> antes do handler rodar e decide commit/rollback a partir do
/// <see cref="Result.IsSuccess"/> devolvido — nunca de uma exceção (erros de negócio nunca
/// lançam, ver ADR-007). Só se aplica a Commands (<see cref="ICommandBase"/>); Queries
/// passam direto, sem transação. <see cref="IUnitOfWork.BeginTransactionAsync"/> já é
/// idempotente (não abre uma segunda transação se uma já estiver ativa no mesmo escopo),
/// então este behavior nunca aninha transações mesmo que um handler chame outro Send.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICommandBase)
        {
            return await next();
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        TResponse response;
        try
        {
            response = await next();
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        // TResponse é sempre Result ou Result<T> para um ICommandBase (ver ICommand/
        // ICommand<T>) — Result<T> herda de Result, então este cast nunca é nulo aqui.
        var result = (Result)(object)response!;

        if (result.IsSuccess)
        {
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        else
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
        }

        return response;
    }
}
