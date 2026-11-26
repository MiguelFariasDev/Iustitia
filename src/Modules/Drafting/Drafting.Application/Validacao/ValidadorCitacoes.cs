using System.Globalization;
using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.Validacao;

public sealed record CitacaoAvaliada(
    string Texto, CitacaoStatus Status, string Detalhe, string? PontoId = null, string? FonteUrl = null, string? Urn = null);

public sealed record ValidacaoCitacoes(IReadOnlyList<CitacaoAvaliada> Itens, IReadOnlySet<string> RotulosCitados)
{
    /// <summary>
    /// Exportação final só depois de revisão humana quando há item pendente ou possivelmente inventado.
    /// (Revogada e conflito de versão exigem atenção, mas já vão sinalizados.)
    /// </summary>
    public bool ExportacaoBloqueada =>
        Itens.Any(i => i.Status is CitacaoStatus.PossivelAlucinacao or CitacaoStatus.PendenteDeVerificacao);

    public IEnumerable<CitacaoAvaliada> Problemas => Itens.Where(i => i.Status != CitacaoStatus.Validada);

    public bool Cita(RetrievedChunk chunk) => RotulosCitados.Contains(CitacaoParser.Normalizar(chunk.Rotulo));
}

/// <param name="TextoDoCaso">Fatos, pedidos e instruções da advogada.</param>
/// <param name="DataFatos">Data relevante: base da conferência de vigência.</param>
/// <param name="UrnsConhecidas">URNs citadas que existem no banco, mas não vieram no contexto.</param>
public sealed record ContextoValidacao(
    IReadOnlyList<RetrievedChunk> Chunks, string TextoDoCaso, DateOnly? DataFatos = null, IReadOnlySet<string>? UrnsConhecidas = null);

/// <summary>
/// Conferência DETERMINÍSTICA pós-geração contra o que de fato entrou no prompt. Aponta o que a advogada
/// precisa conferir; não prova que a minuta está certa (ex.: não julga se o artigo citado sustenta a tese).
/// </summary>
public static partial class ValidadorCitacoes
{
    private const int AspasMinChars = 60;
    private const int JanelaFrase = 220;

    public static ValidacaoCitacoes Validar(string texto, ContextoValidacao contexto)
    {
        var itens = new List<CitacaoAvaliada>();
        var citados = new HashSet<string>(StringComparer.Ordinal);
        var conflitos = ConflitosDeVersao(contexto.Chunks);

        foreach (var citacao in CitacaoParser.Extrair(texto))
        {
            AvaliarCitacao(citacao, texto, contexto, conflitos, citados, itens);
        }

        // A seção de fontes repete rótulos e a de revisão lista justamente o que falta: só o corpo afirma coisas.
        var corpo = Corpo(texto);
        // Tribunal antes: a explicação específica ("é de outro tribunal") prevalece sobre a genérica na deduplicação.
        AvaliarTribunais(corpo, contexto.Chunks, itens);
        AvaliarReferenciasSemFonte(corpo, contexto, itens);
        AvaliarAnos(corpo, contexto.Chunks, itens);
        AvaliarAspas(corpo, contexto, itens);
        AvaliarEmentas(corpo, contexto, itens);
        AvaliarUrns(texto, contexto, itens);
        AvaliarFatos(corpo, contexto, itens);
        AvaliarCitacaoAusente(corpo, citados, contexto.Chunks, itens);

        return new ValidacaoCitacoes(
            itens.GroupBy(i => (CitacaoParser.Normalizar(i.Texto), i.Status)).Select(g => g.First()).ToList(), citados);
    }

    /// <summary>URNs que a minuta cita e que NÃO estão nos trechos: candidatas à conferência no banco.</summary>
    public static IReadOnlyList<string> UrnsParaConferir(string texto, IReadOnlyList<RetrievedChunk> chunks)
    {
        var noContexto = UrnsDoContexto(chunks);

        return Urn().Matches(texto).Select(LimparUrn).Where(u => !noContexto.Contains(u))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ------------------------------------------------------------ citações entre colchetes

    private static void AvaliarCitacao(
        CitacaoEncontrada citacao, string texto, ContextoValidacao contexto, HashSet<string> conflitos,
        HashSet<string> citados, List<CitacaoAvaliada> itens)
    {
        var chave = CitacaoParser.Normalizar(citacao.Texto);
        var exato = contexto.Chunks.FirstOrDefault(c => CitacaoParser.Normalizar(c.Rotulo) == chave);
        if (exato is not null)
        {
            citados.Add(chave);
            itens.Add(AvaliarEncontrado(citacao, exato, texto, contexto, conflitos));

            return;
        }

        // "Código Civil, arts. 186 e 927" ou "Código Civil, art. 186, caput": confere artigo a artigo.
        if (CitacaoParser.Artigos(citacao.Texto) is { } composta)
        {
            var daNorma = contexto.Chunks.Where(c => NomeConfere(c, composta.Norma)).ToList();
            if (daNorma.Count > 0)
            {
                var encontrados = composta.Artigos
                    .Select(a => daNorma.FirstOrDefault(c =>
                        c.Meta.Norma?.Artigo is { } artigo && ExtratorReferencias.NormalizarArtigo(artigo) == a))
                    .ToList();
                if (encontrados.All(c => c is not null))
                {
                    foreach (var chunk in encontrados)
                    {
                        citados.Add(CitacaoParser.Normalizar(chunk!.Rotulo));
                    }

                    itens.Add(AvaliarEncontrado(citacao, encontrados[0]!, texto, contexto, conflitos));

                    return;
                }

                itens.Add(new CitacaoAvaliada(
                    citacao.Texto, CitacaoStatus.PendenteDeVerificacao,
                    "A norma consta no contexto, mas o dispositivo citado não foi recuperado: confira o texto oficial.",
                    null, daNorma[0].Meta.UrlOrigem, daNorma[0].Meta.Norma?.Urn));

                return;
            }
        }

        // Rótulo com complemento: "[STJ, Súmula nº 297, p. 3]" ou "[contrato.pdf]".
        var parcial = contexto.Chunks.FirstOrDefault(c =>
        {
            var rotulo = CitacaoParser.Normalizar(c.Rotulo);

            return chave.StartsWith(rotulo + " ", StringComparison.Ordinal);
        });
        if (parcial is not null)
        {
            citados.Add(CitacaoParser.Normalizar(parcial.Rotulo));
            itens.Add(parcial.Meta.Norma is { Colecao: Colecoes.Jurisprudencia }
                ? new CitacaoAvaliada(
                    citacao.Texto, CitacaoStatus.PendenteDeVerificacao,
                    "A decisão consta no contexto, mas com identificação diferente da citada: confira número e página.",
                    parcial.PointId, parcial.Meta.UrlOrigem, parcial.Meta.Norma.Urn)
                : AvaliarEncontrado(citacao, parcial, texto, contexto, conflitos));

            return;
        }

        itens.Add(AvaliarDesconhecido(citacao.Texto, contexto.Chunks));
    }

    private static CitacaoAvaliada AvaliarEncontrado(
        CitacaoEncontrada citacao, RetrievedChunk chunk, string texto, ContextoValidacao contexto, HashSet<string> conflitos)
    {
        CitacaoAvaliada Com(CitacaoStatus status, string detalhe) =>
            new(citacao.Texto, status, detalhe, chunk.PointId, chunk.Meta.UrlOrigem, chunk.Meta.Norma?.Urn);

        if (chunk.Meta.Caso is { } caso)
        {
            return caso.RevisaoPendente
                ? Com(CitacaoStatus.PendenteDeVerificacao, "Documento lido por OCR/transcrição ou com conteúdo suspeito, ainda não conferido por uma pessoa.")
                : Com(CitacaoStatus.Validada, "Documento do caso presente no contexto.");
        }

        if (chunk.Meta.Norma is not { } norma)
        {
            return Com(CitacaoStatus.PossivelAlucinacao, "Fonte sem identificação de origem.");
        }

        if (conflitos.Contains(ChaveNorma(norma)))
        {
            return Com(CitacaoStatus.ConflitoDeVersao, "Há mais de uma versão desta norma no acervo recuperado; confira qual redação vale.");
        }

        if (norma.ForaDeVigor())
        {
            var inicio = Math.Max(0, citacao.Posicao - JanelaFrase);
            var janela = texto.Substring(inicio, Math.Min(texto.Length - inicio, 2 * JanelaFrase));
            var sinalizada = janela.Contains("revogad", StringComparison.OrdinalIgnoreCase)
                || janela.Contains("cancelad", StringComparison.OrdinalIgnoreCase)
                || janela.Contains("vigente à época", StringComparison.OrdinalIgnoreCase);

            return sinalizada
                ? Com(CitacaoStatus.Validada, $"Fora de vigor ({norma.Situacao}) e citada como tal.")
                : Com(CitacaoStatus.NormaRevogada, $"A fonte consta como '{norma.Situacao}' e o texto não indica data nem contexto temporal.");
        }

        if (contexto.DataFatos is { } fatos && chunk.Meta.DataPublicacao is { } data && data > fatos
            && norma.Colecao == Colecoes.LeisFederais)
        {
            return Com(
                CitacaoStatus.ConflitoDeVersao,
                $"A norma é de {data:dd/MM/yyyy}, posterior aos fatos ({fatos:dd/MM/yyyy}): confira qual lei vigia na época.");
        }

        return Com(CitacaoStatus.Validada, $"Confere com {norma.ArquivoMarkdownPath} (texto: {norma.VersaoTexto}).");
    }

    private static CitacaoAvaliada AvaliarDesconhecido(string rotulo, IReadOnlyList<RetrievedChunk> chunks)
    {
        var virgula = rotulo.LastIndexOf(',');
        var nome = virgula > 0 ? rotulo[..virgula] : rotulo;

        var norma = chunks.FirstOrDefault(c => c.Meta.Norma is not null && NomeConfere(c, nome));
        if (norma is not null)
        {
            return new CitacaoAvaliada(
                rotulo, CitacaoStatus.PendenteDeVerificacao,
                "A norma consta no contexto, mas o dispositivo citado não foi recuperado: confira o texto oficial.",
                null, norma.Meta.UrlOrigem, norma.Meta.Norma?.Urn);
        }

        var documento = chunks.FirstOrDefault(c => c.Meta.Caso is not null
            && (CitacaoParser.Normalizar(c.Meta.NomeArquivo) == CitacaoParser.Normalizar(nome)
                || CitacaoParser.Normalizar(c.Meta.NomeArquivo) == CitacaoParser.Normalizar(rotulo)));

        return documento is not null
            ? new CitacaoAvaliada(
                rotulo, CitacaoStatus.PendenteDeVerificacao, "O documento consta no contexto, mas não esta página ou trecho.")
            : new CitacaoAvaliada(
                rotulo, CitacaoStatus.PossivelAlucinacao, "Fonte que não consta no contexto recuperado: não confie sem conferir.");
    }

    /// <summary>O nome escrito é o apelido, a sigla ou o título da norma do trecho?</summary>
    private static bool NomeConfere(RetrievedChunk chunk, string nome)
    {
        if (chunk.Meta.Norma is not { } norma)
        {
            return false;
        }

        var limpo = CitacaoParser.Normalizar(nome);
        var conhecida = NormasConhecidas.Identificar(norma.Tipo, norma.Numero, chunk.Meta.DataPublicacao?.Year, norma.Colecao);
        if (conhecida is not null
            && (CitacaoParser.Normalizar(conhecida.Apelido) == limpo || conhecida.Siglas.Any(s => CitacaoParser.Normalizar(s) == limpo)))
        {
            return true;
        }

        return CitacaoParser.Normalizar(norma.Titulo) == limpo
            || (norma.Numero is { } numero && norma.Colecao == Colecoes.LeisFederais
                && ExtratorReferencias.Extrair(nome).Any(r => r.Numero == numero.Replace(".", string.Empty)));
    }

    // ------------------------------------------------------------ referências fora de colchetes

    private static void AvaliarReferenciasSemFonte(string corpo, ContextoValidacao contexto, List<CitacaoAvaliada> itens)
    {
        var semCitacoes = SemColchetes(corpo);
        var doCaso = ExtratorReferencias.Extrair(contexto.TextoDoCaso, 200).ToHashSet();

        foreach (var referencia in ExtratorReferencias.Extrair(semCitacoes, 200))
        {
            var noContexto = contexto.Chunks.Any(c => c.Meta.Norma is { } norma
                && RerankerService.MesmaNorma(norma, c.Meta, referencia)
                && (referencia.Artigo is null
                    || (norma.Artigo is { } artigo && ExtratorReferencias.NormalizarArtigo(artigo) == referencia.Artigo)));
            if (noContexto)
            {
                continue;
            }

            var descricao = Descrever(referencia);
            if (doCaso.Contains(referencia))
            {
                itens.Add(new CitacaoAvaliada(
                    descricao, CitacaoStatus.PendenteDeVerificacao,
                    "Mencionado pela advogada, mas não recuperado do acervo: confira o texto oficial."));
                continue;
            }

            // A norma veio, o artigo não: pode existir. Súmula, tema ou lei que não veio: possivelmente inventado.
            var normaVeio = contexto.Chunks.Any(c => c.Meta.Norma is { } n && RerankerService.MesmaNorma(n, c.Meta, referencia));
            itens.Add(normaVeio && referencia.Tipo == TipoReferencia.Norma
                ? new CitacaoAvaliada(descricao, CitacaoStatus.PendenteDeVerificacao, "Dispositivo mencionado sem respaldo nos trechos recuperados.")
                : new CitacaoAvaliada(
                    descricao, CitacaoStatus.PossivelAlucinacao,
                    "Norma, súmula ou tema que não consta em nenhum trecho recuperado nem nos dados do caso."));
        }

        var suporte = CitacaoParser.Normalizar(contexto.TextoDoCaso + "\n" + string.Join('\n', contexto.Chunks.Select(c => c.Texto)));
        foreach (Match match in Precedente().Matches(semCitacoes))
        {
            if (!suporte.Contains(CitacaoParser.Normalizar(match.Value), StringComparison.Ordinal))
            {
                itens.Add(new CitacaoAvaliada(
                    match.Value.Trim(), CitacaoStatus.PossivelAlucinacao,
                    "Precedente que não consta em nenhum trecho recuperado nem nos dados do caso."));
            }
        }
    }

    private static void AvaliarTribunais(string corpo, IReadOnlyList<RetrievedChunk> chunks, List<CitacaoAvaliada> itens)
    {
        foreach (var referencia in ExtratorReferencias.Extrair(corpo, 200).Where(r => r.Tipo == TipoReferencia.Sumula && r.Tribunal is not null))
        {
            var mesmoNumero = chunks
                .Where(c => c.Meta.Norma is { } n && n.Numero == referencia.Numero && n.Tipo.Contains("úmula", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (mesmoNumero.Count > 0
                && mesmoNumero.All(c => !string.Equals(c.Meta.Tribunal, referencia.Tribunal, StringComparison.OrdinalIgnoreCase)))
            {
                itens.Add(new CitacaoAvaliada(
                    Descrever(referencia), CitacaoStatus.PossivelAlucinacao,
                    $"A súmula nº {referencia.Numero} recuperada é de outro tribunal ({mesmoNumero[0].Meta.Tribunal ?? "não informado"})."));
            }
        }
    }

    /// <summary>"Lei nº 10.406/2003": o número bate com norma do contexto, o ano não.</summary>
    private static void AvaliarAnos(string corpo, IReadOnlyList<RetrievedChunk> chunks, List<CitacaoAvaliada> itens)
    {
        foreach (Match match in LeiComAno().Matches(corpo))
        {
            var numero = new string(match.Groups["num"].Value.Where(char.IsDigit).ToArray());
            var ano = int.Parse(match.Groups["ano"].Value, CultureInfo.InvariantCulture);
            if (ano < 100)
            {
                ano += ano <= 30 ? 2000 : 1900;
            }

            var chunk = chunks.FirstOrDefault(c =>
                c.Meta.Norma is { Colecao: Colecoes.LeisFederais } n
                && n.Numero?.Replace(".", string.Empty) == numero
                && c.Meta.DataPublicacao is not null);
            if (chunk is not null && chunk.Meta.DataPublicacao!.Value.Year != ano)
            {
                itens.Add(new CitacaoAvaliada(
                    match.Value.Trim(), CitacaoStatus.PossivelAlucinacao,
                    $"O ano não confere: a norma recuperada é de {chunk.Meta.DataPublicacao.Value.Year}.",
                    chunk.PointId, chunk.Meta.UrlOrigem, chunk.Meta.Norma!.Urn));
            }
        }
    }

    private static void AvaliarAspas(string corpo, ContextoValidacao contexto, List<CitacaoAvaliada> itens)
    {
        var suporte = Suporte(contexto);
        foreach (Match match in Aspas().Matches(corpo))
        {
            var citado = match.Groups[1].Value;
            if (citado.Length >= AspasMinChars && !TrechoNoSuporte(citado, suporte))
            {
                itens.Add(new CitacaoAvaliada(
                    Resumir(citado), CitacaoStatus.PossivelAlucinacao,
                    "Trecho entre aspas que não aparece nas fontes recuperadas nem nos dados do caso."));
            }
        }
    }

    private static void AvaliarEmentas(string corpo, ContextoValidacao contexto, List<CitacaoAvaliada> itens)
    {
        var suporte = Suporte(contexto);
        foreach (Match match in Ementa().Matches(corpo))
        {
            var ementa = match.Groups[1].Value.Trim(' ', '"', '“', '”');
            if (ementa.Length >= 30 && !TrechoNoSuporte(ementa, suporte))
            {
                itens.Add(new CitacaoAvaliada(
                    Resumir("EMENTA: " + ementa), CitacaoStatus.PossivelAlucinacao, "Ementa que não consta em nenhum documento recuperado."));
            }
        }
    }

    private static void AvaliarUrns(string texto, ContextoValidacao contexto, List<CitacaoAvaliada> itens)
    {
        foreach (var urn in UrnsParaConferir(texto, contexto.Chunks))
        {
            itens.Add(contexto.UrnsConhecidas?.Contains(urn) == true
                ? new CitacaoAvaliada(urn, CitacaoStatus.PendenteDeVerificacao, "A URN existe no acervo, mas a norma não foi recuperada para esta minuta.", Urn: urn)
                : new CitacaoAvaliada(urn, CitacaoStatus.PossivelAlucinacao, "URN que não consta no contexto nem no acervo."));
        }
    }

    // ------------------------------------------------------------ fatos

    /// <summary>
    /// Fato sem documento de suporte: valor, data, processo ou CPF/CNPJ que a minuta afirma e que não está nos
    /// dados da advogada nem nos trechos. São os fatos que se consegue conferir sem interpretar o texto.
    /// </summary>
    private static void AvaliarFatos(string corpo, ContextoValidacao contexto, List<CitacaoAvaliada> itens)
    {
        var suporte = contexto.TextoDoCaso + "\n" + string.Join('\n', contexto.Chunks.Select(c => c.Texto + "\n" + c.Rotulo));
        var valores = Valor().Matches(suporte).Select(m => NormalizarValor(m.Value)).ToHashSet(StringComparer.Ordinal);
        var datas = DatasDe(suporte);
        foreach (var chunk in contexto.Chunks)
        {
            if (chunk.Meta.DataPublicacao is { } data)
            {
                datas.Add(data);
            }
        }

        var digitos = Identificador().Matches(suporte).Select(m => SoDigitos(m.Value)).ToHashSet(StringComparer.Ordinal);

        foreach (Match match in Valor().Matches(corpo))
        {
            if (!valores.Contains(NormalizarValor(match.Value)))
            {
                itens.Add(SemSuporte(match.Value.Trim(), "Valor"));
            }
        }

        foreach (var (textoData, data) in DatasComTexto(corpo))
        {
            if (!datas.Contains(data))
            {
                itens.Add(SemSuporte(textoData, "Data"));
            }
        }

        foreach (Match match in Identificador().Matches(corpo))
        {
            if (!digitos.Contains(SoDigitos(match.Value)))
            {
                itens.Add(SemSuporte(match.Value, "Número de processo ou documento"));
            }
        }
    }

    private static CitacaoAvaliada SemSuporte(string texto, string tipo) =>
        new(texto, CitacaoStatus.PendenteDeVerificacao, $"{tipo} sem suporte nos fatos informados nem nos documentos do caso.");

    /// <summary>A seção DO DIREITO que não cita nada do contexto é fundamento sem fonte.</summary>
    private static void AvaliarCitacaoAusente(
        string corpo, HashSet<string> citados, IReadOnlyList<RetrievedChunk> chunks, List<CitacaoAvaliada> itens)
    {
        if (!chunks.Any(c => c.Meta.Norma is not null) || !corpo.Contains("DO DIREITO", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var citaNorma = chunks.Any(c => c.Meta.Norma is not null && citados.Contains(CitacaoParser.Normalizar(c.Rotulo)));
        if (!citaNorma && !corpo.Contains("FUNDAMENTO PENDENTE DE VERIFICAÇÃO", StringComparison.OrdinalIgnoreCase))
        {
            itens.Add(new CitacaoAvaliada(
                "DO DIREITO", CitacaoStatus.PendenteDeVerificacao, "A fundamentação jurídica não cita nenhuma das fontes recuperadas."));
        }
    }

    // ------------------------------------------------------------ apoio

    /// <summary>Tudo antes de FONTES UTILIZADAS (ou do último bloco de revisão).</summary>
    internal static string Corpo(string texto)
    {
        var corte = new[] { "FONTES UTILIZADAS", "PONTOS PARA REVISÃO HUMANA" }
            .Select(secao => texto.LastIndexOf(secao, StringComparison.OrdinalIgnoreCase))
            .Where(i => i > 0)
            .DefaultIfEmpty(texto.Length)
            .Min();

        return texto[..corte];
    }

    private static HashSet<string> ConflitosDeVersao(IReadOnlyList<RetrievedChunk> chunks) =>
        chunks.Where(c => c.Meta.Norma is not null)
            .Select(c => c.Meta.Norma!)
            .GroupBy(ChaveNorma)
            .Where(g => g.Select(n => n.NormaId).Distinct().Count() > 1 || g.Select(n => n.VersaoNumero).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

    private static string ChaveNorma(NormaRef norma) =>
        $"{norma.Tipo.ToLowerInvariant()}|{norma.Numero}|{CitacaoParser.Normalizar(norma.Titulo)}";

    private static string Suporte(ContextoValidacao contexto) =>
        CitacaoParser.Normalizar(contexto.TextoDoCaso + "\n" + string.Join('\n', contexto.Chunks.Select(c => c.Texto)));

    /// <summary>Aceita reticências de supressão: "(...)" e "[...]" separam pedaços que precisam constar, cada um.</summary>
    private static bool TrechoNoSuporte(string trecho, string suporteNormalizado) =>
        Regex.Split(trecho, @"[\(\[]\s*(?:\.{3}|…)\s*[\)\]]|\.{3}|…")
            .Select(CitacaoParser.Normalizar)
            .Where(p => p.Length >= 15)
            .All(p => suporteNormalizado.Contains(p, StringComparison.Ordinal));

    private static string SemColchetes(string texto) => Regex.Replace(texto, @"\[[^\[\]\n]{3,300}\]", " ");

    private static HashSet<string> UrnsDoContexto(IReadOnlyList<RetrievedChunk> chunks) =>
        chunks.Select(c => c.Meta.Norma?.Urn).Where(u => u is not null).Select(u => u!).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string LimparUrn(Match match) => match.Value.TrimEnd('.', ',', ';', ')');

    private static string Descrever(ReferenciaLegal r)
    {
        var conhecida = r.Constituicao ? "Constituição Federal" : NormasConhecidas.Todas.FirstOrDefault(n => n.Numero == r.Numero)?.Apelido;

        return r.Tipo switch
        {
            TipoReferencia.Sumula => $"Súmula {(r.Vinculante ? "vinculante " : string.Empty)}nº {r.Numero}{(r.Tribunal is null ? string.Empty : $" do {r.Tribunal}")}",
            TipoReferencia.Tema => $"Tema nº {r.Numero}{(r.Tribunal is null ? string.Empty : $" do {r.Tribunal}")}",
            _ when r.Artigo is not null => $"art. {r.Artigo} — {conhecida ?? $"norma nº {r.Numero}"}",
            _ => conhecida ?? $"norma nº {r.Numero}",
        };
    }

    private static string Resumir(string texto) => texto.Length <= 120 ? texto : texto[..120] + "…";

    private static string SoDigitos(string texto) => new(texto.Where(char.IsDigit).ToArray());

    /// <summary>"R$ 1.000", "R$ 1.000,00" e "R$1000,00" são o mesmo valor.</summary>
    internal static string NormalizarValor(string valor)
    {
        var limpo = new string(valor.Where(c => char.IsDigit(c) || c == ',').ToArray());
        var virgula = limpo.LastIndexOf(',');
        var inteiro = virgula >= 0 ? limpo[..virgula] : limpo;
        var centavos = virgula >= 0 ? limpo[(virgula + 1)..] : "00";

        return $"{inteiro.Replace(",", string.Empty).TrimStart('0')},{centavos.PadRight(2, '0')}";
    }

    internal static HashSet<DateOnly> DatasDe(string texto) => DatasComTexto(texto).Select(d => d.Data).ToHashSet();

    private static IEnumerable<(string Texto, DateOnly Data)> DatasComTexto(string texto)
    {
        foreach (Match match in DataNumerica().Matches(texto))
        {
            if (DateOnly.TryParseExact(
                match.Value.Replace('-', '/').Replace('.', '/'), ["d/M/yyyy", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            {
                yield return (match.Value, data);
            }
        }

        foreach (Match match in DataIso().Matches(texto))
        {
            if (DateOnly.TryParseExact(match.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            {
                yield return (match.Value, data);
            }
        }

        foreach (Match match in DataExtenso().Matches(texto))
        {
            var mes = Array.FindIndex(Meses, m => Bm25.SemAcento(match.Groups["m"].Value.ToLowerInvariant()) == m) + 1;
            if (mes > 0
                && int.TryParse(match.Groups["d"].Value, out var dia)
                && int.TryParse(match.Groups["a"].Value, out var ano)
                && dia >= 1 && ano is >= 1500 and <= 2200 && dia <= DateTime.DaysInMonth(ano, mes))
            {
                yield return (match.Value, new DateOnly(ano, mes, dia));
            }
        }
    }

    private static readonly string[] Meses =
        ["janeiro", "fevereiro", "marco", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    [GeneratedRegex(@"\b(?:AREsp|REsp|EREsp|AgRg|AgInt|RE|ARE|ADI|ADC|ADPF|ADO|HC|RHC|RMS|IRDR|IAC)\s*(?:n[ºo°.]*\s*)?\d[\d\.\-/]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Precedente();

    [GeneratedRegex(@"\b(?:lei(?:\s+complementar|\s+federal)?|decreto(?:-lei)?)\s*(?:n[ºo°.]*\s*)?(?<num>\d{1,3}(?:\.\d{3})+|\d{3,6})\s*(?:/|,?\s+de\s+(?:\d{1,2}[ºo°]?\s+de\s+\p{L}+\s+de\s+)?)(?<ano>\d{4}|\d{2})\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LeiComAno();

    [GeneratedRegex("[“\"]([^”\"\\n]{20,1200})[”\"]", RegexOptions.CultureInvariant)]
    private static partial Regex Aspas();

    [GeneratedRegex(@"^\s*EMENTA\s*[:\-–—]\s*(.{30,1500})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Ementa();

    [GeneratedRegex(@"urn:lex:[a-z0-9:;.,_\-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Urn();

    [GeneratedRegex(@"R\$\s*\d[\d\.]*(?:,\d{1,2})?", RegexOptions.CultureInvariant)]
    private static partial Regex Valor();

    [GeneratedRegex(@"(?<![\d./\-])\d{1,2}([/.\-])\d{1,2}\1(?:1[89]|2[01])\d{2}(?![\d]|[./\-]\d)", RegexOptions.CultureInvariant)]
    private static partial Regex DataNumerica();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex DataIso();

    [GeneratedRegex(@"\b(?<d>\d{1,2})[ºo°]?\s+de\s+(?<m>janeiro|fevereiro|mar[çc]o|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro)\s+de\s+(?<a>\d{4})\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DataExtenso();

    [GeneratedRegex(@"\b\d{7}-?\d{2}\.?\d{4}\.?\d\.?\d{2}\.?\d{4}\b|\b\d{3}\.\d{3}\.\d{3}-\d{2}\b|\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Identificador();
}
