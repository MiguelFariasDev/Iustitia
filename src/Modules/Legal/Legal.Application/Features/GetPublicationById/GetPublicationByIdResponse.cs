namespace Advocacia.Modules.Legal.Application.Features.GetPublicationById;

/// <summary>
/// Detalhe de uma publicação. É o único contrato de leitura que devolve o
/// <paramref name="RawContent"/>: a consulta por id é a tela de leitura da publicação pelo
/// advogado. As listagens devolvem apenas um trecho (ver ListPublications).
/// </summary>
public sealed record GetPublicationByIdResponse(
    Guid Id,
    Guid TenantId,
    Guid? ProcessId,
    Guid? ClienteId,
    string? ClienteNome,
    Guid? ResponsavelUserId,
    string? Tribunal,
    string CnjNumber,
    string ExternalId,
    string Source,
    string RawContent,
    DateTimeOffset PublishedAt,
    DateTimeOffset CapturedAt,
    string? Type,
    string Status,
    bool IsRead,
    string? MetadataJson,
    string? ReviewNotes);
