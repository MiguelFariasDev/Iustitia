using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;
using MapsterMapper;

namespace Advocacia.Modules.Legal.Application.Features.ListPublications;

public sealed class ListPublicationsHandler(
    IPublicationRepository publicationRepository,
    IClienteRepository clienteRepository,
    ICurrentUser currentUser,
    IMapper mapper) : IQueryHandler<ListPublicationsQuery, ListPublicationsResponse>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<ListPublicationsResponse>> Handle(
        ListPublicationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ListPublicationsResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var statuses = ParseStatuses(request.Status, request.OnlyPendingReview);
        if (statuses is null)
        {
            return Result.Failure<ListPublicationsResponse>(
                ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Status)));
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;

        var criteria = new PublicationSearchCriteria(
            statuses,
            request.IsRead,
            request.ProcessId,
            request.ClienteId,
            ResponsavelUserId: null,
            request.Tribunal,
            request.From,
            request.To,
            request.Busca,
            ParseSort(request.Ordem));

        var publications = await publicationRepository.SearchAsync(
            tenantId, criteria, (page - 1) * pageSize, pageSize, cancellationToken);
        var totalCount = await publicationRepository.CountSearchAsync(tenantId, criteria, cancellationToken);

        // Uma consulta de nomes para a página inteira, e não uma por linha.
        var clienteIds = publications.Where(p => p.ClienteId is not null).Select(p => p.ClienteId!.Value).Distinct().ToList();
        var nomes = await clienteRepository.GetNomesAsync(tenantId, clienteIds, cancellationToken);

        var items = publications
            .Select(publication => mapper.Map<PublicationListItem>(publication) with
            {
                ClienteNome = publication.ClienteId is { } id && nomes.TryGetValue(id, out var nome) ? nome : null,
            })
            .ToList();

        return Result.Success(new ListPublicationsResponse(
            items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    private static PublicationSort ParseSort(string? ordem) => ordem?.Trim().ToLowerInvariant() switch
    {
        "oldest" => PublicationSort.Oldest,
        "tribunal" => PublicationSort.TribunalAsc,
        "tribunal-desc" => PublicationSort.TribunalDesc,
        _ => PublicationSort.Recent,
    };

    /// <summary>Vazio = qualquer status; nulo = valor inválido.</summary>
    private static IReadOnlyCollection<PublicationStatus>? ParseStatuses(string? raw, bool onlyPendingReview)
    {
        if (onlyPendingReview)
        {
            return [PublicationStatus.PendingReview];
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var statuses = new List<PublicationStatus>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<PublicationStatus>(part, ignoreCase: true, out var status))
            {
                return null;
            }

            statuses.Add(status);
        }

        return statuses;
    }
}
