using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class DocumentIndexerTests
{
    private static DocumentIndexer Indexer(FakeVectorStore store) =>
        new(new FakeEmbeddings(), store, new FakeCurrentUser(), NullLogger<DocumentIndexer>.Instance);

    private static IndexDocumentRequest Request(params (int Pagina, string Texto)[] paginas) =>
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "contrato.pdf", "contrato", "Civel", "TJSP", null,
            "https://exemplo.com.br/contrato", paginas.Select(p => new IndexPage(p.Pagina, p.Texto)).ToList());

    [Fact]
    public async Task GravaOsTrechosComTenantNoPayloadEOMetadadosCompletos()
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
        primeiro.Meta.HashConteudo.Should().NotBeNullOrEmpty();
        Guid.TryParse(primeiro.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task ReindexarSubstituiOsPontosAntigosDoMesmoDocumento()
    {
        var store = new FakeVectorStore();

        await Indexer(store).IndexAsync(Request((1, "Texto.")));

        store.Ensured.Should().Be(1);
        store.Deleted.Should().ContainSingle().Which.Should().Be((Ids.Tenant, Guid.Parse("44444444-4444-4444-4444-444444444444")));
    }

    [Fact]
    public void IdsDosPontosSaoDeterministicosEDependemDoTenant()
    {
        var doc = Guid.NewGuid();
        var outroTenant = Guid.NewGuid();

        DocumentIndexer.PointId(Ids.Tenant, doc, 1, 0).Should().Be(DocumentIndexer.PointId(Ids.Tenant, doc, 1, 0));
        DocumentIndexer.PointId(Ids.Tenant, doc, 1, 0).Should().NotBe(DocumentIndexer.PointId(Ids.Tenant, doc, 1, 1));
        DocumentIndexer.PointId(Ids.Tenant, doc, 1, 0).Should().NotBe(DocumentIndexer.PointId(outroTenant, doc, 1, 0));
    }

    [Fact]
    public void TextoCurtoViraUmTrecho()
    {
        DocumentIndexer.Split("Um texto curto.").Should().Equal("Um texto curto.");
    }

    [Fact]
    public void TextoLongoQuebraEmTrechosLimitadosComSobreposicao()
    {
        var paragrafo = string.Join(' ', Enumerable.Range(0, 400).Select(i => $"Frase número {i}."));

        var trechos = DocumentIndexer.Split(paragrafo).ToList();

        trechos.Should().HaveCountGreaterThan(1);
        trechos.Should().OnlyContain(t => t.Length <= DocumentIndexer.ChunkChars);
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
        var texto = new string('a', 800) + ".\n\n" + new string('b', 800);

        var trechos = DocumentIndexer.Split(texto).ToList();

        trechos[0].Should().EndWith("a.");
    }

    [Fact]
    public async Task RejeitaDocumentoSemPaginasOuComUrlInvalida()
    {
        var store = new FakeVectorStore();

        var semPaginas = await Indexer(store).IndexAsync(Request());
        var urlRuim = await Indexer(store).IndexAsync(Request((1, "x")) with { UrlOrigem = "javascript:alert(1)" });

        semPaginas.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_FIELD_REQUIRED));
        urlRuim.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_FIELD_REQUIRED));
        store.Upserted.Should().BeEmpty();
    }

    [Fact]
    public async Task LimitaONumeroDePaginas()
    {
        var paginas = Enumerable.Range(1, DocumentIndexer.MaxPages + 1).Select(i => (i, "x")).ToArray();

        var result = await Indexer(new FakeVectorStore()).IndexAsync(Request(paginas));

        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_FIELD_TOO_LONG));
    }
}
