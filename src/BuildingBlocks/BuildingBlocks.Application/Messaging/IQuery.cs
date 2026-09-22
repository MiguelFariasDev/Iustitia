using Advocacia.BuildingBlocks.Domain.Results;
using MediatR;

namespace Advocacia.BuildingBlocks.Application.Messaging;

/// <summary>Consulta de leitura, sempre retorna um valor (paginado ou não).</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
