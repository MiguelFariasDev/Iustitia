using System.Globalization;

namespace Advocacia.Modules.Legal.Domain.Shared;

/// <summary>
/// Deriva a sigla do tribunal a partir dos campos J (segmento) e TR (tribunal) do número
/// único, conforme a Resolução CNJ nº 65/2008.
///
/// Vive no domínio, e não na integração, porque a correspondência faz parte do PADRÃO do
/// número — quem lê "8.26" sabe que é o TJSP independentemente de existir DataJud. As
/// integrações traduzem esta sigla para o identificador delas (ver DataJudEndpoints).
///
/// O código do TR segue a ordem alfabética das unidades da federação nos segmentos
/// estadual (8), eleitoral (6) e militar estadual (9) — a mesma tabela serve aos três.
/// </summary>
public static class TribunalResolver
{
    /// <summary>UFs na ordem dos códigos TR 01..27 da Resolução 65/2008.</summary>
    private static readonly string[] UnidadesFederacao =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DFT", "ES", "GO", "MA", "MT", "MS", "MG",
        "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SE", "SP", "TO",
    ];

    /// <summary>Justiça Militar Estadual existe em três estados apenas (art. 125, §3º, CF).</summary>
    private static readonly Dictionary<int, string> MilitaresEstaduais = new()
    {
        [13] = "TJMMG",
        [21] = "TJMRS",
        [26] = "TJMSP",
    };

    /// <summary>
    /// Sigla do tribunal, ou <c>null</c> quando a combinação não corresponde a nenhum
    /// tribunal existente (ex.: segmento estadual com TR 40). Nulo é resposta legítima:
    /// o número pode ser estruturalmente válido e ainda assim não apontar para lugar algum.
    /// </summary>
    public static string? Resolve(int segmento, string tribunal)
    {
        if (!int.TryParse(tribunal, NumberStyles.None, CultureInfo.InvariantCulture, out var codigo))
        {
            return null;
        }

        return segmento switch
        {
            1 => "STF",
            2 => "CNJ",
            3 => "STJ",
            4 => codigo is >= 1 and <= 6 ? $"TRF{codigo}" : null,
            5 => codigo is >= 1 and <= 24 ? $"TRT{codigo}" : null,
            6 => codigo == 0 ? "TSE" : Uf(codigo) is { } ufEleitoral ? $"TRE-{ufEleitoral}" : null,
            7 => "STM",
            8 => Uf(codigo) is { } ufEstadual ? Estadual(ufEstadual) : null,
            9 => MilitaresEstaduais.GetValueOrDefault(codigo),
            _ => null,
        };
    }

    private static string? Uf(int codigo) =>
        codigo >= 1 && codigo <= UnidadesFederacao.Length ? UnidadesFederacao[codigo - 1] : null;

    // O tribunal do Distrito Federal é TJDFT, não "TJDF" — a sigla inclui os Territórios.
    private static string Estadual(string uf) => $"TJ{uf}";
}
