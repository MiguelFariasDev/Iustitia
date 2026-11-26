using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Application.Busca;

namespace Advocacia.Modules.Drafting.Application.Validacao;

/// <param name="Texto">Conteúdo entre colchetes, como escrito.</param>
/// <param name="Posicao">Posição do colchete de abertura no texto gerado.</param>
public sealed record CitacaoEncontrada(string Texto, int Posicao);

/// <summary>Reconhece citações entre colchetes e as separa de marcadores de preenchimento ("[NOME DO AUTOR]").</summary>
public static partial class CitacaoParser
{
    [GeneratedRegex(@"\[([^\[\]\n]{3,300})\]", RegexOptions.CultureInvariant)]
    private static partial Regex Colchetes();

    /// <summary>Sinais de que o colchete é citação: artigo, página, tribunal, súmula, número de norma, URN ou arquivo.</summary>
    [GeneratedRegex(
        @",\s*(?:arts?\.|artigos?|p\.|fls?\.|texto\b)|\b(?:STF|STJ|TST|TSE|STM|TNU|TJ[A-Z]{2}|TRF\d?|TRT\d{0,2})\b|s[úu]mula|\btema\s+(?:n|repetitivo|\d)|\b(?:REsp|AREsp|RE|ADI|ADC|ADPF|HC|MS|AI)\s*(?:n[ºo°.]*\s*)?\d|\b(?:lei|decreto|c[óo]digo|constitui[çc][ãa]o)\b.{0,60}\d|urn:lex|\.(?:pdf|docx?|txt|md|eml|xlsx?|csv|jpe?g|png|mp3|wav|m4a|ogg)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PareceCitacao();

    [GeneratedRegex(@",\s*(?:arts?\.?|artigos?)\s*(?<lista>\d.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SufixoDeArtigos();

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})+(?:-[A-Z])?|\d+(?:-[A-Z])?", RegexOptions.CultureInvariant)]
    private static partial Regex NumeroDeArtigo();

    public static IReadOnlyList<CitacaoEncontrada> Extrair(string texto) =>
        Colchetes().Matches(texto)
            .Select(m => new CitacaoEncontrada(m.Groups[1].Value.Trim(), m.Index))
            .Where(c => PareceCitacao().IsMatch(c.Texto))
            .ToList();

    /// <summary>
    /// "Código Civil, arts. 186 e 927" → ("Código Civil", [186, 927]). Incisos, parágrafos e alíneas depois do
    /// artigo não mudam qual artigo é citado. Nulo quando não há sufixo de artigo.
    /// </summary>
    public static (string Norma, IReadOnlyList<string> Artigos)? Artigos(string citacao)
    {
        var match = SufixoDeArtigos().Match(citacao);
        if (!match.Success)
        {
            return null;
        }

        // "art. 5º, § 2º" cita só o art. 5: o que vem depois de "§", "inc." ou "par." é detalhe do mesmo artigo.
        var lista = Regex.Split(match.Groups["lista"].Value, @"§|\binc|\bpar[áa]grafo|\bal[íi]nea|\bc/c\b", RegexOptions.IgnoreCase)[0];
        var artigos = NumeroDeArtigo().Matches(lista.Replace("º", string.Empty).Replace("°", string.Empty))
            .Select(m => ExtratorReferencias.NormalizarArtigo(m.Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return artigos.Count == 0 ? null : (citacao[..match.Index].Trim(), artigos);
    }

    /// <summary>Caixa, acento, pontuação e espaços não podem gerar falso alarme.</summary>
    public static string Normalizar(string texto)
    {
        var chars = Bm25.SemAcento(texto.ToLowerInvariant()).Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();

        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
