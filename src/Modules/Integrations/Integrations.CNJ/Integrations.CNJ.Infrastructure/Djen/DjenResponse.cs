using System.Text.Json.Serialization;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;

/// <summary>
/// Contrato bruto da API de comunicações do DJEN/CNJ. Mantido interno de propósito: a
/// forma da API externa (snake_case misturado com camelCase, datas como string, campos
/// opcionais sem contrato firme) nunca deve vazar para Application/Domain — o DjenClient
/// traduz para <c>CnjPublicationDto</c>.
/// </summary>
internal sealed record DjenResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("items")]
    public List<DjenItem>? Items { get; init; }
}

internal sealed record DjenItem
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("hash")]
    public string? Hash { get; init; }

    [JsonPropertyName("data_disponibilizacao")]
    public string? DataDisponibilizacao { get; init; }

    [JsonPropertyName("siglaTribunal")]
    public string? SiglaTribunal { get; init; }

    [JsonPropertyName("tipoComunicacao")]
    public string? TipoComunicacao { get; init; }

    [JsonPropertyName("nomeOrgao")]
    public string? NomeOrgao { get; init; }

    [JsonPropertyName("nomeClasse")]
    public string? NomeClasse { get; init; }

    [JsonPropertyName("texto")]
    public string? Texto { get; init; }

    [JsonPropertyName("numero_processo")]
    public string? NumeroProcesso { get; init; }

    [JsonPropertyName("numeroprocessocommascara")]
    public string? NumeroProcessoComMascara { get; init; }

    [JsonPropertyName("meio")]
    public string? Meio { get; init; }

    [JsonPropertyName("link")]
    public string? Link { get; init; }

    [JsonPropertyName("tipoDocumento")]
    public string? TipoDocumento { get; init; }

    [JsonPropertyName("numeroComunicacao")]
    public int? NumeroComunicacao { get; init; }
}
