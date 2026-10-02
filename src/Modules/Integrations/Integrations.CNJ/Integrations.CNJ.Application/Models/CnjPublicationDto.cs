namespace Advocacia.Modules.Integrations.CNJ.Application.Models;

/// <summary>
/// Publicação como devolvida pelo DJEN/CNJ, já normalizada para o vocabulário do
/// Iustitia (nomes em inglês, datas em UTC) — o contrato bruto da API externa fica
/// confinado em Integrations.CNJ.Infrastructure e nunca atravessa esta fronteira.
/// </summary>
/// <param name="ExternalId">
/// Identificador da publicação no CNJ. É a chave de deduplicação do módulo Legal
/// (ver ADR-056) — nunca nulo nem vazio.
/// </param>
/// <param name="CnjNumber">Número do processo no padrão CNJ, como veio da API (com ou sem máscara).</param>
/// <param name="Tribunal">Sigla do tribunal de origem.</param>
/// <param name="CommunicationType">Tipo de comunicação informado pelo CNJ (ex.: "Intimação").</param>
/// <param name="RawContent">
/// Texto integral da publicação. Pode conter dados pessoais e sigilosos — NUNCA logar
/// (ver claude.md, seção 14, e docs/modules/legal/cnj-capture.md).
/// </param>
/// <param name="PublishedAt">Data/hora de disponibilização, em UTC.</param>
/// <param name="MetadataJson">Metadados adicionais do CNJ serializados em JSON, para auditoria/reprocessamento.</param>
public sealed record CnjPublicationDto(
    string ExternalId,
    string CnjNumber,
    string? Tribunal,
    string? CommunicationType,
    string RawContent,
    DateTimeOffset PublishedAt,
    string? MetadataJson);
