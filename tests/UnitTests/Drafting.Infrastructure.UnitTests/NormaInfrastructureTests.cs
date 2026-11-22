using System.Net;
using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Infrastructure.Storage;
using Advocacia.Modules.Drafting.Domain.Normas;
using Advocacia.Modules.Drafting.Infrastructure.Normas;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Infrastructure.Qdrant;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

public class NormaQdrantStoreTests
{
    private static NormaPayload Payload(string? uf = null) => new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "urn:lex:br:federal:lei:2002-01-10;10406", "Lei nº 10.406 de 10/01/2002",
        "Lei Ordinária", "10406", new DateOnly(2002, 1, 10), "Vigente", false, "compilada", 3, "federal", uf, null, "civel", null,
        ["responsabilidade civil", "dano moral"], Colecoes.LeisFederais, "federal/leis-ordinarias/2002/lei-10406-2002-01-10.md",
        "https://normas.leg.br/x", null, "TÍTULO I > CAPÍTULO II", "186", "§ 1º", "I", "hash1", "Art. 186. Aquele que...");

    [Fact]
    public void Filtro_Padrao_NaoRestringeNada_NemExcluiRevogadas() =>
        NormaQdrantStore.BuildFilter(new NormaFiltro()).Must.Should().BeEmpty();

    [Fact]
    public void Filtro_VigenciaTipoDataEUf_SaoCondicoesObrigatorias()
    {
        var filter = NormaQdrantStore.BuildFilter(new NormaFiltro(
            Tipos: ["Lei Ordinária"], Ufs: ["ce"], IncluirRevogadas: false, DataDe: new DateOnly(2000, 1, 1)));

        filter.Must.Where(c => c.Field != null).Select(c => c.Field.Key).Should().Contain(["revogada", "tipo", "data"]);
        filter.Must.Where(c => c.Filter != null).Should().ContainSingle().Which.Filter.Should.Select(c => c.Field.Key).Should().BeEquivalentTo("uf", "esfera");
    }

    [Fact]
    public void Filtro_Area_AceitaAsCompativeis_EOTrechoSemArea()
    {
        var area = NormaQdrantStore.BuildFilter(new NormaFiltro(Areas: AreaMapper.Compativeis("consumidor"))).Must.Should().ContainSingle().Subject.Filter;

        area.Should.Should().HaveCount(2).And.Contain(c => c.IsEmpty != null && c.IsEmpty.Key == "area");
        area.Should.First(c => c.Field != null).Field.Match.Keywords.Strings.Should().Contain(["consumidor", "civel", "constitucional", "processual"]);
    }

    [Fact]
    public async Task ColecaoDeAnexos_NuncaEAcessadaPorEstaClasse_QueNaoTemFiltroDeTenant()
    {
        using var conexao = new QdrantConexao(
            Options.Create(new QdrantOptions { BaseUrl = "http://localhost:1" }), Microsoft.Extensions.Logging.Abstractions.NullLogger<QdrantConexao>.Instance);
        var store = new NormaQdrantStore(conexao);

        await ((Func<Task>)(() => store.BuscarDensoAsync(Colecoes.DocumentosCaso, new NormaFiltro(), [0f], 1))).Should().ThrowAsync<ArgumentException>();
        await ((Func<Task>)(() => store.UpsertAsync(Colecoes.DocumentosCaso, []))).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void PayloadCompleto_IdaEVolta()
    {
        var id = Guid.NewGuid();
        var point = NormaQdrantStore.ToPoint(new NormaVectorPoint(id, [0.1f, 0.2f], Bm25.Documento("ato ilícito"), Payload()));

        point.Payload.Keys.Should().Contain(
        [
            "fonte", "normaJuridicaId", "urn", "titulo", "tipo", "numero", "ano", "data", "situacao", "revogada", "versaoTexto", "esfera",
            "area", "assuntos", "arquivo", "secao", "artigo", "paragrafo", "inciso", "hashConteudo", "texto", "versao", "fonteOficialUrl", "colecao",
        ]);
        point.Payload["fonte"].StringValue.Should().Be("ctb_leis");
        point.Payload["ano"].IntegerValue.Should().Be(2002);
        point.Payload["data"].StringValue.Should().Be("2002-01-10T00:00:00Z");
        point.Payload.Should().NotContainKey("uf", "campo nulo não vai ao payload");
        point.Vectors.Vectors_.Vectors.Keys.Should().BeEquivalentTo(QdrantConexao.Denso, QdrantConexao.Esparso);

        var scored = new ScoredPoint { Id = id, Score = 0.77f };
        foreach (var (key, value) in point.Payload)
        {
            scored.Payload[key] = value;
        }

        var hit = NormaQdrantStore.ToHit(scored);
        hit.Meta.Norma.Should().BeEquivalentTo(new
        {
            Titulo = "Lei nº 10.406 de 10/01/2002", Artigo = "186", Paragrafo = "§ 1º", Inciso = "I", VersaoNumero = 3, Colecao = Colecoes.LeisFederais,
            ArquivoMarkdownPath = "federal/leis-ordinarias/2002/lei-10406-2002-01-10.md",
        });
        hit.Meta.UrlOrigem.Should().Be("https://normas.leg.br/x");
        hit.Meta.Tags.Should().Equal("responsabilidade civil", "dano moral");
    }
}

public class CasoQdrantStoreTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Caso = Guid.NewGuid();
    private static readonly Guid Usuario = Guid.NewGuid();

    private static IEnumerable<string> Chaves(Filter filtro) => filtro.Must.Where(c => c.Field != null).Select(c => c.Field.Key);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TodaBusca_TemTenantECasoComoCondicaoObrigatoria(bool veRestritos)
    {
        var filtro = CasoQdrantStore.Filtro(new FiltroCaso(Tenant, Caso, Usuario, veRestritos));

        Chaves(filtro).Should().Contain([CasoQdrantStore.TenantKey, CasoQdrantStore.CasoKey]);
        filtro.Must.First(c => c.Field?.Key == CasoQdrantStore.CasoKey).Field.Match.Keyword.Should().Be(Caso.ToString());
        filtro.MustNot.Should().ContainSingle().Which.Field.Key.Should().Be("suspeito");
        filtro.Should.Should().BeEmpty("um OU no nível de cima anularia o isolamento");
    }

    [Fact]
    public void SemVerRestritos_SoOAbertoAoCasoOuOProprio()
    {
        var aninhado = CasoQdrantStore.Filtro(new FiltroCaso(Tenant, Caso, Usuario, false)).Must.Should().ContainSingle(c => c.Filter != null).Subject.Filter;

        aninhado.Should.Select(c => c.Field.Key).Should().BeEquivalentTo("permissao", "usuarioIdResponsavel");
    }

    [Fact]
    public void ExclusaoSemTenantOuCaso_NemMontaFiltro()
    {
        ((Action)(() => CasoQdrantStore.Base(Guid.Empty, Caso))).Should().Throw<ArgumentException>();
        ((Action)(() => CasoQdrantStore.Base(Tenant, Guid.Empty))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Payload_TemTudoQueOIsolamentoExige_EVoltaIgual()
    {
        var caso = new CasoRef(Tenant, Caso, Guid.NewGuid(), Guid.NewGuid(), Usuario, OrigemTexto.Ocr, 0.71, "Cláusula 5", true, false);
        var ponto = CasoQdrantStore.ToPoint(new CasoVectorPoint(
            Guid.NewGuid(), [0.1f], Bm25.Documento("multa"), "Multa de 10%.",
            new ChunkMetadata(caso.DocumentoId, "contrato.pdf", 4, "contrato", "civel", null, new DateOnly(2024, 3, 10), null, "h", Caso: caso),
            DateTimeOffset.UtcNow));

        ponto.Payload.Keys.Should().Contain(
            ["tenantId", "casoId", "clienteId", "usuarioIdResponsavel", "documentoId", "hash", "origem", "dataUpload", "permissao", "texto"]);

        var lido = new ScoredPoint { Id = ponto.Id, Score = 0.5f };
        foreach (var (chave, valor) in ponto.Payload)
        {
            lido.Payload[chave] = valor;
        }

        var hit = CasoQdrantStore.ToHit(lido);
        hit.Meta.Caso.Should().Be(caso);
        hit.Meta.Pagina.Should().Be(4);
        hit.Meta.DataPublicacao.Should().Be(new DateOnly(2024, 3, 10));
    }

    [Fact]
    public void PayloadSemIdentificacao_ViraIdentidadeVazia_QueAConferenciaRecusa()
    {
        var hit = CasoQdrantStore.ToHit(new ScoredPoint { Id = Guid.NewGuid() });

        hit.Meta.Caso!.TenantId.Should().Be(Guid.Empty);
        hit.Meta.Caso.Restrito.Should().BeTrue("sem permissão explícita, o padrão é o mais fechado");
    }
}

public class ReferenciaSqlTests
{
    [Fact]
    public void Sql_ParametrizaNumeroArtigoETribunal_ESeparaLeiDeSumula()
    {
        var (sql, parametros) = NormaRepository.MontarSqlReferencias(
            ExtratorReferencias.Extrair("art. 186 do Código Civil, art. 5º da Constituição Federal e Súmula 297 do STJ"), 30);

        sql.Should().Contain($"n.colecao = '{Colecoes.LeisFederais}'").And.Contain($"n.colecao = '{Colecoes.ConstituicaoFederal}'")
            .And.Contain("n.tipo NOT ILIKE '%vinculante%'").And.Contain("NOT ILIKE 'ATO DAS%'").And.NotContain("10406").And.NotContain("'STJ'");
        parametros.Select(p => p.Value).Should().Contain(["10406", "186", "5", "297", "STJ"]);
    }
}

public class NormaKeywordSqlTests
{
    [Fact]
    public void Sql_Parametriza_TermosEFiltros_SemConcatenarValores()
    {
        var (sql, parametros) = NormaRepository.MontarSql(
            new NormaFiltro(Tipos: ["lei"], Ufs: ["CE"], IncluirRevogadas: false, DataDe: new DateOnly(2000, 1, 1)),
            ["indenização", "moral"], 40);

        sql.Should().Contain("to_tsquery('portuguese', @tsq)");
        sql.Should().Contain("c.status = 'Indexado'").And.Contain("n.status <> 'Removida'");
        sql.Should().Contain("n.colecao = ANY(@colecoes)");
        sql.Should().Contain("n.tipo ILIKE ANY(@tipos)");
        sql.Should().Contain("(lower(n.uf) = ANY(@ufs) OR n.uf IS NULL)");
        sql.Should().Contain("n.revogada = false").And.Contain("n.data >= @dataDe");
        sql.Should().Contain("ORDER BY score DESC").And.Contain("LIMIT @lim");
        sql.Should().NotContain("indenização", "termos e filtros vão só como parâmetro");
        parametros.Single(p => p.ParameterName == "tsq").Value.Should().Be("indenização | moral");
        parametros.Single(p => p.ParameterName == "lim").Value.Should().Be(40);
    }

    [Fact]
    public void Sql_PadraoBuscaTodasAsColecoesPublicas_SemExcluirRevogadas()
    {
        var (sql, parametros) = NormaRepository.MontarSql(new NormaFiltro(), ["dano"], 10);

        sql.Should().NotContain("n.revogada = false");
        ((string[])parametros.Single(p => p.ParameterName == "colecoes").Value!).Should().BeEquivalentTo(Colecoes.Publicas);
    }
}

public class GitSparseRepositorySyncTests
{
    [Fact]
    public void PadroesSparse_IncluemAsPastasEExcluemAuditoriasFonteEArquivosNaoMarkdown()
    {
        var padroes = GitSparseRepositorySync.PadroesSparse(new NormasIngestaoOptions());
        var linhas = padroes.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        linhas.Should().Contain(["/constituicao.md", "/federal/", "/jurisprudencia/", "!estaduais/", "!municipais/", "!reguladores/", "!tratados/"]);
        linhas.Should().Contain(["!_*", "!_fonte/", "!scripts/", "!*.json", "!*.jsonl", "!*.gz", "!*.zst", "!*.pdf", "!*.html"]);
        linhas.Should().NotContain(l => l.Contains(".git"));
        linhas.Should().NotContain("/_fonte/", "originais só sob demanda");
        Array.IndexOf(linhas, "!_*").Should().BeGreaterThan(Array.IndexOf(linhas, "/federal/"), "no sparse-checkout o último padrão que casa vence");
    }

    [Fact]
    public void BaixarOriginais_AcrescentaAFontePorUltimo()
    {
        var linhas = GitSparseRepositorySync.PadroesSparse(new NormasIngestaoOptions { BaixarOriginais = true })
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        linhas[^1].Should().Be("/_fonte/");
    }
}

public class LocalDirectoryRepositorySyncTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("acervo-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Escrever(string relativo)
    {
        var caminho = Path.Combine(_dir, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, "# x");
    }

    private Task<RepoSnapshot> Sincronizar(Action<NormasIngestaoOptions>? ajuste = null)
    {
        var opcoes = new NormasIngestaoOptions { DiretorioLocal = _dir, SomenteAltaUtilizacao = false };
        ajuste?.Invoke(opcoes);

        return new LocalDirectoryRepositorySync(
            Options.Create(opcoes), Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalDirectoryRepositorySync>.Instance).SincronizarAsync();
    }

    [Fact]
    public async Task ListaSoOEscopo_SemEntrarNasPastasExcluidas_ESemEscreverNada()
    {
        foreach (var f in new[]
                 {
                     "constituicao.md", "federal/leis-ordinarias/2002/lei-1.md", "federal/_fonte/lixo.md", "federal/_lotes/lixo.md", "federal/scripts/x.md",
                     "jurisprudencia/stf/sumulas/s.md", "estaduais/CE/lei.md", "README.md", "federal/dados.json",
                 })
        {
            Escrever(f);
        }

        var antes = Directory.GetFiles(_dir, "*", SearchOption.AllDirectories).Length;
        var snapshot = await Sincronizar();

        snapshot.Arquivos.Select(a => a.CaminhoRelativo).Should()
            .BeEquivalentTo("constituicao.md", "federal/leis-ordinarias/2002/lei-1.md", "jurisprudencia/stf/sumulas/s.md");
        snapshot.Commit.Should().Be("local");
        Directory.GetFiles(_dir, "*", SearchOption.AllDirectories).Should().HaveCount(antes);
    }

    [Fact]
    public async Task CommitDoClone_ELidoSemExecutarGit()
    {
        Escrever("constituicao.md");
        Directory.CreateDirectory(Path.Combine(_dir, ".git", "refs", "heads"));
        File.WriteAllText(Path.Combine(_dir, ".git", "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(_dir, ".git", "refs", "heads", "main"), "abc123\n");

        (await Sincronizar()).Commit.Should().Be("abc123");
    }

    [Fact]
    public async Task PastaInexistenteOuForaDoDiretorio_EErroClaro()
    {
        await ((Func<Task>)(() => Sincronizar(o => o.DiretorioLocal = _dir + "/nao-existe"))).Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => Sincronizar(o => o.PastasIncluidas = ["../fora"]))).Should().ThrowAsync<InvalidOperationException>();
    }
}

public class ObjectStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "storage-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private LocalObjectStorage Local() => new(Options.Create(new NormasIngestaoOptions { Storage = { DiretorioLocal = _dir } }));

    [Fact]
    public async Task Local_GravaLeCopiaEConfereExistencia()
    {
        var storage = Local();

        await storage.PutAsync("juridico/markdown/federal/a.md", "olá"u8.ToArray(), "text/markdown");
        await storage.CopyAsync("juridico/markdown/federal/a.md", "juridico/versoes/x/h.md");

        Encoding.UTF8.GetString((await storage.GetAsync("juridico/versoes/x/h.md"))!).Should().Be("olá");
        (await storage.ExistsAsync("juridico/markdown/federal/a.md")).Should().BeTrue();
        (await storage.GetAsync("juridico/nao-existe.md")).Should().BeNull();
        await storage.DeleteAsync("juridico/markdown/federal/a.md");
        await storage.DeleteAsync("juridico/markdown/federal/a.md");
        (await storage.ExistsAsync("juridico/markdown/federal/a.md")).Should().BeFalse("apagar é idempotente");
    }

    [Fact]
    public async Task Local_RecusaChavesQueSaemDoDiretorio()
    {
        var act = () => Local().PutAsync("../fora.md", [1], "text/plain");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Method { get; private set; }

        public string? Url { get; private set; }

        public string? Authorization { get; private set; }

        public string? PayloadHash { get; private set; }

        public string? CopySource { get; private set; }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method.Method;
            Url = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.TryGetValues("Authorization", out var a) ? a.Single() : null;
            PayloadHash = request.Headers.TryGetValues("x-amz-content-sha256", out var h) ? h.Single() : null;
            CopySource = request.Headers.TryGetValues("x-amz-copy-source", out var c) ? c.Single() : null;

            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(string.Empty) });
        }
    }

    private static IOptions<NormasIngestaoOptions> S3Options(string? endpoint = "https://conta.r2.cloudflarestorage.com", string? key = "AKIA") =>
        Options.Create(new NormasIngestaoOptions
        {
            Storage = { Provedor = "S3", Bucket = "juridico", Endpoint = endpoint, AccessKey = key, SecretKey = "segredo", Regiao = "auto" },
        });

    [Fact]
    public async Task S3_Put_AssinaComSigV4_EUsaEnderecamentoPathStyle()
    {
        var handler = new CapturingHandler();
        var storage = new S3ObjectStorage(new HttpClient(handler), S3Options());
        var body = "conteúdo"u8.ToArray();

        await storage.PutAsync("juridico/markdown/federal/a b.md", body, "text/markdown");

        handler.Method.Should().Be("PUT");
        handler.Url.Should().Be("https://conta.r2.cloudflarestorage.com/juridico/juridico/markdown/federal/a%20b.md");
        handler.PayloadHash.Should().Be(Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant());
        handler.Authorization.Should().StartWith("AWS4-HMAC-SHA256 Credential=AKIA/");
        handler.Authorization.Should().Contain("/auto/s3/aws4_request").And.Contain("SignedHeaders=host;x-amz-content-sha256;x-amz-date");
        handler.Authorization.Should().MatchRegex("Signature=[0-9a-f]{64}$");
        handler.Authorization.Should().NotContain("segredo");
    }

    [Fact]
    public async Task S3_Copy_AssinaOCabecalhoDeOrigem_EGetDe404DevolveNulo()
    {
        var handler = new CapturingHandler();
        var storage = new S3ObjectStorage(new HttpClient(handler), S3Options());

        await storage.CopyAsync("juridico/markdown/a.md", "juridico/versoes/x/h.md");
        handler.CopySource.Should().Be("/juridico/juridico/markdown/a.md");
        handler.Authorization.Should().Contain("x-amz-copy-source");

        handler.Status = HttpStatusCode.NotFound;
        (await storage.GetAsync("juridico/markdown/a.md")).Should().BeNull();
        (await storage.ExistsAsync("juridico/markdown/a.md")).Should().BeFalse();
    }

    [Fact]
    public async Task S3_ErroHttp_NaoVazaCorpoDaResposta()
    {
        var handler = new CapturingHandler { Status = HttpStatusCode.Forbidden };
        var storage = new S3ObjectStorage(new HttpClient(handler), S3Options());

        var act = () => storage.PutAsync("juridico/x.md", [1], "text/plain");

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().Be("O storage respondeu HTTP 403.");
    }

    [Theory]
    [InlineData("http://inseguro.exemplo.com", "AKIA")]
    [InlineData("https://conta.r2.cloudflarestorage.com", null)]
    [InlineData(null, "AKIA")]
    public void S3_ConfiguracaoInvalida_FalhaAlto(string? endpoint, string? key)
    {
        var act = () => new S3ObjectStorage(new HttpClient(new CapturingHandler()), S3Options(endpoint, key));

        act.Should().Throw<InvalidOperationException>();
    }
}

public class NormaPersistenceTests
{
    private sealed class NoTenant : ITenantContext
    {
        public bool HasTenant => false;

        public Guid TenantId => Guid.Empty;

        public void SetTenant(Guid tenantId) => throw new NotSupportedException();
    }

    private static DraftingDbContext NewContext() =>
        new(new DbContextOptionsBuilder<DraftingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new NoTenant());

    private static NormaMetadata Meta(string urn = "urn:x") => new(
        "Lei X", "Lei", "1", 2000, new DateOnly(2000, 1, 1), null, "Ementa", "Vigente", false, "v", urn, "https://planalto.gov.br/x", "federal",
        null, null, null, "civel", ["assunto"], Colecoes.LeisFederais);

    [Fact]
    public async Task NormaVersaoEChunks_PersistemSemTenant_EAConsultaPorEstadoFunciona()
    {
        await using var db = NewContext();
        var repo = new NormaRepository(db);
        var norma = NormaJuridica.Criar(Meta(), "federal/leis/x.md", "juridico/markdown/federal/leis/x.md", "hash1", "ctresb/leis", DateTimeOffset.UtcNow);
        repo.Adicionar(norma);
        repo.Adicionar(new NormaVersao(norma.Id, 1, Meta(), "hash1", norma.ArquivoMarkdownPath, "abc", DateTimeOffset.UtcNow));
        repo.Adicionar([new NormaChunk(norma.Id, 1, 0, "Art. 1º Texto.", null, "1º", null, null, 5, "h", DateTimeOffset.UtcNow)]);
        norma.MarcarIndexada(DateTimeOffset.UtcNow);
        await repo.SalvarAsync();

        var estado = await repo.ListarEstadoAsync();

        estado.Should().ContainSingle().Which.Should().Be(new NormaEstado(norma.Id, "federal/leis/x.md", "hash1", NormaStatus.Indexada, Colecoes.LeisFederais));
        (await repo.ObterPorCaminhoAsync("federal/leis/x.md"))!.Assuntos.Should().Equal("assunto");
        (await repo.UrnExisteAsync("urn:x")).Should().BeTrue();
        (await repo.UrnExisteAsync("urn:y")).Should().BeFalse();
        (await repo.ObterVersaoAsync(norma.Id, 1))!.CommitOrigem.Should().Be("abc");
        db.NormaChunks.Single().Id.Should().Be(NormaChunk.IdPara(norma.Id, 1, 0), "o id do ponto é determinístico");
    }

    [Fact]
    public async Task NormaRemovida_NaoContaComoUrnExistente()
    {
        await using var db = NewContext();
        var repo = new NormaRepository(db);
        var norma = NormaJuridica.Criar(Meta(), "federal/leis/x.md", "k", "h", "o", DateTimeOffset.UtcNow);
        repo.Adicionar(norma);
        norma.MarcarRemovida(DateTimeOffset.UtcNow);
        await repo.SalvarAsync();

        (await repo.UrnExisteAsync("urn:x")).Should().BeFalse();
    }

    [Fact]
    public async Task Jobs_PendenteMaisAntigoPrimeiro_ESomenteOPedidoManualFicaPendente()
    {
        await using var db = NewContext();
        var repo = new NormaRepository(db);
        var agora = DateTimeOffset.UtcNow;
        var antigo = IngestaoJob.Solicitar(IngestaoJobTipo.Incremental, agora.AddMinutes(-5));
        repo.Adicionar(antigo);
        repo.Adicionar(IngestaoJob.Solicitar(IngestaoJobTipo.Incremental, agora));
        repo.Adicionar(new IngestaoJob(IngestaoJobTipo.Completa, "federal", agora.AddHours(-1)));
        await repo.SalvarAsync();

        (await repo.ObterJobPendenteAsync())!.Id.Should().Be(antigo.Id);
        (await repo.ListarJobsAsync(10)).Should().HaveCount(3);
        (await repo.UltimaSincronizacaoAsync()).Should().Be(agora.AddHours(-1));
    }
}
