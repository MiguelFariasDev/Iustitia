using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;

namespace Advocacia.Modules.Drafting.Application.Normas;

public enum CitacaoStatus
{
    Validada,
    PendenteDeVerificacao,
    PossivelAlucinacao,
    ConflitoDeVersao,
    NormaRevogada,
}

public sealed record CitacaoAvaliada(string Texto, CitacaoStatus Status, string Detalhe);

public sealed record ValidacaoCitacoes(IReadOnlyList<CitacaoAvaliada> Itens)
{
    /// <summary>
    /// Exportação final só depois de revisão humana quando há citação pendente ou possivelmente inventada.
    /// (Revogada e conflito de versão exigem atenção, mas o advogado já os vê sinalizados no texto.)
    /// </summary>
    public bool ExportacaoBloqueada =>
        Itens.Any(i => i.Status is CitacaoStatus.PossivelAlucinacao or CitacaoStatus.PendenteDeVerificacao);

    public IEnumerable<CitacaoAvaliada> Problemas => Itens.Where(i => i.Status != CitacaoStatus.Validada);
}

/// <summary>
/// Validação DETERMINÍSTICA pós-geração das citações da minuta contra os trechos que de fato entraram no
/// prompt. Aponta o que o advogado precisa conferir; não prova que a minuta está certa.
/// Não cobre: coerência de datas e conferência semântica de ementas (só texto entre aspas e URNs).
/// </summary>
public static partial class NormaCitationValidator
{
    public const string MarcadorBloqueio = "EXPORTAÇÃO BLOQUEADA";

    private const int AspasMinChars = 60;
    private const int JanelaFrase = 220;

    public static ValidacaoCitacoes Validar(
        string textoGerado, IReadOnlyList<RetrievedChunk> chunks, string textoDoCaso, IReadOnlySet<string>? urnsConhecidas = null)
    {
        var itens = new List<CitacaoAvaliada>();
        var porRotulo = chunks.GroupBy(c => Normalizar(c.Rotulo)).ToDictionary(g => g.Key, g => g.First());
        var normasNoContexto = chunks.Where(c => c.Meta.Norma is not null).Select(c => c.Meta.Norma!).ToList();
        var versoesEmConflito = ConflitosDeVersao(normasNoContexto);

        foreach (Match match in Colchetes().Matches(textoGerado))
        {
            var rotulo = match.Groups[1].Value.Trim();
            var chave = Normalizar(rotulo);

            if (porRotulo.TryGetValue(chave, out var chunk))
            {
                itens.Add(AvaliarEncontrado(rotulo, chunk, textoGerado, match.Index, versoesEmConflito));
                continue;
            }

            itens.Add(AvaliarDesconhecido(rotulo, chunks));
        }

        AvaliarTribunais(textoGerado, chunks, itens);
        AvaliarReferenciasSemFonte(textoGerado, chunks, textoDoCaso, itens);
        AvaliarAspas(textoGerado, chunks, textoDoCaso, itens);
        AvaliarUrns(textoGerado, chunks, urnsConhecidas, itens);

        return new ValidacaoCitacoes(itens
            .GroupBy(i => (Normalizar(i.Texto), i.Status))
            .Select(g => g.First())
            .ToList());
    }

    private static CitacaoAvaliada AvaliarEncontrado(
        string rotulo, RetrievedChunk chunk, string texto, int posicao, HashSet<string> conflitos)
    {
        if (chunk.Meta.Norma is not { } norma)
        {
            return new CitacaoAvaliada(rotulo, CitacaoStatus.Validada, "Fonte presente no contexto recuperado.");
        }

        if (conflitos.Contains(ChaveNorma(norma)))
        {
            return new CitacaoAvaliada(
                rotulo, CitacaoStatus.ConflitoDeVersao,
                "Há mais de uma versão/arquivo desta norma no acervo recuperado; confira qual redação vale.");
        }

        if (norma.Revogada || norma.Situacao.Contains("revogad", StringComparison.OrdinalIgnoreCase))
        {
            var inicio = Math.Max(0, posicao - JanelaFrase);
            var janela = texto.Substring(inicio, Math.Min(texto.Length - inicio, (posicao - inicio) + JanelaFrase));

            return janela.Contains("revogad", StringComparison.OrdinalIgnoreCase)
                ? new CitacaoAvaliada(rotulo, CitacaoStatus.Validada, "Norma revogada, citada como tal.")
                : new CitacaoAvaliada(
                    rotulo, CitacaoStatus.NormaRevogada,
                    "A fonte consta como revogada e o texto não indica data nem contexto temporal.");
        }

        return new CitacaoAvaliada(rotulo, CitacaoStatus.Validada, $"Confere com {norma.ArquivoMarkdownPath} (texto: {norma.VersaoTexto}).");
    }

    private static CitacaoAvaliada AvaliarDesconhecido(string rotulo, IReadOnlyList<RetrievedChunk> chunks)
    {
        var virgula = rotulo.LastIndexOf(',');
        var nome = virgula > 0 ? Normalizar(rotulo[..virgula]) : Normalizar(rotulo);

        var mesmaNorma = chunks.Any(c => c.Meta.Norma is { } n && Normalizar(n.Titulo) == nome);
        if (mesmaNorma)
        {
            return new CitacaoAvaliada(
                rotulo, CitacaoStatus.PendenteDeVerificacao,
                "A norma consta no contexto, mas o dispositivo citado não foi recuperado: confira o texto oficial.");
        }

        var mesmoArquivo = chunks.Any(c => c.Meta.Norma is null && Normalizar(c.Meta.NomeArquivo) == nome);
        if (mesmoArquivo)
        {
            return new CitacaoAvaliada(
                rotulo, CitacaoStatus.PendenteDeVerificacao, "O documento consta no contexto, mas não esta página/trecho.");
        }

        return new CitacaoAvaliada(
            rotulo, CitacaoStatus.PossivelAlucinacao, "Fonte que não consta no contexto recuperado: não confie sem conferir.");
    }

    private static void AvaliarTribunais(string texto, IReadOnlyList<RetrievedChunk> chunks, List<CitacaoAvaliada> itens)
    {
        foreach (Match match in SumulaComTribunal().Matches(texto))
        {
            var numero = match.Groups["n"].Value;
            var tribunal = match.Groups["t"].Value.ToUpperInvariant();
            var candidatas = chunks
                .Select(c => c.Meta)
                .Where(m => m.Norma is { } n && n.Tipo.Contains("úmula", StringComparison.OrdinalIgnoreCase) && n.Numero == numero)
                .ToList();

            if (candidatas.Count > 0 && candidatas.All(m => !string.Equals(m.Tribunal, tribunal, StringComparison.OrdinalIgnoreCase)))
            {
                itens.Add(new CitacaoAvaliada(
                    match.Value.Trim(), CitacaoStatus.PossivelAlucinacao,
                    $"A súmula nº {numero} recuperada é de outro tribunal ({candidatas[0].Tribunal ?? "não informado"})."));
            }
        }
    }

    private static void AvaliarReferenciasSemFonte(
        string texto, IReadOnlyList<RetrievedChunk> chunks, string caso, List<CitacaoAvaliada> itens)
    {
        var relatorio = CitationVerifier.Verify(texto, chunks, caso);
        foreach (var referencia in relatorio.UnsupportedReferences)
        {
            var jurisprudencia = !referencia.StartsWith("art", StringComparison.OrdinalIgnoreCase);
            itens.Add(new CitacaoAvaliada(
                referencia,
                jurisprudencia ? CitacaoStatus.PossivelAlucinacao : CitacaoStatus.PendenteDeVerificacao,
                jurisprudencia
                    ? "Súmula/precedente que não consta em nenhum trecho recuperado nem nos dados do caso."
                    : "Dispositivo mencionado sem respaldo nos trechos recuperados."));
        }
    }

    private static void AvaliarAspas(string texto, IReadOnlyList<RetrievedChunk> chunks, string caso, List<CitacaoAvaliada> itens)
    {
        var suporte = Normalizar(caso + "\n" + string.Join('\n', chunks.Select(c => c.Texto)));
        foreach (Match match in Aspas().Matches(texto))
        {
            var citado = match.Groups[1].Value;
            if (citado.Length >= AspasMinChars && !suporte.Contains(Normalizar(citado), StringComparison.Ordinal))
            {
                itens.Add(new CitacaoAvaliada(
                    Resumir(citado), CitacaoStatus.PossivelAlucinacao,
                    "Trecho entre aspas que não aparece nas fontes recuperadas nem nos dados do caso."));
            }
        }
    }

    private static void AvaliarUrns(
        string texto, IReadOnlyList<RetrievedChunk> chunks, IReadOnlySet<string>? conhecidas, List<CitacaoAvaliada> itens)
    {
        var noContexto = chunks.Select(c => c.Meta.Norma?.Urn).Where(u => u is not null).Select(u => u!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Urn().Matches(texto))
        {
            var urn = match.Value.TrimEnd('.', ',', ';', ')');
            if (noContexto.Contains(urn))
            {
                continue;
            }

            itens.Add(conhecidas?.Contains(urn) == true
                ? new CitacaoAvaliada(urn, CitacaoStatus.PendenteDeVerificacao, "A URN existe no acervo, mas a norma não foi recuperada para esta minuta.")
                : new CitacaoAvaliada(urn, CitacaoStatus.PossivelAlucinacao, "URN que não consta no contexto nem no acervo."));
        }
    }

    /// <summary>URNs que a minuta cita e que NÃO estão nos trechos: candidatas à conferência no banco.</summary>
    public static IReadOnlyList<string> UrnsParaConferir(string texto, IReadOnlyList<RetrievedChunk> chunks)
    {
        var noContexto = chunks.Select(c => c.Meta.Norma?.Urn).Where(u => u is not null).Select(u => u!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Urn().Matches(texto).Select(m => m.Value.TrimEnd('.', ',', ';', ')')).Where(u => !noContexto.Contains(u)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Normas (mesmo tipo/número/título) presentes com mais de um texto distinto no contexto.</summary>
    private static HashSet<string> ConflitosDeVersao(List<NormaRef> normas) =>
        normas.GroupBy(ChaveNorma)
            .Where(g => g.Select(n => n.NormaId).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

    private static string ChaveNorma(NormaRef norma) =>
        $"{norma.Tipo.ToLowerInvariant()}|{norma.Numero}|{Normalizar(norma.Titulo)}";

    private static string Resumir(string texto) => texto.Length <= 80 ? texto : texto[..80] + "…";

    private static string Normalizar(string texto)
    {
        var chars = texto.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray();

        return Regex.Replace(new string(chars), @"\s+", " ").Trim();
    }

    [GeneratedRegex(@"\[([^\[\]\n]{1,300}?,\s*(?:p\.\s*[^\[\]\n]{1,20}?|art\.\s*[^\[\]\n]{1,20}?|texto))\]", RegexOptions.CultureInvariant)]
    private static partial Regex Colchetes();

    [GeneratedRegex(@"s[úu]mula\s+(?:vinculante\s+)?(?:n[ºo°.]?\s*)?(?<n>\d+)\s*(?:do|da|de)?\s*(?<t>STF|STJ|TST|TSE|STM|TNU|TCU)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SumulaComTribunal();

    [GeneratedRegex("[“\"]([^”\"\\n]{20,1200})[”\"]", RegexOptions.CultureInvariant)]
    private static partial Regex Aspas();

    [GeneratedRegex(@"urn:lex:[a-z0-9:;.,_\-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Urn();
}

/// <summary>Variante assíncrona: confere no banco as URNs citadas que não vieram no contexto.</summary>
public sealed class NormaCitationService(INormaSearchIndex index)
{
    public async Task<ValidacaoCitacoes> ValidarAsync(
        string textoGerado, IReadOnlyList<RetrievedChunk> chunks, string textoDoCaso, CancellationToken cancellationToken = default)
    {
        var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var urn in NormaCitationValidator.UrnsParaConferir(textoGerado, chunks).Take(20))
        {
            if (await index.UrnExisteAsync(urn, cancellationToken))
            {
                existentes.Add(urn);
            }
        }

        return NormaCitationValidator.Validar(textoGerado, chunks, textoDoCaso, existentes);
    }
}
