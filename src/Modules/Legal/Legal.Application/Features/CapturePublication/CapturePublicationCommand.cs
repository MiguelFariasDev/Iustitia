using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Application.Features.CapturePublication;

/// <summary>
/// Persiste uma publicação capturada de um diário. É um comando de uso INTERNO (job de
/// captura, reprocessamento manual), não exposto pela API pública: por isso carrega o
/// <paramref name="TenantId"/> explicitamente em vez de resolvê-lo de <c>ICurrentUser</c> —
/// o job roda sem usuário autenticado e precisa capturar para o escritório certo.
///
/// Se a API algum dia expuser captura manual, o endpoint deve preencher TenantId a partir
/// do JWT e nunca do corpo da requisição.
/// </summary>
/// <param name="TenantId">Escritório ao qual a publicação pertence.</param>
/// <param name="ExternalId">Identificador da publicação na origem — chave de deduplicação (ver ADR-056).</param>
/// <param name="CnjNumber">Número do processo no padrão CNJ, com ou sem máscara.</param>
/// <param name="Source">Diário de origem.</param>
/// <param name="RawContent">Inteiro teor. NUNCA logar este campo.</param>
/// <param name="PublishedAt">Data/hora de disponibilização no diário.</param>
/// <param name="Type">Natureza do ato, quando a origem informa.</param>
/// <param name="MetadataJson">Metadados extras da origem, em JSON.</param>
/// <param name="ProcessId">Processo cadastrado que originou a consulta, quando houver.</param>
/// <param name="ClienteId">Cliente do processo, quando houver — denormalizado para filtrar sem join.</param>
/// <param name="ResponsavelUserId">Advogado responsável pelo processo — destinatário do alerta.</param>
public sealed record CapturePublicationCommand(
    Guid TenantId,
    string ExternalId,
    string CnjNumber,
    PublicationSource Source,
    string RawContent,
    DateTimeOffset PublishedAt,
    PublicationType? Type = null,
    string? MetadataJson = null,
    Guid? ProcessId = null,
    Guid? ClienteId = null,
    Guid? ResponsavelUserId = null) : ICommand<CapturePublicationResponse>;
