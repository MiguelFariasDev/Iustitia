using System.Text;
using Advocacia.Modules.Drafting.Application.Busca;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>Nomes das coleções do Qdrant. Uma coleção por tipo de conteúdo, não uma coleção gigante.</summary>
public static class Colecoes
{
    public const string ConstituicaoFederal = "constituicao_federal";
    public const string LeisFederais = "leis_federais";
    public const string Jurisprudencia = "jurisprudencia";

    /// <summary>Anexos, isolados por caso: só o <c>ICasoVectorStore</c> acessa, sempre com filtro de tenant e de caso.</summary>
    public const string DocumentosCaso = "documentos_caso";

    /// <summary>Reservada para modelos do escritório: a coleção é criada, mas nada a alimenta nesta entrega.</summary>
    public const string PecasModelo = "pecas_modelo";

    /// <summary>O acervo normativo público, compartilhado entre escritórios (sem tenant).</summary>
    public static readonly IReadOnlyList<string> Publicas = [ConstituicaoFederal, LeisFederais, Jurisprudencia];

    public static readonly IReadOnlyList<string> Todas = [.. Publicas, DocumentosCaso, PecasModelo];
}

/// <summary>Deduz a área do direito de sinais baratos (tribunal, título). O YAML do acervo NÃO traz área.</summary>
public static class AreaMapper
{
    private static readonly (string Trecho, string Area)[] PorTitulo =
    [
        ("constituição", "constitucional"),
        ("código civil", "civel"),
        ("código de processo civil", "processual"),
        ("código de defesa do consumidor", "consumidor"),
        ("proteção e defesa do consumidor", "consumidor"),
        ("código penal", "penal"),
        ("código de processo penal", "penal"),
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

        if (repoPath.Equals("constituicao.md", StringComparison.OrdinalIgnoreCase))
        {
            return "constitucional";
        }

        var conhecida = NormasConhecidas.Todas.FirstOrDefault(n =>
            n.CaminhosRepo.Any(c => repoPath.StartsWith(c, StringComparison.OrdinalIgnoreCase)));
        if (conhecida is not null)
        {
            return conhecida.Area;
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

    private static readonly Dictionary<string, string[]> Vizinhas = new(StringComparer.Ordinal)
    {
        ["civel"] = ["consumidor", "empresarial", "digital", "familia"],
        ["consumidor"] = ["civel", "digital", "empresarial"],
        ["familia"] = ["civel", "infancia"],
        ["empresarial"] = ["civel", "tributario", "consumidor"],
        ["trabalhista"] = ["civel", "previdenciario"],
        ["previdenciario"] = ["trabalhista", "administrativo"],
        ["tributario"] = ["administrativo", "empresarial", "civel"],
        ["administrativo"] = ["civel", "tributario", "previdenciario"],
        ["penal"] = [],
    };

    /// <summary>
    /// Áreas que o filtro aceita para um caso da área dada: a própria, as vizinhas e as transversais. Um
    /// filtro de igualdade tiraria o Código Civil de um caso de consumidor. Trecho sem área sempre passa.
    /// </summary>
    public static IReadOnlyList<string> Compativeis(string? area) =>
        string.IsNullOrWhiteSpace(area)
            ? []
            : [area, .. Vizinhas.GetValueOrDefault(area, []), "constitucional", "processual"];

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
        return key switch
        {
            "civil" => "civel",
            "processo_civil" => "processual",
            "processo_penal" => "penal",
            _ => key,
        };
    }
}
