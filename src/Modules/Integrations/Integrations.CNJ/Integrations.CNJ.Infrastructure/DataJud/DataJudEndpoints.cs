namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Traduz a sigla do tribunal para o índice correspondente na API do DataJud
/// (<c>/api_publica_{alias}/_search</c>).
///
/// O alias é a sigla em minúsculas em quase todos os casos — a exceção são os regionais
/// eleitorais, cuja sigla canônica tem hífen ("TRE-CE") e o índice não ("tre-ce" é o alias,
/// mas a sigla chega com hífen do TribunalResolver, então a normalização basta).
///
/// Nem todo tribunal existente está no DataJud: o STF não publica por ali, e alguns
/// tribunais não enviam dados. Por isso a lista é EXPLÍCITA — derivar o alias por
/// minúsculas geraria uma URL plausível para um índice inexistente, e o erro chegaria ao
/// usuário como 404 genérico em vez de "tribunal não suportado".
/// </summary>
public static class DataJudEndpoints
{
    private static readonly HashSet<string> Suportados = new(StringComparer.OrdinalIgnoreCase)
    {
        // Superiores (o STF não expõe índice no DataJud).
        "TST", "TSE", "STJ", "STM",

        // Justiça Federal.
        "TRF1", "TRF2", "TRF3", "TRF4", "TRF5", "TRF6",

        // Justiça Estadual.
        "TJAC", "TJAL", "TJAP", "TJAM", "TJBA", "TJCE", "TJDFT", "TJES", "TJGO",
        "TJMA", "TJMT", "TJMS", "TJMG", "TJPA", "TJPB", "TJPR", "TJPE", "TJPI",
        "TJRJ", "TJRN", "TJRS", "TJRO", "TJRR", "TJSC", "TJSE", "TJSP", "TJTO",

        // Justiça do Trabalho.
        "TRT1", "TRT2", "TRT3", "TRT4", "TRT5", "TRT6", "TRT7", "TRT8",
        "TRT9", "TRT10", "TRT11", "TRT12", "TRT13", "TRT14", "TRT15", "TRT16",
        "TRT17", "TRT18", "TRT19", "TRT20", "TRT21", "TRT22", "TRT23", "TRT24",

        // Justiça Eleitoral (regionais).
        "TRE-AC", "TRE-AL", "TRE-AP", "TRE-AM", "TRE-BA", "TRE-CE", "TRE-DFT",
        "TRE-ES", "TRE-GO", "TRE-MA", "TRE-MT", "TRE-MS", "TRE-MG", "TRE-PA",
        "TRE-PB", "TRE-PR", "TRE-PE", "TRE-PI", "TRE-RJ", "TRE-RN", "TRE-RS",
        "TRE-RO", "TRE-RR", "TRE-SC", "TRE-SE", "TRE-SP", "TRE-TO",

        // Justiça Militar Estadual.
        "TJMMG", "TJMRS", "TJMSP",
    };

    /// <summary>Alias do índice, ou <c>null</c> quando o tribunal não é atendido pelo DataJud.</summary>
    public static string? GetAlias(string? tribunal) =>
        !string.IsNullOrWhiteSpace(tribunal) && Suportados.Contains(tribunal)
            ? tribunal.ToLowerInvariant()
            : null;

    /// <summary>Caminho de busca do tribunal, ou <c>null</c> se não suportado.</summary>
    public static string? GetSearchPath(string? tribunal) =>
        GetAlias(tribunal) is { } alias ? $"/api_publica_{alias}/_search" : null;

    /// <summary>
    /// Este módulo recebe a SIGLA pronta, nunca o número CNJ: derivar o tribunal do número é
    /// regra do padrão CNJ e mora no domínio de Legal (TribunalResolver). Integrations não
    /// conhece Legal — a dependência entre módulos só existe na direção contrária (ADR-001).
    /// </summary>
    public static IReadOnlyCollection<string> TribunaisSuportados => Suportados;
}
