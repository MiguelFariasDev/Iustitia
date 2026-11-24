using System.Text;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>Nomes das coleções do Qdrant. Uma coleção por tipo de conteúdo, não uma coleção gigante.</summary>
public static class Colecoes
{
    public const string LeisFederais = "leis_federais";
    public const string Jurisprudencia = "jurisprudencia";
    public const string Sumulas = "sumulas";
    public const string Regulamentos = "regulamentos";
    public const string LegislacaoEstadual = "legislacao_estadual";
    public const string LegislacaoMunicipal = "legislacao_municipal";
    public const string Tratados = "tratados";

    /// <summary>Documentos privados dos escritórios (tenant). Não é público: usa o IVectorStore com filtro de tenant.</summary>
    public const string DocumentosCliente = "documentos_cliente";

    /// <summary>Reservada: a coleção é criada, mas nada a alimenta nesta entrega.</summary>
    public const string ModelosDePeca = "modelos_de_peca";

    /// <summary>As coleções do acervo normativo público (sem tenant).</summary>
    public static readonly IReadOnlyList<string> Publicas =
        [LeisFederais, Jurisprudencia, Sumulas, Regulamentos, LegislacaoEstadual, LegislacaoMunicipal, Tratados];
}

/// <summary>Deduz a área do direito de sinais baratos (tribunal, título). O YAML do acervo NÃO traz área.</summary>
public static class AreaMapper
{
    private static readonly (string Trecho, string Area)[] PorTitulo =
    [
        ("constituição", "constitucional"),
        ("código civil", "civel"),
        ("código de processo civil", "civel"),
        ("código de defesa do consumidor", "consumidor"),
        ("proteção e defesa do consumidor", "consumidor"),
        ("código penal", "penal"),
        ("código de processo penal", "processo_penal"),
        ("consolidação das leis do trabalho", "trabalhista"),
        ("código tributário nacional", "tributario"),
        ("estatuto da criança", "infancia"),
        ("lei maria da penha", "penal"),
        ("violência doméstica", "penal"),
        ("locação de imóveis", "civel"),
        ("lei de locações", "civel"),
        ("lei geral de proteção de dados", "digital"),
        ("marco civil da internet", "digital"),
        ("improbidade administrativa", "administrativo"),
        ("licitações e contratos", "administrativo"),
        ("falências", "empresarial"),
        ("recuperação judicial", "empresarial"),
        ("sociedades por ações", "empresarial"),
        ("previdência social", "previdenciario"),
        ("benefícios da previdência", "previdenciario"),
    ];

    public static string? Deduzir(string? tribunal, string titulo, string repoPath)
    {
        switch (tribunal?.ToUpperInvariant())
        {
            case "TST":
                return "trabalhista";
            case "STM":
                return "militar";
            case "TSE":
                return "eleitoral";
        }

        var lower = titulo.ToLowerInvariant();
        foreach (var (trecho, area) in PorTitulo)
        {
            if (lower.Contains(trecho, StringComparison.Ordinal))
            {
                return area;
            }
        }

        var path = repoPath.ToLowerInvariant();

        return path.Contains("/rfb/", StringComparison.Ordinal) ? "tributario"
            : path.Contains("/anvisa/", StringComparison.Ordinal) || path.Contains("/ms/", StringComparison.Ordinal) ? "saude"
            : null;
    }

    /// <summary>"Civil", "civil", "Processo Civil" → "civil" / "processo_civil" (mesma forma do restante do módulo).</summary>
    public static string Normalizar(string area)
    {
        var sb = new StringBuilder();
        foreach (var ch in area.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        var key = sb.ToString().Trim('_');

        // Vocabulário do módulo (PecaCatalogo): "civel", não "civil".
        return key is "civil" or "processo_civil" ? "civel" : key;
    }
}
