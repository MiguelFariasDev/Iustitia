using Advocacia.BuildingBlocks.Domain.Results;
using MediatR;

namespace Advocacia.BuildingBlocks.Application.Messaging;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
