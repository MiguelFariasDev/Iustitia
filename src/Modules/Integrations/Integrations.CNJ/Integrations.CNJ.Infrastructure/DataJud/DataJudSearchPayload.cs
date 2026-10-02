using System.Text.Json.Serialization;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Formato de fio da API do DataJud, que é um Elasticsearch exposto diretamente. Estes tipos
/// são INTERNOS à infraestrutura de propósito: o envelope de busca (hits/_source/_score) é
/// detalhe do Elasticsearch, e vazá-lo para a aplicação amarraria o resto do sistema a ele.
/// O cliente traduz para DataJudProcesso na fronteira.
/// </summary>
internal sealed record DataJudSearchRequest(
    [property: JsonPropertyName("query")] DataJudQuery Query,
    [property: JsonPropertyName("size")] int Size)
{
    /// <summary>
    /// Busca pelo número do processo. O DataJud espera os 20 dígitos SEM máscara —
    /// enviar "0001234-56.2026.8.26.0100" devolve zero resultados sem erro nenhum.
    /// </summary>
    public static DataJudSearchRequest ByNumeroProcesso(string digitos) =>
        new(new DataJudQuery(new DataJudMatch(digitos)), Size: 1);
}

internal sealed record DataJudQuery(
    [property: JsonPropertyName("match")] DataJudMatch Match);

internal sealed record DataJudMatch(
    [property: JsonPropertyName("numeroProcesso")] string NumeroProcesso);

internal sealed record DataJudSearchResponse(
    [property: JsonPropertyName("took")] int Took,
    [property: JsonPropertyName("timed_out")] bool TimedOut,
    [property: JsonPropertyName("hits")] DataJudHits? Hits);

internal sealed record DataJudHits(
    [property: JsonPropertyName("total")] DataJudTotal? Total,
    [property: JsonPropertyName("hits")] IReadOnlyList<DataJudHit>? Items);

internal sealed record DataJudTotal(
    [property: JsonPropertyName("value")] int Value,
    [property: JsonPropertyName("relation")] string? Relation);

internal sealed record DataJudHit(
    [property: JsonPropertyName("_id")] string? Id,
    [property: JsonPropertyName("_index")] string? Index,
    [property: JsonPropertyName("_source")] DataJudSource? Source);

internal sealed record DataJudSource(
    [property: JsonPropertyName("numeroProcesso")] string? NumeroProcesso,
    [property: JsonPropertyName("tribunal")] string? Tribunal,
    [property: JsonPropertyName("grau")] string? Grau,
    [property: JsonPropertyName("dataAjuizamento")][property: JsonConverter(typeof(DataJudDateTimeConverter))] DateTimeOffset? DataAjuizamento,
    [property: JsonPropertyName("nivelSigilo")] int NivelSigilo,
    [property: JsonPropertyName("classe")] DataJudTabelado? Classe,
    [property: JsonPropertyName("sistema")] DataJudTabelado? Sistema,
    [property: JsonPropertyName("formato")] DataJudTabelado? Formato,
    [property: JsonPropertyName("orgaoJulgador")] DataJudOrgao? OrgaoJulgador,
    [property: JsonPropertyName("assuntos")] IReadOnlyList<DataJudTabelado>? Assuntos,
    [property: JsonPropertyName("movimentos")] IReadOnlyList<DataJudMovimentoPayload>? Movimentos);

internal sealed record DataJudTabelado(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("nome")] string? Nome);

internal sealed record DataJudOrgao(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("codigoMunicipioIBGE")] int? CodigoMunicipioIBGE);

internal sealed record DataJudMovimentoPayload(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("dataHora")][property: JsonConverter(typeof(DataJudDateTimeConverter))] DateTimeOffset? DataHora);
