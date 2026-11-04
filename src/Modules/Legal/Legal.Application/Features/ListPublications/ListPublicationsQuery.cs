using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.ListPublications;

/// <summary>
/// Listagem paginada de publicações do escritório da requisição, mais recentes primeiro.
/// <paramref name="Status"/> aceita um ou mais status separados por vírgula
/// (ex.: "Approved,Corrected"). <paramref name="From"/>/<paramref name="To"/> são datas de
/// divulgação inclusivas. <paramref name="Busca"/> casa com número do processo ou cliente.
/// <paramref name="Ordem"/> aceita recent (padrão), oldest, tribunal ou tribunal-desc.
/// <paramref name="OnlyPendingReview"/> é atalho para a fila de revisão.
/// </summary>
public sealed record ListPublicationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    bool? IsRead = null,
    Guid? ProcessId = null,
    bool OnlyPendingReview = false,
    Guid? ClienteId = null,
    string? Tribunal = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Busca = null,
    string? Ordem = null) : IQuery<ListPublicationsResponse>;
