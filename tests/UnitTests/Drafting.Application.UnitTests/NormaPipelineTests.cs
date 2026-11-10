using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Normas;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

// ------------------------------------------------------------------ fakes

internal sealed class MemNormaStore : INormaStore, INormaSearchIndex
{
    public List<NormaJuridica> Normas { get; } = [];

    public List<NormaVersao> Versoes { get; } = [];

    public List<NormaChunk> Chunks { get; } = [];

    public List<IngestaoJob> Jobs { get; } = [];

    public Task<IReadOnlyList<NormaEstado>> ListarEstadoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NormaEstado>>(Normas.Select(n => new NormaEstado(n.Id, n.RepoPath, n.HashSha256, n.Status, n.Colecao)).ToList());

    public Task<NormaJuridica?> ObterPorCaminhoAsync(string repoPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(Normas.FirstOrDefault(n => n.RepoPath == repoPath));

    public Task<NormaJuridica?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Normas.FirstOrDefault(n => n.Id == id));

    public Task<NormaVersao?> ObterVersaoAsync(Guid normaId, int numero, CancellationToken cancellationToken = default) =>
        Task.FromResult(Versoes.FirstOrDefault(v => v.NormaJuridicaId == normaId && v.Numero == numero));

    public void Adicionar(NormaJuridica norma) => Normas.Add(norma);

    public void Adicionar(NormaVersao versao) => Versoes.Add(versao);

    public void Adicionar(IngestaoJob job) => Jobs.Add(job);

    public void Adicionar(IEnumerable<NormaChunk> chunks) => Chunks.AddRange(chunks);

    public Task RemoverChunksDaVersaoAsync(Guid normaId, int versao, CancellationToken cancellationToken = default)
    {
        Chunks.RemoveAll(c => c.NormaJuridicaId == normaId && c.Versao == versao);

        return Task.CompletedTask;
    }

    public Task ConcluirIndexacaoAsync(Guid normaId, int versao, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in Chunks.Where(c => c.NormaJuridicaId == normaId && c.Versao == versao))
        {
            chunk.MarcarIndexado();
        }

        Chunks.RemoveAll(c => c.NormaJuridicaId == normaId && c.Versao != versao);

        return Task.CompletedTask;
    }

    public Task<IngestaoJob?> ObterJobAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.FirstOrDefault(j => j.Id == id));

    public Task<IngestaoJob?> ObterJobPendenteAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.FirstOrDefault(j => j.Status == IngestaoJobStatus.Pendente));

    public Task<IReadOnlyList<IngestaoJob>> ListarJobsAsync(int limite, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IngestaoJob>>(Jobs.OrderByDescending(j => j.IniciadoEm).Take(limite).ToList());

    public Task<DateTimeOffset?> UltimaSincronizacaoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.Where(j => j.Status != IngestaoJobStatus.Pendente).Select(j => (DateTimeOffset?)j.IniciadoEm).Max());

    public Task<int> SalvarAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<IReadOnlyList<VectorHit>> BuscarPalavrasChaveAsync(
        NormaFiltro filtro, string consulta, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VectorHit>>([]);

    public Task<bool> UrnExisteAsync(string urn, CancellationToken cancellationToken = default) =>
        Task.FromResult(Normas.Any(n => n.Urn == urn));
}

internal sealed class MemObjectStorage : IObjectStorage
{
    public Dictionary<string, byte[]> Objects { get; } = [];

    public List<(string From, string To)> Copies { get; } = [];

    public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        Objects[key] = content;

        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Objects.TryGetValue(key, out var value) ? value : null);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(Objects.ContainsKey(key));

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        Copies.Add((sourceKey, destinationKey));
        if (Objects.TryGetValue(sourceKey, out var value))
        {
            Objects[destinationKey] = value;
        }

        return Task.CompletedTask;
    }
}

internal sealed class StubEmbeddings(int failTimes = 0) : IEmbeddingService
{
    private int _failures = failTimes;

    public int Calls { get; private set; }

    public int Inputs { get; private set; }

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (_failures > 0)
        {
            _failures--;

            throw new LlmServiceException("GPU fora do ar");
        }

        Inputs += inputs.Count;

        return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new float[DocumentIndexer.EmbeddingDimensions]).ToList());
    }
}

internal sealed class MemNormaVectorStore : INormaVectorStore
{
    public List<(string Colecao, NormaVectorPoint Point)> Upserts { get; } = [];

    public List<(string Colecao, Guid NormaId, int? Manter)> Deletes { get; } = [];

    public IReadOnlyList<VectorHit> Hits { get; set; } = [];

    public NormaFiltro? LastFiltro { get; private set; }

    public Task EnsureCollectionsAsync(int dimensions, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpsertAsync(string colecao, IReadOnlyList<NormaVectorPoint> points, CancellationToken cancellationToken = default)
    {
        Upserts.AddRange(points.Select(p => (colecao, p)));

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string colecao, Guid normaId, int? manterVersao, CancellationToken cancellationToken = default)
    {
        Deletes.Add((colecao, normaId, manterVersao));
        if (manterVersao is null)
        {
            Upserts.RemoveAll(u => u.Colecao == colecao && u.Point.Payload.NormaJuridicaId == normaId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorHit>> SearchAsync(NormaFiltro filtro, float[] vetor, int limit, CancellationToken cancellationToken = default)
    {
        LastFiltro = filtro;

        return Task.FromResult(Hits);
    }

    public Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

internal sealed class FakeGpuOrchestrator : IGpuOrchestrator
{
    public int Garantidas { get; private set; }

    public bool Falhar { get; set; }

    public Task<Result> GarantirProntaAsync(string motivo, Func<CancellationToken, Task>? aoIniciar, CancellationToken cancellationToken = default)
    {
        Garantidas++;

        return Task.FromResult(Falhar
            ? Result.Failure(Advocacia.BuildingBlocks.Domain.Errors.ErrorFactory.From(Advocacia.BuildingBlocks.Domain.Errors.ErrorCode.DRAFTING_GPU_UNAVAILABLE))
            : Result.Success());
    }

    public Task<bool> PararSeOciosaAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal static class NormaSamples
{
    public static VectorHit Hit(
        string id, string titulo, string tipo = "Lei Ordinária", string? numero = "10406", string? artigo = "186",
        string texto = "Art. 186. Aquele que, por ação ou omissão voluntária, comete ato ilícito.", bool revogada = false,
        string esfera = "federal", string? tribunal = null, string fonte = "https://www.planalto.gov.br/x",
        string? area = null, Guid? normaId = null, string versaoTexto = "compilada", double score = 0.5, string? urn = null) =>
        new NormaPayload(
            normaId ?? Guid.NewGuid(), urn, titulo, tipo, numero, new DateOnly(2002, 1, 10), revogada ? "Revogada" : "Vigente",
            revogada, versaoTexto, 1, esfera, null, null, area, tribunal, [], Colecoes.LeisFederais, "juridico/markdown/x.md",
            fonte, null, null, artigo, null, null, ContextSelector.HashText(id + texto), texto).ToHit(id, score);

    public static RetrievedChunk Chunk(VectorHit hit) => new(hit.PointId, hit.Texto, hit.Score, hit.Meta);

    public static string LeiCivil(string artigo1 = "Toda pessoa é capaz de direitos e deveres na ordem civil.") => $"""
        ---
        titulo: "Lei nº 10.406 de 10/01/2002"
        tipo: "Lei Ordinária"
        numero: "10406"
        data: 2002-01-10
        ementa: "Institui o Código Civil."
        revogada: false
        versao_texto: "compilada"
        urn: "urn:lex:br:federal:lei:2002-01-10;10406"
        fonte: "https://normas.leg.br/?urn=urn:lex:br:federal:lei:2002-01-10;10406"
        ---
        # Lei nº 10.406 de 10/01/2002

        **Art. 1º** {artigo1}

        **Art. 2º** A personalidade civil da pessoa começa do nascimento com vida.

        **Art. 186.** Aquele que, por ação ou omissão voluntária, violar direito e causar dano a outrem comete ato ilícito.
        """;
}

// ------------------------------------------------------------------ reranker e busca híbrida

public class NormaRerankerTests
{
    private static IReadOnlyList<CandidatoRerankeado> Rank(string consulta, string? area, params (VectorHit Hit, double Fusao)[] candidatos) =>
        NormaReranker.Reordenar(candidatos.Select(c => new CandidatoBusca(c.Hit, c.Fusao)).ToList(), new RerankContexto(consulta, area, null));

    [Fact]
    public void ArtigoENormaCitadosNaConsulta_PassamNaFrenteDeUmTrechoMaisSimilar()
    {
        var alvo = NormaSamples.Hit("a", "Lei nº 10.406 de 10/01/2002", artigo: "186", texto: "Ato ilícito: dano moral gera indenização.");
        var outro = NormaSamples.Hit("b", "Lei nº 10.406 de 10/01/2002", artigo: "927", texto: "Aplica-se o disposto neste capítulo.");

        var ordem = Rank("indenização por dano moral, art. 186 da Lei 10.406", "civel", (alvo, 0.016), (outro, 0.033));

        ordem[0].Hit.PointId.Should().Be("a");
    }

    [Fact]
    public void NormaRevogada_NaoEDescartada_SoPerdePontos()
    {
        var vigente = NormaSamples.Hit("v", "Lei nº 1 de 2000", numero: "1", artigo: "1", texto: "Dano moral indenizável.");
        var revogada = NormaSamples.Hit("r", "Lei nº 2 de 1990", numero: "2", artigo: "1", texto: "Dano moral indenizável.", revogada: true);

        var ordem = Rank("dano moral indenizável", null, (vigente, 0.03), (revogada, 0.03));

        ordem.Should().HaveCount(2);
        ordem[0].Hit.PointId.Should().Be("v");
        ordem[1].Hit.PointId.Should().Be("r");
        ordem[0].Score.Should().BeGreaterThan(ordem[1].Score);
    }

    [Fact]
    public void HierarquiaNormativa_ConstituicaoAntesDeDecreto()
    {
        var cf = NormaSamples.Hit("cf", "Constituição de 1988", tipo: "Constituição", numero: null, texto: "Direito de propriedade.");
        var decreto = NormaSamples.Hit("d", "Decreto nº 5 de 2001", tipo: "Decreto", numero: "5", texto: "Direito de propriedade.");

        Rank("direito de propriedade", null, (decreto, 0.03), (cf, 0.03))[0].Hit.PointId.Should().Be("cf");
    }

    [Fact]
    public void AreaDiferente_PerdePontos_AreaIgual_Ganha()
    {
        var civel = NormaSamples.Hit("c", "Lei nº 1", numero: "1", area: "civel", texto: "Contrato de locação.");
        var trab = NormaSamples.Hit("t", "Lei nº 2", numero: "2", area: "trabalhista", texto: "Contrato de locação.");

        Rank("contrato de locação", "civel", (trab, 0.03), (civel, 0.03))[0].Hit.PointId.Should().Be("c");
    }

    [Fact]
    public void FonteOficialAusente_PerdePontos()
    {
        var comFonte = NormaSamples.Hit("f", "Lei nº 1", numero: "1", texto: "Texto idêntico sobre contrato.");
        var semFonte = NormaSamples.Hit("s", "Lei nº 2", numero: "2", texto: "Texto idêntico sobre contrato.", fonte: "");

        Rank("contrato", null, (semFonte, 0.03), (comFonte, 0.03))[0].Hit.PointId.Should().Be("f");
    }

    [Fact]
    public void ListaVazia_DevolveVazio() =>
        NormaReranker.Reordenar([], new RerankContexto("x", null, null)).Should().BeEmpty();

    [Fact]
    public void TermosEReferencias_SaoExtraidosDaConsulta()
    {
        NormaTexto.ArtigosMencionados("art. 186 e artigos 927").Should().BeEquivalentTo("186", "927");
        NormaTexto.NormasMencionadas("Lei nº 10.406 e Súmula 297").Should().BeEquivalentTo("10406", "297");
        NormaTexto.NumeroDoArtigo("121-A").Should().Be("121");
        NormaTexto.Termos("O autor pede indenização por dano moral").Should().Contain("indenização").And.NotContain("autor");
    }
}

public class HybridSearchServiceTests
{
    private static HybridSearchService Service(MemNormaVectorStore vectors, INormaSearchIndex keywords, BuscaNormasOptions? options = null) =>
        new(vectors, keywords, new RuleBasedNormaReranker(), Options.Create(options ?? new BuscaNormasOptions()), NullLogger<HybridSearchService>.Instance);

    private sealed class KeywordIndex(IReadOnlyList<VectorHit> hits, bool fail = false) : INormaSearchIndex
    {
        public Task<IReadOnlyList<VectorHit>> BuscarPalavrasChaveAsync(NormaFiltro filtro, string consulta, int limit, CancellationToken cancellationToken = default) =>
            fail ? throw new InvalidOperationException("db fora") : Task.FromResult(hits);

        public Task<bool> UrnExisteAsync(string urn, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    [Fact]
    public void Fusao_RrfPremiaQuemApareceNasDuasBuscas()
    {
        var a = NormaSamples.Hit("a", "A");
        var b = NormaSamples.Hit("b", "B");
        var c = NormaSamples.Hit("c", "C");

        var fundidos = HybridSearchService.Fundir([a, b], [c, b], 60);

        fundidos[0].Hit.PointId.Should().Be("b", "está nas duas listas");
        fundidos.Select(f => f.Hit.PointId).Should().BeEquivalentTo("a", "b", "c");
        fundidos[0].Fusao.Should().BeApproximately((1.0 / 62) + (1.0 / 62), 1e-9);
    }

    [Fact]
    public void Selecao_RemoveDuplicatasELimitaTrechosPorNorma()
    {
        var norma = Guid.NewGuid();
        var hits = Enumerable.Range(0, 6).Select(i => NormaSamples.Hit($"n{i}", "Lei X", normaId: norma, artigo: $"{i}", texto: $"Texto {i}")).ToList();
        var duplicado = NormaSamples.Hit("d", "Lei Y", texto: hits[0].Texto) with { Meta = hits[0].Meta with { HashConteudo = hits[0].Meta.HashConteudo } };

        var final = HybridSearchService.Selecionar(
            hits.Append(duplicado).Select((h, i) => new CandidatoRerankeado(h, 1.0 - (i * 0.01))).ToList(), 10);

        final.Count(h => h.Meta.Norma!.NormaId == norma).Should().Be(3, "uma norma não monopoliza o contexto");
        final.Select(h => h.PointId).Should().NotContain("d", "mesmo conteúdo já presente");
    }

    [Fact]
    public async Task Busca_CombinaVetorEPalavraChave_ELimitaAoMaximo()
    {
        var vectors = new MemNormaVectorStore
        {
            Hits = Enumerable.Range(0, 20).Select(i => NormaSamples.Hit($"v{i}", $"Lei {i}", numero: $"{i}", texto: $"Vetor {i}")).ToList(),
        };
        var keywords = new KeywordIndex(Enumerable.Range(0, 20).Select(i => NormaSamples.Hit($"k{i}", $"Decreto {i}", numero: $"{100 + i}", texto: $"Palavra {i}")).ToList());

        var result = await Service(vectors, keywords).BuscarAsync(new NormaBusca("dano moral", new NormaFiltro(Area: "civel")), new float[4]);

        result.Count.Should().Be(10);
        vectors.LastFiltro!.Area.Should().Be("civel");
        result.Select(h => h.PointId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task FalhaNaPalavraChave_DegradaParaSoVetor()
    {
        var vectors = new MemNormaVectorStore { Hits = [NormaSamples.Hit("v", "Lei", texto: "Só vetor.")] };

        var result = await Service(vectors, new KeywordIndex([], fail: true)).BuscarAsync(new NormaBusca("x", new NormaFiltro()), new float[4]);

        result.Should().ContainSingle().Which.PointId.Should().Be("v");
    }

    [Fact]
    public async Task IncluirRevogadasFalsoNaConfiguracao_PropagaAoFiltro()
    {
        var vectors = new MemNormaVectorStore();

        await Service(vectors, new KeywordIndex([]), new BuscaNormasOptions { IncluirRevogadas = false })
            .BuscarAsync(new NormaBusca("x", new NormaFiltro()), new float[4]);

        vectors.LastFiltro!.IncluirRevogadas.Should().BeFalse();
    }
}

// ------------------------------------------------------------------ validação de citações

public class NormaCitationValidatorTests
{
    private const string Lei = "Lei nº 10.406 de 10/01/2002";

    private static ValidacaoCitacoes Validar(string texto, IEnumerable<VectorHit> hits, string caso = "", IReadOnlySet<string>? urns = null) =>
        NormaCitationValidator.Validar(texto, hits.Select(NormaSamples.Chunk).ToList(), caso, urns);

    [Fact]
    public void CitacaoQueConfereComOTrechoRecuperado_EValidada_ENaoBloqueia()
    {
        var hit = NormaSamples.Hit("a", Lei, artigo: "186");

        var resultado = Validar($"O ato ilícito gera dever de indenizar [{Lei}, art. 186].", [hit]);

        resultado.Itens.Should().Contain(i => i.Status == CitacaoStatus.Validada);
        resultado.ExportacaoBloqueada.Should().BeFalse();
    }

    [Fact]
    public void NormaRevogadaSemContextoTemporal_EMarcada_ComContextoEValidada()
    {
        var hit = NormaSamples.Hit("a", Lei, artigo: "186", revogada: true);

        var semData = Validar($"Aplica-se o dispositivo [{Lei}, art. 186].", [hit]);
        var comData = Validar($"A norma, hoje revogada (2015), previa o dispositivo [{Lei}, art. 186].", [hit]);

        semData.Itens.Should().Contain(i => i.Status == CitacaoStatus.NormaRevogada);
        comData.Itens.Should().NotContain(i => i.Status == CitacaoStatus.NormaRevogada);
    }

    [Fact]
    public void ArtigoDaMesmaNormaQueNaoFoiRecuperado_FicaPendente_EBloqueiaAExportacao()
    {
        var hit = NormaSamples.Hit("a", Lei, artigo: "186");

        var resultado = Validar($"A responsabilidade decorre do [{Lei}, art. 927].", [hit]);

        resultado.Itens.Should().Contain(i => i.Status == CitacaoStatus.PendenteDeVerificacao && i.Texto.Contains("927"));
        resultado.ExportacaoBloqueada.Should().BeTrue();
    }

    [Fact]
    public void FonteQueNaoEstaNoContexto_EPossivelAlucinacao()
    {
        var hit = NormaSamples.Hit("a", Lei, artigo: "186");

        var resultado = Validar("Aplica-se o CDC [Lei nº 8.078 de 11/09/1990, art. 6].", [hit]);

        resultado.Itens.Should().Contain(i => i.Status == CitacaoStatus.PossivelAlucinacao);
        resultado.ExportacaoBloqueada.Should().BeTrue();
    }

    [Fact]
    public void SumulaAtribuidaAoTribunalErrado_EPossivelAlucinacao()
    {
        var sumula = NormaSamples.Hit(
            "s", "Súmula nº 5 do STJ", tipo: "Súmula", numero: "5", artigo: null, tribunal: "STJ", esfera: "jurisprudencia",
            texto: "Súmula nº 5 do STJ. A simples interpretação de cláusula contratual não enseja recurso especial.");

        var resultado = Validar("Conforme a Súmula 5 do STF, a interpretação de cláusula não enseja recurso.", [sumula]);

        resultado.Itens.Should().Contain(i => i.Status == CitacaoStatus.PossivelAlucinacao && i.Detalhe.Contains("outro tribunal"));
    }

    [Fact]
    public void TrechoEntreAspasInexistente_EPossivelAlucinacao_ETrechoReal_Passa()
    {
        var hit = NormaSamples.Hit("a", Lei, artigo: "186", texto: "Aquele que, por ação ou omissão voluntária, violar direito e causar dano a outrem, comete ato ilícito.");

        var inventado = Validar("Diz a lei: “o dano moral dispensa qualquer prova de culpa do agente causador do prejuízo”.", [hit]);
        var real = Validar("Diz a lei: “Aquele que, por ação ou omissão voluntária, violar direito e causar dano a outrem, comete ato ilícito.”", [hit]);

        inventado.Itens.Should().Contain(i => i.Status == CitacaoStatus.PossivelAlucinacao && i.Detalhe.Contains("aspas"));
        real.Itens.Should().NotContain(i => i.Detalhe.Contains("aspas"));
    }

    [Fact]
    public void UrnForaDoContexto_EAlucinacao_ComUrnConhecidaNoBanco_FicaPendente()
    {
        var hit = NormaSamples.Hit("a", Lei, artigo: "186", urn: "urn:lex:br:federal:lei:2002-01-10;10406");
        const string urn = "urn:lex:br:federal:lei:1990-09-11;8078";

        var desconhecida = Validar($"Veja {urn}.", [hit]);
        var conhecida = Validar($"Veja {urn}.", [hit], urns: new HashSet<string> { urn });

        desconhecida.Itens.Should().Contain(i => i.Status == CitacaoStatus.PossivelAlucinacao);
        conhecida.Itens.Should().Contain(i => i.Status == CitacaoStatus.PendenteDeVerificacao);
        NormaCitationValidator.UrnsParaConferir($"Veja {urn}.", [NormaSamples.Chunk(hit)]).Should().Equal(urn);
    }

    [Fact]
    public void MesmaNormaEmDoisArquivosDistintos_GeraConflitoDeVersao()
    {
        var a = NormaSamples.Hit("a", Lei, artigo: "186", versaoTexto: "original");
        var b = NormaSamples.Hit("b", Lei, artigo: "187", versaoTexto: "compilada");

        var resultado = Validar($"Cita-se o [{Lei}, art. 186].", [a, b]);

        resultado.Itens.Should().Contain(i => i.Status == CitacaoStatus.ConflitoDeVersao);
    }

    [Fact]
    public void TextoSemCitacoes_NaoBloqueia()
    {
        var resultado = Validar("DOS FATOS\nO autor contratou os serviços.", [NormaSamples.Hit("a", Lei)]);

        resultado.Itens.Should().BeEmpty();
        resultado.ExportacaoBloqueada.Should().BeFalse();
    }

    [Fact]
    public void RotuloDaNorma_UsaNormaEArtigo_SemArtigoUsaTexto()
    {
        NormaSamples.Chunk(NormaSamples.Hit("a", Lei, artigo: "186")).Rotulo.Should().Be($"{Lei}, art. 186");
        NormaSamples.Chunk(NormaSamples.Hit("s", "Súmula nº 1 do STF", artigo: null)).Rotulo.Should().Be("Súmula nº 1 do STF, texto");
    }

    [Fact]
    public void AvisosDoProcessor_IncluemProblemasETravaDeExportacao()
    {
        var hit = NormaSamples.Hit("a", Lei, artigo: "186");
        var texto = $"Veja [{Lei}, art. 927] e [Lei nº 8.078 de 11/09/1990, art. 6].";
        var chunks = new[] { NormaSamples.Chunk(hit) };

        var validacao = NormaCitationValidator.Validar(texto, chunks, string.Empty);
        var avisos = PecaProcessor.CitationWarnings(validacao, CitationVerifier.Verify(texto, chunks, string.Empty)).ToList();

        avisos.Should().Contain(a => a.StartsWith(NormaCitationValidator.MarcadorBloqueio));
    }
}

// ------------------------------------------------------------------ ingestão

public class NormaIngestionServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "normas-" + Guid.NewGuid().ToString("N"));
    private readonly MemNormaStore _store = new();
    private readonly MemObjectStorage _storage = new();
    private readonly MemNormaVectorStore _vectors = new();
    private readonly NormasIngestaoOptions _options = new();
    private StubEmbeddings _embeddings = new();

    public NormaIngestionServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private NormaIngestionService Service() => new(
        _store, _storage, _embeddings, _vectors, new NormaChunker(new ChunkingOptions()), Options.Create(_options),
        new GpuStateService(Options.Create(new VastAiOptions())), new FakeClock(), NullLogger<NormaIngestionService>.Instance);

    private RepoArquivo Escrever(string relativo, string conteudo)
    {
        var path = Path.Combine(_dir, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, conteudo, new UTF8Encoding(false));

        return new RepoArquivo(relativo, path, new FileInfo(path).Length);
    }

    private const string Caminho = "federal/leis-ordinarias/2002/lei-10406-2002-01-10.md";

    [Fact]
    public async Task ArquivoNovo_GravaMarkdownVersaoChunksEEmbeddingsNaColecaoCerta()
    {
        var resultado = await Service().IngerirAsync(Escrever(Caminho, NormaSamples.LeiCivil()), "abc123");

        resultado.Resultado.Should().Be(IngestaoResultado.Novo);
        var norma = _store.Normas.Should().ContainSingle().Subject;
        norma.Status.Should().Be(NormaStatus.Indexada);
        norma.VersaoAtual.Should().Be(1);
        norma.Colecao.Should().Be(Colecoes.LeisFederais);
        norma.ArquivoMarkdownPath.Should().Be($"juridico/markdown/{Caminho}");
        _storage.Objects.Should().ContainKey(norma.ArquivoMarkdownPath);
        _store.Versoes.Should().ContainSingle().Which.CommitOrigem.Should().Be("abc123");
        _store.Chunks.Should().NotBeEmpty().And.OnlyContain(c => c.Status == ChunkStatus.Indexado && c.QdrantPointId == c.Id.ToString());

        _vectors.Upserts.Should().HaveCount(_store.Chunks.Count).And.OnlyContain(u => u.Colecao == Colecoes.LeisFederais);
        var ponto = _vectors.Upserts.Select(u => u.Point).First(p => p.Payload.Artigo == "186");
        ponto.Id.Should().NotBe(Guid.Empty);
        ponto.Payload.Titulo.Should().Be("Lei nº 10.406 de 10/01/2002");
        ponto.Payload.Urn.Should().Be("urn:lex:br:federal:lei:2002-01-10;10406");
        ponto.Payload.ArquivoMarkdownPath.Should().Be($"juridico/markdown/{Caminho}");
        ponto.Payload.FonteOficialUrl.Should().StartWith("https://normas.leg.br/");
        ponto.Payload.Texto.Should().Contain("Art. 186");
        _vectors.Deletes.Should().Contain((Colecoes.LeisFederais, norma.Id, 1), "a versão anterior (nenhuma) sai só depois da nova");
    }

    [Fact]
    public async Task SemMudanca_NaoReprocessaNemPagaEmbedding()
    {
        var arquivo = Escrever(Caminho, NormaSamples.LeiCivil());
        await Service().IngerirAsync(arquivo, "c1");
        var embeddingsAntes = _embeddings.Inputs;
        var upsertsAntes = _vectors.Upserts.Count;

        var segunda = await Service().IngerirAsync(arquivo, "c2");

        segunda.Resultado.Should().Be(IngestaoResultado.SemMudanca);
        _embeddings.Inputs.Should().Be(embeddingsAntes);
        _vectors.Upserts.Should().HaveCount(upsertsAntes);
        _store.Versoes.Should().ContainSingle();
    }

    [Fact]
    public async Task ArquivoAlterado_CriaNovaVersao_PreservaAAntiga_ERemoveOsPontosVelhos()
    {
        await Service().IngerirAsync(Escrever(Caminho, NormaSamples.LeiCivil()), "c1");
        var norma = _store.Normas.Single();
        var hashAntigo = norma.HashSha256;

        var resultado = await Service().IngerirAsync(Escrever(Caminho, NormaSamples.LeiCivil("Texto NOVO do artigo primeiro.")), "c2");

        resultado.Resultado.Should().Be(IngestaoResultado.Atualizado);
        norma.VersaoAtual.Should().Be(2);
        norma.HashSha256.Should().NotBe(hashAntigo);
        _store.Versoes.Should().HaveCount(2);
        var antiga = _store.Versoes.Single(v => v.Numero == 1);
        antiga.SubstituidaEm.Should().NotBeNull();
        antiga.ArquivoMarkdownPath.Should().Be($"juridico/versoes/{norma.Id}/{hashAntigo}.md");
        _storage.Copies.Should().Contain(($"juridico/markdown/{Caminho}", antiga.ArquivoMarkdownPath));
        Encoding.UTF8.GetString(_storage.Objects[norma.ArquivoMarkdownPath]).Should().Contain("Texto NOVO");
        _store.Chunks.Should().OnlyContain(c => c.Versao == 2, "os trechos da versão anterior saem do banco");
        _vectors.Deletes.Should().Contain((Colecoes.LeisFederais, norma.Id, 2));
    }

    [Fact]
    public async Task FalhaNoEmbedding_MarcaFalhou_ERetentativaNaoCriaVersaoNemDuplicaChunks()
    {
        _embeddings = new StubEmbeddings(failTimes: 1);
        var arquivo = Escrever(Caminho, NormaSamples.LeiCivil());

        var primeira = await Service().IngerirAsync(arquivo, "c1");

        primeira.Resultado.Should().Be(IngestaoResultado.Falhou);
        var norma = _store.Normas.Single();
        norma.Status.Should().Be(NormaStatus.Falhou);
        var chunksNaFalha = _store.Chunks.Count;

        var segunda = await Service().IngerirAsync(arquivo, "c2");

        segunda.Resultado.Should().Be(IngestaoResultado.Atualizado);
        norma.Status.Should().Be(NormaStatus.Indexada);
        norma.VersaoAtual.Should().Be(1, "mesmo conteúdo: não é versão nova");
        _store.Versoes.Should().ContainSingle();
        _store.Chunks.Should().HaveCount(chunksNaFalha).And.OnlyContain(c => c.Status == ChunkStatus.Indexado);
    }

    [Fact]
    public async Task ChunksComTextoIdentico_SaoDeduplicadosAntesDoEmbedding()
    {
        var resultado = await Service().IngerirAsync(
            Escrever(Caminho, "---\ntitulo: \"Lei X\"\nfonte: \"https://x.gov.br/\"\n---\n# Lei X\n\n**Art. 1º** Texto igual.\n\n**Art. 1º** Texto igual.\n"),
            "c1");

        resultado.Resultado.Should().Be(IngestaoResultado.Novo);
        _store.Chunks.Should().ContainSingle(c => c.Artigo == "1º");
    }

    [Fact]
    public async Task ArquivoInvalidoOuForaDoRecorte_EIgnorado_SemEfeitos()
    {
        var vazio = await Service().IngerirAsync(Escrever("federal/decretos/2001/vazio.md", "---\ntitulo: \"X\"\n---\n"), "c1");

        _options.Filtros.Tipos = ["decreto"];
        var fora = await Service().IngerirAsync(Escrever(Caminho, NormaSamples.LeiCivil()), "c1");

        vazio.Resultado.Should().Be(IngestaoResultado.Ignorado);
        fora.Resultado.Should().Be(IngestaoResultado.Ignorado);
        fora.Motivo.Should().Contain("tipo");
        _store.Normas.Should().BeEmpty();
        _storage.Objects.Should().BeEmpty();
        _embeddings.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ArquivoAcimaDoLimiteDeTamanho_EIgnorado()
    {
        _options.TamanhoMaximoArquivoMb = 0;

        var resultado = await Service().IngerirAsync(Escrever(Caminho, NormaSamples.LeiCivil()), "c1");

        resultado.Resultado.Should().Be(IngestaoResultado.Ignorado);
    }
}

// ------------------------------------------------------------------ orquestrador

public class NormaSyncOrchestratorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sync-" + Guid.NewGuid().ToString("N"));
    private readonly MemNormaStore _store = new();
    private readonly MemObjectStorage _storage = new();
    private readonly MemNormaVectorStore _vectors = new();
    private readonly FakeGpuOrchestrator _gpu = new();
    private readonly NormasIngestaoOptions _options = new() { ParalelismoArquivos = 1 };
    private readonly StubEmbeddings _embeddings = new();

    public NormaSyncOrchestratorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class DiskSync(string dir) : IRepositorySync
    {
        public Task<RepoSnapshot> SincronizarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepoSnapshot(
                "commit1",
                Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Select(p => new RepoArquivo(Path.GetRelativePath(dir, p).Replace('\\', '/'), p, new FileInfo(p).Length)).ToList()));
    }

    private NormaSyncOrchestrator Orchestrator(StubEmbeddings? embeddings = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<INormaStore>(_store);
        services.AddSingleton<IObjectStorage>(_storage);
        services.AddSingleton<INormaVectorStore>(_vectors);
        services.AddSingleton<IEmbeddingService>(embeddings ?? _embeddings);
        services.AddSingleton<IGpuOrchestrator>(_gpu);
        services.AddSingleton(new NormaChunker(new ChunkingOptions()));
        services.AddSingleton(Options.Create(_options));
        services.AddSingleton<IGpuStateService>(new GpuStateService(Options.Create(new VastAiOptions())));
        services.AddSingleton<Advocacia.BuildingBlocks.Domain.Time.IDateTimeProvider>(new FakeClock());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<INormaIngestionService, NormaIngestionService>();
        var provider = services.BuildServiceProvider();

        return new NormaSyncOrchestrator(
            new DiskSync(_dir), provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(_options),
            new FakeClock(), NullLogger<NormaSyncOrchestrator>.Instance);
    }

    private void Escrever(string relativo, string conteudo)
    {
        var path = Path.Combine(_dir, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, conteudo, new UTF8Encoding(false));
    }

    private static string Lei(string numero, string texto = "Texto da lei sobre o assunto.") =>
        $"---\ntitulo: \"Lei nº {numero}\"\nnumero: \"{numero}\"\nfonte: \"https://planalto.gov.br/{numero}\"\n---\n# Lei nº {numero}\n\n**Art. 1º** {texto}\n";

    [Fact]
    public async Task PrimeiraCarga_ProcessaOsValidos_IgnoraOsInvalidos_ELigaAGpuUmaVez()
    {
        Escrever("federal/leis-ordinarias/2001/lei-1.md", Lei("1"));
        Escrever("federal/leis-ordinarias/2001/lei-2.md", Lei("2"));
        Escrever("federal/leis-ordinarias/2001/vazia.md", "---\ntitulo: \"X\"\n---\n");
        Escrever("federal/_auditoria/lixo.md", Lei("999"));
        Escrever("federal/leis-ordinarias/2001/dados.json", "{}");

        var resumo = await Orchestrator().SincronizarAsync(IngestaoJobTipo.Completa);

        resumo.Total.Should().Be(3, "o que é _auditoria e não-Markdown nem entra na contagem");
        resumo.Novos.Should().Be(2);
        resumo.Ignorados.Should().Be(1);
        resumo.Erros.Should().Be(0);
        _gpu.Garantidas.Should().Be(1);
        _store.Normas.Should().HaveCount(2);
        var job = _store.Jobs.Should().ContainSingle().Subject;
        job.Status.Should().Be(IngestaoJobStatus.Concluido);
        job.CommitOrigem.Should().Be("commit1");
        job.ArquivosProcessados.Should().Be(2);
    }

    [Fact]
    public async Task SegundoCiclo_SemMudancas_NaoLigaAGpuNemReprocessa()
    {
        Escrever("federal/leis-ordinarias/2001/lei-1.md", Lei("1"));
        Escrever("federal/leis-ordinarias/2001/vazia.md", "---\ntitulo: \"X\"\n---\n");
        var orchestrator = Orchestrator();
        await orchestrator.SincronizarAsync(IngestaoJobTipo.Completa);
        var embeddings = _embeddings.Inputs;

        var resumo = await orchestrator.SincronizarAsync(IngestaoJobTipo.Incremental);

        resumo.Novos.Should().Be(0);
        resumo.Atualizados.Should().Be(0);
        resumo.SemMudanca.Should().Be(1);
        resumo.Ignorados.Should().Be(1);
        _gpu.Garantidas.Should().Be(1, "nada a embutir: a GPU não é religada");
        _embeddings.Inputs.Should().Be(embeddings);
    }

    [Fact]
    public async Task Incremental_ReprocessaSoOAlterado_EMarcaORemovido()
    {
        Escrever("federal/leis-ordinarias/2001/lei-1.md", Lei("1"));
        Escrever("federal/leis-ordinarias/2001/lei-2.md", Lei("2"));
        var orchestrator = Orchestrator();
        await orchestrator.SincronizarAsync(IngestaoJobTipo.Completa);
        var norma2 = _store.Normas.Single(n => n.Numero == "2");

        Escrever("federal/leis-ordinarias/2001/lei-1.md", Lei("1", "Texto ALTERADO da lei."));
        File.Delete(Path.Combine(_dir, "federal/leis-ordinarias/2001/lei-2.md"));
        var resumo = await orchestrator.SincronizarAsync(IngestaoJobTipo.Incremental);

        resumo.Atualizados.Should().Be(1);
        resumo.Removidos.Should().Be(1);
        norma2.Status.Should().Be(NormaStatus.Removida);
        _vectors.Deletes.Should().Contain((Colecoes.LeisFederais, norma2.Id, (int?)null));
        _store.Chunks.Should().NotContain(c => c.NormaJuridicaId == norma2.Id);
        _store.Versoes.Should().Contain(v => v.NormaJuridicaId == norma2.Id, "o histórico fica para auditoria");
    }

    [Fact]
    public async Task EscopoReduzidoDeConfiguracao_NaoMarcaRemovidoOQueSoSaiuDoEscopo()
    {
        Escrever("federal/leis-ordinarias/2001/lei-1.md", Lei("1"));
        Escrever("estaduais/CE/leis/lei-9.md", Lei("9"));
        _options.PastasIncluidas = ["federal", "estaduais/CE"];
        var orchestrator = Orchestrator();
        await orchestrator.SincronizarAsync(IngestaoJobTipo.Completa);

        _options.PastasIncluidas = ["federal"];
        var resumo = await orchestrator.SincronizarAsync(IngestaoJobTipo.Incremental);

        resumo.Removidos.Should().Be(0, "estaduais/CE deixou de ser monitorado, não foi apagado do repositório");
        _store.Normas.Should().OnlyContain(n => n.Status == NormaStatus.Indexada);
    }

    [Fact]
    public async Task LimitePorCiclo_ProcessaPorPrioridade_EMarcaParcial()
    {
        Escrever("constituicao.md", "# Constituição de 1988\n\n> Fonte: [x](https://planalto.gov.br/cf)\n\n**Art. 1º** A República Federativa do Brasil.\n");
        Escrever("federal/decretos/2001/d-1.md", Lei("1"));
        _options.LimiteArquivosPorCiclo = 1;

        var resumo = await Orchestrator().SincronizarAsync(IngestaoJobTipo.Completa);

        resumo.Parcial.Should().BeTrue();
        resumo.Novos.Should().Be(1);
        _store.Normas.Should().ContainSingle().Which.RepoPath.Should().Be("constituicao.md");
    }

    [Fact]
    public async Task GpuIndisponivel_FalhaOJobSemTocarNosArquivos()
    {
        Escrever("federal/leis-ordinarias/2001/lei-1.md", Lei("1"));
        _gpu.Falhar = true;

        var resumo = await Orchestrator().SincronizarAsync(IngestaoJobTipo.Completa);

        resumo.Novos.Should().Be(0);
        _store.Normas.Should().BeEmpty();
        _store.Jobs.Single().Status.Should().Be(IngestaoJobStatus.Falhou);
    }

    [Fact]
    public async Task FalhasSeguidasDeInfra_AbortamOCiclo()
    {
        for (var i = 1; i <= 8; i++)
        {
            Escrever($"federal/leis-ordinarias/2001/lei-{i}.md", Lei(i.ToString()));
        }

        // Embeddings sempre falhando: depois de 5 falhas seguidas o ciclo para em vez de martelar a GPU.
        var resumo = await Orchestrator(new StubEmbeddings(failTimes: 1000)).SincronizarAsync(IngestaoJobTipo.Completa);

        resumo.Erros.Should().BeGreaterThanOrEqualTo(5);
        resumo.Erros.Should().BeLessThan(8);
        resumo.Parcial.Should().BeTrue();
        _store.Jobs.Single().Status.Should().Be(IngestaoJobStatus.Falhou);
    }
}

public class NormaAdminServiceTests
{
    [Fact]
    public async Task SolicitarSincronizacao_CriaUmPedidoPendente_EPedidosRepetidosReaproveitamOMesmo()
    {
        var store = new MemNormaStore();
        var service = new NormaAdminService(store, new FakeClock());

        var primeira = await service.SolicitarSincronizacaoAsync(reprocessar: false);
        var segunda = await service.SolicitarSincronizacaoAsync(reprocessar: true);

        primeira.IsSuccess.Should().BeTrue();
        segunda.Value.Id.Should().Be(primeira.Value.Id);
        store.Jobs.Should().ContainSingle().Which.Status.Should().Be(IngestaoJobStatus.Pendente);
    }

    [Fact]
    public async Task ObterNorma_NaoExpoeChaveDoStorage_E404ParaRemovida()
    {
        var store = new MemNormaStore();
        var meta = new NormaMetadata(
            "Lei X", "Lei", "1", 2000, null, null, null, "Vigente", false, "v", "urn:x", "https://planalto.gov.br/x", "federal", null, null, null, null, [],
            Colecoes.LeisFederais);
        var norma = NormaJuridica.Criar(meta, "federal/leis/x.md", "juridico/markdown/federal/leis/x.md", "h", "ctresb/leis", DateTimeOffset.UtcNow);
        store.Adicionar(norma);
        var service = new NormaAdminService(store, new FakeClock());

        var ok = await service.ObterNormaAsync(norma.Id);
        norma.MarcarRemovida(DateTimeOffset.UtcNow);
        var removida = await service.ObterNormaAsync(norma.Id);

        ok.Value.Arquivo.Should().Be("federal/leis/x.md");
        ok.Value.Aviso.Should().Contain("Não substitui a publicação oficial");
        System.Text.Json.JsonSerializer.Serialize(ok.Value).Should().NotContain("juridico/markdown");
        removida.IsFailure.Should().BeTrue();
    }
}
