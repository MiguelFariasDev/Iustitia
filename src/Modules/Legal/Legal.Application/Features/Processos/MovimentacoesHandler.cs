using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Mappings;
using Advocacia.Modules.Legal.Domain.Processos;

namespace Advocacia.Modules.Legal.Application.Features.Processos;

/// <summary>
/// As movimentações vêm das PUBLICAÇÕES capturadas do diário para o processo — não do andamento
/// completo do tribunal. O DataJud, hoje, só entrega metadados do cadastro (ver ADR-064), então esta
/// lista mostra o que o diário publicou, e <c>Fonte</c> diz isso. Quando houver movimentos do
/// tribunal, entram aqui com outra <c>Fonte</c>, sem mudar o contrato.
/// </summary>
public sealed class ListMovimentacoesHandler(
    IProcessoRepository processoRepository,
    IPublicationRepository publicationRepository,
    ICurrentUser currentUser) : IQueryHandler<ListMovimentacoesQuery, MovimentacoesPageResponse>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 50;

    public async Task<Result<MovimentacoesPageResponse>> Handle(ListMovimentacoesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<MovimentacoesPageResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        // Processo de outro escritório também cai aqui: o filtro global de tenant o esconde.
        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(request.ProcessoId), cancellationToken);
        if (processo is null)
        {
            return Result.Failure<MovimentacoesPageResponse>(ErrorFactory.From(ErrorCode.PROCESS_NOT_FOUND));
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;
        var criteria = new PublicationSearchCriteria(ProcessId: request.ProcessoId);

        var publications = await publicationRepository.SearchAsync(
            tenantId, criteria, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await publicationRepository.CountSearchAsync(tenantId, criteria, cancellationToken);

        var items = publications
            .Select(publication => new MovimentacaoResponse(
                publication.Id.Value,
                publication.PublishedOn,
                publication.Type?.ToString() ?? "Publicacao",
                PublicationMappingProfile.BuildPreview(publication.RawContent.Value),
                publication.Source.ToString(),
                publication.IsRead))
            .ToList();

        return Result.Success(new MovimentacoesPageResponse(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }
}
