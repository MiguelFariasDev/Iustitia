using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;

namespace Advocacia.Modules.Drafting.Application.Conversao;

/// <param name="Suspeito">O trecho tenta dar ordens ao assistente: fica guardado, mas não vai ao prompt.</param>
public sealed record TrechoDocumento(
    int Ordem, int? Pagina, string? Secao, string Texto, int Tokens, string Hash, OrigemTexto Origem, double? Confianca, bool Suspeito);

/// <summary>
/// Divide um anexo pela estrutura do documento: página (a citação "p. 4" depende disto) → seção/cláusula →
/// parágrafo → frase. Cláusula ou seção curta nunca é dividida; parágrafos pequenos da mesma seção são
/// reunidos até o alvo. Overlap (uma frase) só no corte por frase, o único que separa um raciocínio.
/// </summary>
public sealed partial class DocumentoCasoChunker(ChunkingOptions options)
{
    private sealed record Unidade(int? Pagina, string? Secao, string Texto, OrigemTexto Origem, double? Confianca);

    public IReadOnlyList<TrechoDocumento> Dividir(DocumentoConvertido documento)
    {
        var resultado = new List<TrechoDocumento>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var repetidas = LinhasRepetidas(documento);
        string? secao = null;

        foreach (var bloco in documento.Blocos)
        {
            foreach (var unidade in Agrupar(Unidades(bloco, repetidas, ref secao)))
            {
                foreach (var parte in Cortar(unidade.Texto))
                {
                    var hash = TextoHash.DoTexto(parte);
                    if (parte.Length < 3 || !vistos.Add(hash))
                    {
                        continue;
                    }

                    resultado.Add(new TrechoDocumento(
                        resultado.Count, unidade.Pagina, unidade.Secao, parte, Tok(parte), hash, unidade.Origem, unidade.Confianca,
                        PromptInjectionGuard.Detect(parte)));
                }
            }
        }

        return resultado;
    }

    /// <summary>Quebra o bloco em seções: um título (Markdown, cláusula ou "DOS FATOS") abre a próxima.</summary>
    private static List<Unidade> Unidades(BlocoConvertido bloco, HashSet<string> repetidas, ref string? secao)
    {
        var unidades = new List<Unidade>();
        var linhas = new List<string>();
        var atual = bloco.Secao ?? secao;

        void Fechar()
        {
            var texto = string.Join('\n', linhas).Trim();
            if (texto.Length > 0)
            {
                unidades.Add(new Unidade(bloco.Pagina, atual, texto, bloco.Origem, bloco.Confianca));
            }

            linhas.Clear();
        }

        foreach (var linha in bloco.Markdown.Split('\n'))
        {
            if (repetidas.Contains(linha.Trim()))
            {
                continue;
            }

            if (Titulo().Match(linha) is { Success: true } titulo)
            {
                Fechar();
                atual = TextoDoTitulo(titulo);
            }

            linhas.Add(linha.TrimEnd());
        }

        Fechar();
        secao = atual;

        return unidades;
    }

    /// <summary>Junta seções pequenas e vizinhas da MESMA página até o alvo: 40 cláusulas de uma linha não viram 40 vetores.</summary>
    private IEnumerable<Unidade> Agrupar(List<Unidade> unidades)
    {
        Unidade? acumulada = null;
        foreach (var unidade in unidades)
        {
            if (acumulada is null)
            {
                acumulada = unidade;
                continue;
            }

            var junta = acumulada.Texto + "\n\n" + unidade.Texto;
            if (Tok(acumulada.Texto) < options.MinTokens && Tok(junta) <= options.AlvoTokens)
            {
                // A seção exibida é a primeira do grupo: é onde a leitura começa.
                acumulada = acumulada with { Texto = junta };
                continue;
            }

            yield return acumulada;
            acumulada = unidade;
        }

        if (acumulada is not null)
        {
            yield return acumulada;
        }
    }

    private IEnumerable<string> Cortar(string texto)
    {
        if (Tok(texto) <= options.MaxTokens)
        {
            yield return texto;
            yield break;
        }

        // Cabeçalho de tabela Markdown acompanha cada pedaço: linha sem cabeçalho não diz nada.
        var cabecalho = CabecalhoDeTabela(texto);
        var grupo = new List<string>();
        foreach (var paragrafo in Paragrafo().Split(texto).SelectMany(QuebrarParagrafo))
        {
            if (grupo.Count > 0 && Tok(string.Join('\n', grupo)) + Tok(paragrafo) > options.AlvoTokens)
            {
                yield return string.Join('\n', grupo);
                grupo = cabecalho is not null && paragrafo.TrimStart().StartsWith('|') ? [cabecalho] : [];
            }

            grupo.Add(paragrafo);
        }

        if (grupo.Count > 0)
        {
            yield return string.Join('\n', grupo);
        }
    }

    private IEnumerable<string> QuebrarParagrafo(string paragrafo)
    {
        if (Tok(paragrafo) <= options.AlvoTokens)
        {
            return [paragrafo];
        }

        // Tabela longa: por linha. Texto corrido: por frase, com a última frase repetida no pedaço seguinte.
        return paragrafo.TrimStart().StartsWith('|')
            ? paragrafo.Split('\n')
            : PorFrase(paragrafo);
    }

    private IEnumerable<string> PorFrase(string texto)
    {
        var grupo = new List<string>();
        string? anterior = null;
        foreach (var frase in Frase().Split(texto).Where(f => f.Trim().Length > 0).SelectMany(Forcar))
        {
            if (grupo.Count > 0 && Tok(string.Join(' ', grupo)) + Tok(frase) > options.AlvoTokens)
            {
                yield return string.Join(' ', grupo);
                grupo = anterior is not null && Tok(anterior) < options.AlvoTokens / 4 ? [anterior] : [];
            }

            grupo.Add(frase);
            anterior = frase;
        }

        if (grupo.Count > 0)
        {
            yield return string.Join(' ', grupo);
        }
    }

    /// <summary>Frase maior que o teto (OCR sem pontuação): corta em espaço.</summary>
    private IEnumerable<string> Forcar(string frase)
    {
        var max = (int)(options.AlvoTokens * options.CharsPorToken);
        while (frase.Length > max)
        {
            var corte = frase.LastIndexOf(' ', max);
            if (corte <= 0)
            {
                corte = max;
            }

            yield return frase[..corte];
            frase = frase[corte..].TrimStart();
        }

        yield return frase;
    }

    /// <summary>Cabeçalho e rodapé que se repetem em várias páginas não são conteúdo.</summary>
    private static HashSet<string> LinhasRepetidas(DocumentoConvertido documento)
    {
        var paginas = documento.Blocos.Where(b => b.Pagina is not null).GroupBy(b => b.Pagina!.Value).ToList();
        if (paginas.Count < 4)
        {
            return [];
        }

        return paginas
            .SelectMany(p =>
            {
                var linhas = string.Join('\n', p.Select(b => b.Markdown)).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

                return linhas.Take(3).Concat(linhas.TakeLast(3)).Distinct(StringComparer.Ordinal);
            })
            .GroupBy(l => l, StringComparer.Ordinal)
            .Where(g => g.Key.Length is >= 3 and <= 160 && g.Count() >= Math.Max(4, paginas.Count * 0.6))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? CabecalhoDeTabela(string texto)
    {
        var linhas = texto.Split('\n');
        for (var i = 0; i + 1 < linhas.Length; i++)
        {
            if (linhas[i].TrimStart().StartsWith('|') && Regex.IsMatch(linhas[i + 1], @"^\s*\|[\s:|-]+\|\s*$"))
            {
                return linhas[i] + "\n" + linhas[i + 1];
            }
        }

        return null;
    }

    private static string TextoDoTitulo(Match titulo)
    {
        var texto = titulo.Value.Trim().TrimStart('#').Trim().Trim('*').Trim();

        return texto.Length <= 120 ? texto : texto[..120];
    }

    private int Tok(string texto) => TokenEstimator.Estimate(texto, options.CharsPorToken);

    [GeneratedRegex(
        @"^\s*(?:#{1,6}\s+\S.*|\**(?:CL[ÁA]USULA|Cl[áa]usula)\s+[\wÀ-ÿªº°\.]+.{0,120}|\**(?:CAP[ÍI]TULO|SE[ÇC][ÃA]O|T[ÍI]TULO)\s+[IVXLC\d]+.{0,100}|(?:I{1,3}|IV|VI{0,3}|IX|X)\s*[-–—.]\s*D[AO]S?\s+[A-ZÀ-Ú ]{3,60}|D[AO]S?\s+[A-ZÀ-Ú][A-ZÀ-Ú ]{2,60})\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Titulo();

    [GeneratedRegex(@"\n{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Paragrafo();

    [GeneratedRegex(@"(?<=[\.\;\:\!\?])\s+(?=[A-ZÁÉÍÓÚÂÊÔÃÕÇ0-9§“""(])", RegexOptions.CultureInvariant)]
    private static partial Regex Frase();
}
