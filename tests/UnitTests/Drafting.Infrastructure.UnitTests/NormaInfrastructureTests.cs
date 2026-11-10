using System.Net;
using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Normas;
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
    private static NormaPayload Payload(string? uf = "CE") => new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "urn:lex:br:federal:lei:2002-01-10;10406", "Lei nº 10.406 de 10/01/2002",
        "Lei Ordinária", "10406", new DateOnly(2002, 1, 10), "Vigente", false, "compilada", 3, "estadual", uf, null, "civel", null,
        ["responsabilidade civil", "dano moral"], Colecoes.LegislacaoEstadual, "juridico/markdown/estaduais/CE/x.md",
        "https://normas.leg.br/x", null, "TÍTULO I > CAPÍTULO II", "186", "§ 1º", "I", "hash1", "Art. 186. Aquele que...");

    [Fact]
    public void Filtro_Padrao_NaoRestringeNada_NemExcluiRevogadas()
    {
        NormaQdrantStore.BuildFilter(new NormaFiltro()).Must.Should().BeEmpty();
    }

    [Fact]
    public void Filtro_ExcluirRevogadasUfMunicipioETipo_SaoCondicoesObrigatorias()
    {
        var filter = NormaQdrantStore.BuildFilter(new NormaFiltro(
            Tipos: ["Lei Ordinária"], Ufs: ["ce"], Municipios: ["Fortaleza"], IncluirRevogadas: false,
            DataDe: new DateOnly(2000, 1, 1)));

        filter.Must.Should().Contain(c => c.Field != null && c.Field.Key == "revogada");
        filter.Must.Should().Contain(c => c.Field != null && c.Field.Key == "tipo");
        filter.Must.Should().Contain(c => c.Field != null && c.Field.Key == "data");

        // UF e município são OU aninhado: "tem a UF pedida" OU "a esfera não tem UF/município" (federal segue valendo).
        var aninhados = filter.Must.Where(c => c.Filter != null).Select(c => c.Filter).ToList();
        aninhados.Should().HaveCount(2).And.OnlyContain(f => f.Should.Count == 2);
        aninhados.SelectMany(f => f.Should).Select(c => c.Field.Key).Should().Contain(["uf", "municipio", "esfera"]);
    }

    [Fact]
    public void Filtro_AreaSoRestringeQuandoEstrita_PorqueOYamlNaoTemArea()
    {
        NormaQdrantStore.BuildFilter(new NormaFiltro(Area: "civel")).Must.Should().BeEmpty();
        NormaQdrantStore.BuildFilter(new NormaFiltro(Area: "civel", AreaEstrita: true)).Must
            .Should().ContainSingle(c => c.Field != null && c.Field.Key == "area");
    }

    [Fact]
    public void PayloadCompleto_IdaEVolta_ComTodosOsCamposExigidos()
    {
        var id = Guid.NewGuid();
        var point = NormaQdrantStore.ToPoint(new NormaVectorPoint(id, [0.1f, 0.2f], Payload()));

        foreach (var chave in new[]
                 {
                     "normaJuridicaId", "urn", "titulo", "tipo", "numero", "data", "situacao", "revogada", "versaoTexto", "esfera", "uf",
                     "area", "assuntos", "arquivoMarkdownPath", "secao", "artigo", "paragrafo", "inciso", "hashConteudo", "texto", "versao",
                     "fonteOficialUrl", "colecao",
                 })
        {
            point.Payload.Should().ContainKey(chave);
        }

        point.Payload["data"].StringValue.Should().Be("2002-01-10T00:00:00Z");
        point.Payload["revogada"].BoolValue.Should().BeFalse();
        point.Payload["versao"].IntegerValue.Should().Be(3);
        point.Payload.Should().NotContainKey("municipio", "campo nulo não vai ao payload");

        var scored = new ScoredPoint { Id = id, Score = 0.77f };
        foreach (var (key, value) in point.Payload)
        {
            scored.Payload[key] = value;
        }

        var hit = NormaQdrantStore.ToHit(scored);

        hit.PointId.Should().Be(id.ToString());
        hit.Score.Should().BeApproximately(0.77, 0.001);
        hit.Texto.Should().Be("Art. 186. Aquele que...");
        var norma = hit.Meta.Norma!;
        norma.Titulo.Should().Be("Lei nº 10.406 de 10/01/2002");
        norma.Artigo.Should().Be("186");
        norma.Paragrafo.Should().Be("§ 1º");
        norma.Inciso.Should().Be("I");
        norma.VersaoNumero.Should().Be(3);
        norma.Uf.Should().Be("CE");
        norma.Urn.Should().Contain("10406");
        norma.ArquivoMarkdownPath.Should().Be("juridico/markdown/estaduais/CE/x.md");
        hit.Meta.UrlOrigem.Should().Be("https://normas.leg.br/x");
        hit.Meta.Tags.Should().Equal("responsabilidade civil", "dano moral");
    }

    [Fact]
    public void PayloadSemUf_NaoGravaUf()
    {
        NormaQdrantStore.ToPoint(new NormaVectorPoint(Guid.NewGuid(), [0.1f], Payload(uf: null))).Payload.Should().NotContainKey("uf");
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

        linhas.Should().Contain(["/constituicao.md", "/federal/", "/jurisprudencia/", "/tratados/", "/reguladores/", "/estaduais/CE/", "/municipais/CE/fortaleza/"]);
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
            Url = request.RequestUri!.ToString();
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
