using Advocacia.BuildingBlocks.Domain.Results;
using MediatR;

namespace Advocacia.BuildingBlocks.Application.Messaging;

/// <summary>
/// Marcador comum a <see cref="ICommand"/>/<see cref="ICommand{TResponse}"/>, sem
/// depender do tipo de resposta — usado por TransactionBehavior para distinguir Commands
/// de Queries via <c>request is ICommandBase</c>, já que os dois genéricos de ICommand não
/// compartilham uma interface não-genérica comum além desta.
/// </summary>
public interface ICommandBase;

/// <summary>Comando que não retorna valor além do sucesso/falha da operação.</summary>
public interface ICommand : ICommandBase, IRequest<Result>;

/// <summary>Comando que retorna um valor em caso de sucesso.</summary>
public interface ICommand<TResponse> : ICommandBase, IRequest<Result<TResponse>>;
