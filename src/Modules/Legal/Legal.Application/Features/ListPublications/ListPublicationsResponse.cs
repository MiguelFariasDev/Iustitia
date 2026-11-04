namespace Advocacia.Modules.Legal.Application.Features.ListPublications;

public sealed record ListPublicationsResponse(
    IReadOnlyList<PublicationListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// Item de listagem. Carrega <paramref name="ContentPreview"/> (um trecho curto) em vez do
/// inteiro teor: uma listagem de 20 publicações com o texto completo trafegaria centenas de
/// KB de dados sigilosos sem necessidade — o texto integral só sai pela consulta por id.
/// </summary>
public sealed record PublicationListItem(
    Guid Id,
    Guid? ProcessId,
    Guid? ClienteId,
    string? ClienteNome,
    Guid? ResponsavelUserId,
    string? Tribunal,
    string CnjNumber,
    string Source,
    string ContentPreview,
    DateTimeOffset PublishedAt,
    DateTimeOffset CapturedAt,
    string? Type,
    string Status,
    bool IsRead);
