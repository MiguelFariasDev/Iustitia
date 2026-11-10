using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Application.Features.Inbox;

/// <summary>
/// Não filtra por tenant explicitamente: o filtro global do AtendimentoDbContext e o RLS já
/// restringem ao escritório da requisição — atendimento de outro escritório não é encontrado
/// (404, não 403, para não confirmar que ele existe).
/// </summary>
public sealed class ListAtendimentosHandler(IAtendimentoRepository repository)
    : IQueryHandler<ListAtendimentosQuery, ListAtendimentosResponse>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<ListAtendimentosResponse>> Handle(
        ListAtendimentosQuery request, CancellationToken cancellationToken)
    {
        AtendimentoStatus? status = Enum.TryParse<AtendimentoStatus>(request.Status, ignoreCase: true, out var parsed)
            ? parsed
            : null;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;

        var filter = new AtendimentoFilter(status, request.AssignedUserId, request.Search?.Trim());

        var items = await repository.ListAsync(filter, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await repository.CountAsync(filter, cancellationToken);

        return Result.Success(new ListAtendimentosResponse(
            items.Select(item => item.Atendimento.ToResponse(item.LastMessagePreview)).ToList(),
            page,
            pageSize,
            total,
            (int)Math.Ceiling(total / (double)pageSize)));
    }
}

public sealed class GetAtendimentoByIdHandler(IAtendimentoRepository repository)
    : IQueryHandler<GetAtendimentoByIdQuery, AtendimentoResponse>
{
    public async Task<Result<AtendimentoResponse>> Handle(
        GetAtendimentoByIdQuery request, CancellationToken cancellationToken)
    {
        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(request.AtendimentoId), cancellationToken);

        return atendimento is null
            ? Result.Failure<AtendimentoResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND))
            : Result.Success(atendimento.ToResponse());
    }
}

public sealed class ListMensagensHandler(IAtendimentoRepository repository)
    : IQueryHandler<ListMensagensQuery, ListMensagensResponse>
{
    private const int MaxLimit = 200;
    private const int DefaultLimit = 50;

    public async Task<Result<ListMensagensResponse>> Handle(ListMensagensQuery request, CancellationToken cancellationToken)
    {
        var id = AtendimentoId.From(request.AtendimentoId);

        if (await repository.GetByIdAsync(id, cancellationToken) is null)
        {
            return Result.Failure<ListMensagensResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND));
        }

        var limit = request.Limit is < 1 or > MaxLimit ? DefaultLimit : request.Limit;

        // Busca uma a mais só para saber se existem mensagens anteriores.
        var newestFirst = await repository.ListMensagensAsync(id, request.Before, limit + 1, cancellationToken);
        var hasMore = newestFirst.Count > limit;

        var chronological = newestFirst.Take(limit).Reverse().Select(m => m.ToResponse()).ToList();

        return Result.Success(new ListMensagensResponse(chronological, hasMore));
    }
}
