namespace Advocacia.Modules.Drafting.Application.Busca;

/// <param name="Apelido">Como se cita: "Código Civil".</param>
/// <param name="Siglas">Como aparece em textos, sem ponto: "cc", "cpc".</param>
/// <param name="Numero">Número da norma, só dígitos; nulo para a Constituição.</param>
/// <param name="TipoContem">Trecho do tipo, para não confundir "Lei 5452" com "Decreto-Lei 5452".</param>
public sealed record NormaConhecida(
    string Apelido, string[] Siglas, string? Numero, int? Ano, string TipoContem, string[] CaminhosRepo, string Area);

/// <summary>
/// Normas de alta utilização: dão o nome pelo qual se cita ("Código Civil", e não "Lei nº 10.406 de
/// 10/01/2002"), resolvem "art. 186 do CC" para a lei certa e definem por onde a ingestão começa.
/// </summary>
public static class NormasConhecidas
{
    public static readonly IReadOnlyList<NormaConhecida> Todas =
    [
        new("Constituição Federal", ["cf", "crfb", "cf/88", "crfb/88", "constituição", "carta magna"], null, 1988, "constitui", ["constituicao.md"], "constitucional"),
        new("Código Civil", ["cc", "cc/02", "cc/2002"], "10406", 2002, "lei", ["federal/leis-ordinarias/2002/lei-10406-"], "civel"),
        new("Código de Processo Civil", ["cpc", "ncpc", "cpc/15", "cpc/2015"], "13105", 2015, "lei", ["federal/leis-ordinarias/2015/lei-13105-"], "processual"),
        new("Código de Defesa do Consumidor", ["cdc"], "8078", 1990, "lei", ["federal/leis-ordinarias/1990/lei-8078-"], "consumidor"),
        new("Consolidação das Leis do Trabalho", ["clt"], "5452", 1943, "decreto", ["federal/decretos-lei/1943/del-5452-"], "trabalhista"),
        new("Código Penal", ["cp"], "2848", 1940, "decreto", ["federal/decretos-lei/1940/del-2848-"], "penal"),
        new("Código de Processo Penal", ["cpp"], "3689", 1941, "decreto", ["federal/decretos-lei/1941/del-3689-"], "penal"),
        new("Código Tributário Nacional", ["ctn"], "5172", 1966, "lei", ["federal/leis-ordinarias/1966/lei-5172-"], "tributario"),
        new("Código de Trânsito Brasileiro", ["ctb"], "9503", 1997, "lei", ["federal/leis-ordinarias/1997/lei-9503-"], "administrativo"),
        new("Estatuto da Criança e do Adolescente", ["eca"], "8069", 1990, "lei", ["federal/leis-ordinarias/1990/lei-8069-"], "familia"),
        new("Estatuto da Pessoa Idosa", ["estatuto do idoso"], "10741", 2003, "lei", ["federal/leis-ordinarias/2003/lei-10741-"], "civel"),
        new("Lei do Inquilinato", ["lei de locações", "lei das locações"], "8245", 1991, "lei", ["federal/leis-ordinarias/1991/lei-8245-"], "civel"),
        new("Lei dos Juizados Especiais", ["lje"], "9099", 1995, "lei", ["federal/leis-ordinarias/1995/lei-9099-"], "processual"),
        new("Lei dos Juizados Especiais Federais", [], "10259", 2001, "lei", ["federal/leis-ordinarias/2001/lei-10259-"], "processual"),
        new("Lei Geral de Proteção de Dados", ["lgpd"], "13709", 2018, "lei", ["federal/leis-ordinarias/2018/lei-13709-"], "digital"),
        new("Marco Civil da Internet", [], "12965", 2014, "lei", ["federal/leis-ordinarias/2014/lei-12965-"], "digital"),
        new("Lei Maria da Penha", [], "11340", 2006, "lei", ["federal/leis-ordinarias/2006/lei-11340-"], "penal"),
        new("Lei de Benefícios da Previdência Social", ["lbps"], "8213", 1991, "lei", ["federal/leis-ordinarias/1991/lei-8213-"], "previdenciario"),
        new("Lei de Custeio da Seguridade Social", [], "8212", 1991, "lei", ["federal/leis-ordinarias/1991/lei-8212-"], "previdenciario"),
        new("Lei Orgânica da Assistência Social", ["loas"], "8742", 1993, "lei", ["federal/leis-ordinarias/1993/lei-8742-"], "previdenciario"),
        new("Estatuto dos Servidores Públicos Federais", [], "8112", 1990, "lei", ["federal/leis-ordinarias/1990/lei-8112-"], "administrativo"),
        new("Lei do Processo Administrativo Federal", [], "9784", 1999, "lei", ["federal/leis-ordinarias/1999/lei-9784-"], "administrativo"),
        new("Lei de Improbidade Administrativa", ["lia"], "8429", 1992, "lei", ["federal/leis-ordinarias/1992/lei-8429-"], "administrativo"),
        new("Lei de Licitações e Contratos", [], "14133", 2021, "lei", ["federal/leis-ordinarias/2021/lei-14133-"], "administrativo"),
        new("Lei do Mandado de Segurança", [], "12016", 2009, "lei", ["federal/leis-ordinarias/2009/lei-12016-"], "processual"),
        new("Lei da Ação Civil Pública", ["lacp"], "7347", 1985, "lei", ["federal/leis-ordinarias/1985/lei-7347-"], "processual"),
        new("Lei de Execução Fiscal", ["lef"], "6830", 1980, "lei", ["federal/leis-ordinarias/1980/lei-6830-"], "tributario"),
        new("Lei de Falências e Recuperação", ["lfre"], "11101", 2005, "lei", ["federal/leis-ordinarias/2005/lei-11101-"], "empresarial"),
        new("Lei das Sociedades por Ações", ["lsa", "lei das s.a."], "6404", 1976, "lei", ["federal/leis-ordinarias/1976/lei-6404-"], "empresarial"),
        new("Lei de Alimentos", [], "5478", 1968, "lei", ["federal/leis-ordinarias/1968/lei-5478-"], "familia"),
        new("Lei da Guarda Compartilhada", [], "13058", 2014, "lei", ["federal/leis-ordinarias/2014/lei-13058-"], "familia"),
        new("Estatuto da Advocacia", ["eoab"], "8906", 1994, "lei", ["federal/leis-ordinarias/1994/lei-8906-"], "processual"),
        new("Lei dos Registros Públicos", ["lrp"], "6015", 1973, "lei", ["federal/leis-ordinarias/1973/lei-6015-"], "civel"),
        new("Lei de Arbitragem", [], "9307", 1996, "lei", ["federal/leis-ordinarias/1996/lei-9307-"], "processual"),
        new("Lei da Liberdade Econômica", [], "13874", 2019, "lei", ["federal/leis-ordinarias/2019/lei-13874-"], "empresarial"),
        new("Lei de Introdução às Normas do Direito Brasileiro", ["lindb"], "4657", 1942, "decreto", ["federal/decretos-lei/1942/del-4657-"], "civel"),
    ];

    /// <summary>Jurisprudência de alta utilização: vinculante, de tribunal superior ou uniformizada.</summary>
    public static readonly IReadOnlyList<string> JurisprudenciaAltaUtilizacao =
    [
        "jurisprudencia/stf/sumulas-vinculantes/", "jurisprudencia/stf/sumulas/", "jurisprudencia/stj/sumulas/",
        "jurisprudencia/stj/repetitivos/", "jurisprudencia/tst/sumulas/", "jurisprudencia/tst/orientacoes-jurisprudenciais/",
        "jurisprudencia/tnu/sumulas/", "jurisprudencia/stf/repercussao-geral/",
    ];

    public static bool AltaUtilizacao(string caminhoRelativo)
    {
        var caminho = caminhoRelativo.Replace('\\', '/');

        return Todas.Any(n => n.CaminhosRepo.Any(p => caminho.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            || JurisprudenciaAltaUtilizacao.Any(p => caminho.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            || caminho.StartsWith("federal/leis-complementares/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A norma conhecida por trás de um registro do acervo (número E tipo E ano têm de bater).</summary>
    public static NormaConhecida? Identificar(string tipo, string? numero, int? ano, string colecao)
    {
        if (string.Equals(colecao, Normas.Colecoes.ConstituicaoFederal, StringComparison.Ordinal))
        {
            return Todas[0];
        }

        if (string.IsNullOrWhiteSpace(numero) || colecao == Normas.Colecoes.Jurisprudencia)
        {
            return null;
        }

        var digitos = numero.Replace(".", string.Empty).Trim();

        return Todas.FirstOrDefault(n =>
            n.Numero == digitos
            && (ano is null || n.Ano == ano)
            && tipo.Contains(n.TipoContem, StringComparison.OrdinalIgnoreCase)
            && (n.TipoContem != "lei" || !tipo.Contains("decreto", StringComparison.OrdinalIgnoreCase)));
    }

    public static NormaConhecida? PorNome(string nome)
    {
        var limpo = nome.Trim().TrimEnd('.').ToLowerInvariant();

        return Todas.FirstOrDefault(n =>
            string.Equals(n.Apelido, limpo, StringComparison.OrdinalIgnoreCase)
            || n.Siglas.Contains(limpo, StringComparer.OrdinalIgnoreCase));
    }
}
