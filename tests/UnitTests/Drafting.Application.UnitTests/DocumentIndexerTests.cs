using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class DocumentIndexerTests
{
    private static readonly Guid Documento = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static DocumentIndexer Indexer(FakeVectorStore store, FakeEmbeddings? embeddings = null) =>
        new(embeddings ?? new FakeEmbeddings(), store, NullLogger<DocumentIndexer>.Instance);

    private static IndexDocumentRequest Request(params (int? Pagina, string Texto)[] paginas) =>
        new(Ids.Tenant, Ids.User, Documento, "contrato.pdf", "contrato", "Civel", "TJSP", null,
            "https://exemplo.com.br/contrato", ["rescisao", "multa"], "escritorio",
            paginas.Select(p => new IndexPage(p.Pagina, p.Texto)).ToList());

    [Fact]
    public async Task GravaOsTrechosComTenantVisibilidadeETagsNoPayload()
    {
        var store = new FakeVectorStore();

        var result = await Indexer(store).IndexAsync(Request((1, "Cláusula primeira. O contrato vale."), (2, "Cláusula segunda.")));

        result.IsSuccess.Should().BeTrue();
        result.Value.Trechos.Should().Be(2);
        store.Upserted.Should().HaveCount(2);
        store.Upserted.Should().OnlyContain(p => p.TenantId == Ids.Tenant);
        var primeiro = store.Upserted[0];
        primeiro.Meta.NomeArquivo.Should().Be("contrato.pdf");
        primeiro.Meta.Pagina.Should().Be(1);
        primeiro.Meta.Area.Should().Be("civel");
        primeiro.Meta.Tribunal.Should().Be("TJSP");
        primeiro.Meta.UsuarioId.Should().Be(Ids.User);
        primeiro.Meta.Visibilidade.Should().Be("escritorio");
        primeiro.Meta.Tags.Should().Equal("rescisao", "multa");
        primeiro.Meta.HashConteudo.Should().NotBeNullOrEmpty();
        Guid.TryParse(primeiro.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task DocumentoSemNumeroDePagina_GravaPaginaNula()
    {
        var store = new FakeVectorStore();

        await Indexer(store).IndexAsync(Request((null, "Texto de um DOCX, sem paginação.")));

        store.Upserted.Single().Meta.Pagina.Should().BeNull();
    }

    [Fact]
    public async Task ReindexarSubstituiOsPontosAntigosDoMesmoDocumento()
    {
        var store = new FakeVectorStore();

        await Indexer(store).IndexAsync(Request((1, "Texto.")));

        store.Ensured.Should().Be(1);
        store.Deleted.Should().ContainSingle().Which.Should().Be((Ids.Tenant, Documento));
    }

    [Fact]
    public async Task EmbeddingsSaoPedidosEmLotes()
    {
        var embeddings = new FakeEmbeddings();
        var longo = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Parágrafo {i}: " + new string('x', 1000)));

        var result = await Indexer(new FakeVectorStore(), embeddings).IndexAsync(Request((1, longo)));

        result.IsSuccess.Should().BeTrue();
        embeddings.Calls.Should().HaveCountGreaterThan(1);
        embeddings.Calls.Should().OnlyContain(lote => lote.Count <= 16);
    }

    [Fact]
    public void IdsDosPontosSaoDeterministicosEDependemDoTenant()
    {
        var outroTenant = Guid.NewGuid();

        DocumentIndexer.PointId(Ids.Tenant, Documento, 1, 0).Should().Be(DocumentIndexer.PointId(Ids.Tenant, Documento, 1, 0));
        DocumentIndexer.PointId(Ids.Tenant, Documento, 1, 0).Should().NotBe(DocumentIndexer.PointId(Ids.Tenant, Documento, 1, 1));
        DocumentIndexer.PointId(Ids.Tenant, Documento, 1, 0).Should().NotBe(DocumentIndexer.PointId(outroTenant, Documento, 1, 0));
        DocumentIndexer.PointId(Ids.Tenant, Documento, null, 0).Should().NotBe(DocumentIndexer.PointId(Ids.Tenant, Documento, 1, 0));
    }

    [Fact]
    public void TextoCurtoViraUmTrecho()
    {
        DocumentIndexer.Split("Um texto curto.").Select(t => t.Text).Should().Equal("Um texto curto.");
    }

    [Fact]
    public void TextoLongoQuebraEmTrechosLimitadosComSobreposicao()
    {
        var paragrafo = string.Join(' ', Enumerable.Range(0, 400).Select(i => $"Frase número {i}."));

        var trechos = DocumentIndexer.Split(paragrafo).Select(t => t.Text).ToList();

        trechos.Should().HaveCountGreaterThan(1);
        trechos.Should().OnlyContain(t => t.Length <= DocumentIndexer.ChunkChars);
        trechos.Take(trechos.Count - 1).Should().OnlyContain(t => t.Length >= DocumentIndexer.MinChunkChars);
        // Sobreposição: o fim de um trecho reaparece no começo do seguinte.
        var cauda = trechos[0][^40..];
        trechos[1].Should().Contain(cauda.Trim().Split(' ')[^1]);
        // Nada se perde: todas as frases aparecem em algum trecho.
        for (var i = 0; i < 400; i += 37)
        {
            trechos.Should().Contain(t => t.Contains($"Frase número {i}."));
        }
    }

    [Fact]
    public void PrefereCortarEmFimDeParagrafo()
    {
        var texto = new string('a', 900) + ".\n\n" + new string('b', 900);

        var trechos = DocumentIndexer.Split(texto).Select(t => t.Text).ToList();

        trechos[0].Should().EndWith("a.");
    }

    [Fact]
    public void NaoCortaNoMeioDeUmArtigo_CortaAntesDoCabecalhoDoProximo()
    {
        var art1 = "Art. 1º " + string.Join(' ', Enumerable.Repeat("texto do primeiro artigo.", 32));   // ~830 caracteres
        var art2 = "Art. 2º " + string.Join(' ', Enumerable.Repeat("texto do segundo artigo.", 40));
        var texto = art1 + "\nArt. 2º " + art2;

        var trechos = DocumentIndexer.Split(texto).Select(t => t.Text).ToList();

        trechos[0].Should().StartWith("Art. 1º").And.NotContain("segundo artigo");
        trechos[1].Should().Contain("Art. 2º");
    }

    [Fact]
    public void CadaTrechoLevaAUltimaReferenciaVista()
    {
        var texto = "CLÁUSULA 3 " + string.Join(' ', Enumerable.Repeat("o contratante pagará a multa.", 60));

        var trechos = DocumentIndexer.Split(texto).ToList();

        trechos.Should().OnlyContain(t => t.Reference != null && t.Reference.StartsWith("CLÁUSULA 3"));
    }

    [Fact]
    public async Task DocumentoSemTextoUtil_EhRecusado()
    {
        var store = new FakeVectorStore();

        var result = await Indexer(store).IndexAsync(Request((1, "   \n  ")));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
        store.Upserted.Should().BeEmpty();
    }

    [Fact]
    public async Task SemPaginas_EhErro()
    {
        var result = await Indexer(new FakeVectorStore()).IndexAsync(Request());

        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_FIELD_REQUIRED));
    }
}
