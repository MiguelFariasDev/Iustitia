using System.Text;
using System.Text.RegularExpressions;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Tira do corpo o que não é norma nem tese antes de dividir: numeração de página do PDF de origem, sumário e
/// índice, fecho e assinaturas, e os campos de catálogo da jurisprudência (chaves, colegiado, datas de
/// cadastro). O Markdown integral continua guardado no storage — a limpeza só vale para o que é INDEXADO.
/// </summary>
public static partial class TextoJuridicoLimpo
{
    /// <summary>Campos dos registros de jurisprudência que carregam conteúdo jurídico. O resto é catálogo.</summary>
    private static readonly string[] CamposUteis =
    [
        "enunciado", "tese", "questao", "ementa", "situacao", "informacoescomplementares", "delimitacao", "modulacao",
        "assunto", "tema", "subtema", "excerto", "anotacoes", "referencialegal", "decisao", "dispositivo",
    ];

    public static string Limpar(string corpo, bool jurisprudencia)
    {
        var texto = corpo.Replace("\r\n", "\n");
        if (jurisprudencia)
        {
            texto = SemRegistroDuplicado(texto);
            texto = SoCamposUteis(texto);
            texto = SemCatalogoDeSumula(texto);
        }

        texto = SecaoVazia().Replace(texto, string.Empty);
        texto = JsonBlockFlattener.Flatten(texto);
        texto = NumeracaoDePagina().Replace(texto, string.Empty);
        texto = SemSumario(texto);

        return jurisprudencia ? texto : SemFecho(texto);
    }

    /// <summary>Quando há "## Tese" ou "## Questão" preenchidas, o JSON bruto que vem depois só as repete.</summary>
    private static string SemRegistroDuplicado(string texto)
    {
        var indice = texto.IndexOf("## Registro original do CNJ", StringComparison.OrdinalIgnoreCase);
        if (indice < 0)
        {
            return texto;
        }

        var antes = texto[..indice];

        return SecaoComConteudo().Matches(antes).Any(m => !m.Groups["v"].Value.Contains("não informado", StringComparison.OrdinalIgnoreCase))
            ? antes
            : texto;
    }

    private static string SoCamposUteis(string texto)
    {
        if (CampoDeRegistro().Count(texto) < 3)
        {
            return texto;
        }

        var sb = new StringBuilder();
        var manter = true;
        foreach (var linha in texto.Split('\n'))
        {
            if (CampoDeRegistro().Match(linha) is { Success: true } campo)
            {
                var nome = Busca.Bm25.SemAcento(campo.Groups["n"].Value.ToLowerInvariant());
                manter = CamposUteis.Any(u => nome.Contains(u, StringComparison.Ordinal));
            }
            else if (linha.StartsWith("## Registro", StringComparison.OrdinalIgnoreCase))
            {
                // "## Registro 3388": identificador do cadastro.
                manter = true;
                continue;
            }

            if (manter)
            {
                sb.Append(linha).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Cadernos de súmulas: o enunciado vem primeiro e é o que importa; órgão, datas, fonte de publicação e
    /// listas de referência legislativa codificada ("LEG:FED LEI:005764") saem.
    /// </summary>
    private static string SemCatalogoDeSumula(string texto)
    {
        if (!RotuloDeCatalogo().IsMatch(texto))
        {
            return texto;
        }

        var sb = new StringBuilder();
        var manter = true;
        foreach (var linha in texto.Split('\n'))
        {
            var limpa = linha.Trim();
            if (RotuloDeCatalogo().IsMatch(limpa))
            {
                manter = false;
            }
            else if (limpa.StartsWith("Excerto", StringComparison.OrdinalIgnoreCase) || limpa.StartsWith("Enunciado", StringComparison.OrdinalIgnoreCase))
            {
                manter = true;
                if (limpa.StartsWith("Excerto", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("\n## ").Append(limpa).Append('\n');
                    continue;
                }
            }

            if (manter)
            {
                sb.Append(linha).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>"SUMÁRIO" ou "ÍNDICE" como título, até o próximo título ou artigo.</summary>
    private static string SemSumario(string texto)
    {
        var sb = new StringBuilder();
        var dentro = false;
        foreach (var linha in texto.Split('\n'))
        {
            if (TituloDeSumario().IsMatch(linha))
            {
                dentro = true;
                continue;
            }

            if (dentro && (linha.StartsWith('#') || InicioDeArtigo().IsMatch(linha)))
            {
                dentro = false;
            }

            if (!dentro)
            {
                sb.Append(linha).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// "Brasília, 10 de janeiro de 2002; 181º da Independência…" e as assinaturas. Só corta até o próximo
    /// título: depois do fecho pode vir anexo, que é norma.
    /// </summary>
    private static string SemFecho(string texto)
    {
        var sb = new StringBuilder();
        var dentro = false;
        foreach (var linha in texto.Split('\n'))
        {
            if (Fecho().IsMatch(linha))
            {
                dentro = true;
                continue;
            }

            if (dentro && (linha.StartsWith('#') || InicioDeArtigo().IsMatch(linha) || InicioDeAnexo().IsMatch(linha)))
            {
                dentro = false;
            }

            if (!dentro)
            {
                sb.Append(linha).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>"## Tese" seguida de "não informado" (ou de nada): não é conteúdo.</summary>
    [GeneratedRegex(@"^##+\s+[^\n]+\n+\s*(?:n[ãa]o informad[oa]\.?|null|-)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SecaoVazia();

    [GeneratedRegex(@"^\s*(?:P[áa]gina\s+\d+\s+de\s+\d+|\d+\s*/\s*\d{2,})\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex NumeracaoDePagina();

    [GeneratedRegex(@"^##\s+(?:Quest[ãa]o|Tese)\s*\n+(?<v>[^#\n][^#]{30,})", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SecaoComConteudo();

    [GeneratedRegex(@"^###\s+(?<n>[\p{L}][\p{L}\d_ ]{1,60})\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CampoDeRegistro();

    [GeneratedRegex(@"^\s*(?:[ÓO]rg[ãa]o Julgador|Data d[ae] (?:Decis[ãa]o|Aprova[çc][ãa]o|Publica[çc][ãa]o)|Fonte(?: de Publica[çc][ãa]o)?|Refer[êe]ncias? Legislativas?|Precedentes(?: Origin[áa]rios)?)\s*:?\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RotuloDeCatalogo();

    [GeneratedRegex(@"^\s*(?:#{1,6}\s*)?\**(?:SUM[ÁA]RIO|[ÍI]NDICE(?:\s+(?:SISTEM[ÁA]TICO|ALFAB[ÉE]TICO|REMISSIVO|GERAL).*)?)\**\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TituloDeSumario();

    [GeneratedRegex(@"^\s*\**Art(?:igo)?\.?\s*\d", RegexOptions.CultureInvariant)]
    private static partial Regex InicioDeArtigo();

    [GeneratedRegex(@"^\s*\**ANEXO", RegexOptions.CultureInvariant)]
    private static partial Regex InicioDeAnexo();

    [GeneratedRegex(@"^\s*(?:Bras[íi]lia|Rio de Janeiro|Senado Federal|Congresso Nacional|C[âa]mara dos Deputados)[^.\n]{0,40},?\s+(?:em\s+)?\d{1,2}[ºo°]?\s+de\s+\p{L}+\s+de\s+\d{4}", RegexOptions.CultureInvariant)]
    private static partial Regex Fecho();
}
