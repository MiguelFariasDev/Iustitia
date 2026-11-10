using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Advocacia.Modules.Drafting.Domain.Fila;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class IngestionTests
{
    private sealed class FakeExtractor : IDocumentTextExtractor
    {
        /// <summary>Se definido, é devolvido sempre; senão o "texto extraído" é o próprio conteúdo do arquivo de teste.</summary>
        public Result<IReadOnlyList<ExtractedPage>>? Resultado { get; set; }

        public Result<IReadOnlyList<ExtractedPage>> Extract(Stream content, string fileName)
        {
            if (Resultado is not null)
            {
                return Resultado;
            }

            using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);

            return Result.Success<IReadOnlyList<ExtractedPage>>([new ExtractedPage(1, reader.ReadToEnd())]);
        }
    }

    private sealed class Harness
    {
        public FakeClock Clock { get; } = new();

        public FakeQueue Queue { get; }

        public FakeIndexedDocuments Documents { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeVectorStore Store { get; } = new();

        public FakeExtractor Extractor { get; } = new();

        public Harness() => Queue = new FakeQueue(Clock);

        public DocumentIngestionService For(FakeCurrentUser user, bool enabled = true, int maxMb = 20, int maxPages = 500) =>
            new(
                Options.Create(new DraftingOptions { Enabled = enabled }),
                Options.Create(new IngestaoOptions { MaxMegabytes = maxMb, MaxPaginas = maxPages }),
                Extractor, Documents, Queue, Store, UnitOfWork, user, Clock);
    }

    private static readonly FakeCurrentUser Dona = new(Ids.Tenant, Ids.User, "Lawyer");
    private static readonly FakeCurrentUser Outro = new(Ids.Tenant, Ids.OtherUser, "Lawyer");
    private static readonly FakeCurrentUser Socio = new(Ids.Tenant, Ids.OtherUser, "Partner");

    private static Stream Bytes(string text = "Art. 186 do Código Civil: aquele que causar dano comete ato ilícito.") => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static UploadDocumentoRequest Meta(string visibilidade = "escritorio") =>
        new("jurisprudencia", "civel", "tjsp", new DateOnly(2024, 5, 1), "https://exemplo.com.br/acordao", ["dano moral", "Dano Moral", "consumidor"], visibilidade);

    [Fact]
    public async Task Upload_ExtraiValidaGravaMetadadosEEnfileiraAIndexacao_SemUsarGpu()
    {
        var h = new Harness();

        var result = await h.For(Dona).EnviarAsync(Meta(), Bytes(), "acordao.pdf", 100);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pendente");
        var doc = h.Documents.Items.Should().ContainSingle().Subject;
        doc.TenantId.Should().Be(Ids.Tenant);
        doc.UsuarioId.Should().Be(Ids.User);
        doc.NomeArquivo.Should().Be("acordao.pdf");
        doc.TipoDocumento.Should().Be("jurisprudencia");
        doc.Area.Should().Be("civel");
        doc.Tribunal.Should().Be("TJSP");
        doc.Visibilidade.Should().Be(DocumentVisibility.Escritorio);
        doc.TagsJson.Should().Be("[\"dano moral\",\"consumidor\"]");
        doc.PaginasJson.Should().Contain("Art. 186");
        doc.HashConteudo.Should().HaveLength(64);
        var item = h.Queue.Items.Should().ContainSingle().Subject;
        item.Tipo.Should().Be(QueueItemKind.Indexacao);
        item.ReferenciaId.Should().Be(doc.Id.Value);
        h.Store.Upserted.Should().BeEmpty("o upload não gera embeddings; a indexação roda no worker, com a GPU ligada");
    }

    [Fact]
    public async Task Upload_DeConteudoRepetido_EhRecusadoPorHash()
    {
        var h = new Harness();
        var service = h.For(Dona);
        await service.EnviarAsync(Meta(), Bytes(), "a.pdf", 100);

        var segundo = await service.EnviarAsync(Meta(), Bytes(), "copia-com-outro-nome.pdf", 100);

        segundo.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_DOCUMENT_DUPLICATE));
        h.Documents.Items.Should().ContainSingle();
        h.Queue.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Upload_AcimaDoLimite_ERecusado_MesmoSeOTamanhoDeclaradoMentir()
    {
        var h = new Harness();
        var service = h.For(Dona, maxMb: 1);

        var declarado = await service.EnviarAsync(Meta(), Bytes(), "a.pdf", 2 * 1024 * 1024);
        var mentiroso = await service.EnviarAsync(Meta(), new MemoryStream(new byte[(1024 * 1024) + 1]), "b.pdf", 10);

        declarado.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_FILE_TOO_LARGE));
        mentiroso.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_FILE_TOO_LARGE));
        h.Documents.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_DeArquivoIlegivelOuEscaneado_ERecusadoComMensagemClara()
    {
        var h = new Harness();
        h.Extractor.Resultado = Result.Failure<IReadOnlyList<ExtractedPage>>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE));

        var result = await h.For(Dona).EnviarAsync(Meta(), Bytes(), "escaneado.pdf", 100);

        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
        h.Documents.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_DeTextoSoComEspacos_ERecusado()
    {
        var h = new Harness();
        h.Extractor.Resultado = Result.Success<IReadOnlyList<ExtractedPage>>([new ExtractedPage(1, "  \n ​ ")]);

        var result = await h.For(Dona).EnviarAsync(Meta(), Bytes(), "vazio.pdf", 100);

        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
    }

    [Theory]
    [InlineData("ficcao", "civel", null, null, "DRAFTING_FIELD_REQUIRED")]
    [InlineData("lei", "astrologia", null, null, "DRAFTING_INVALID_AREA")]
    [InlineData("lei", null, "javascript:alert(1)", null, "DRAFTING_FIELD_REQUIRED")]
    [InlineData("lei", null, null, "talvez", "DRAFTING_FIELD_REQUIRED")]
    public async Task Upload_ComMetadadosInvalidos_ERecusado(string tipo, string? area, string? url, string? visibilidade, string codigo)
    {
        var h = new Harness();

        var result = await h.For(Dona).EnviarAsync(
            new UploadDocumentoRequest(tipo, area, null, null, url, null, visibilidade), Bytes(), "a.pdf", 100);

        result.Error.Code.Should().Be(codigo);
        h.Documents.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_ComRecursoDesligado_NaoGrava()
    {
        var h = new Harness();

        var result = await h.For(Dona, enabled: false).EnviarAsync(Meta(), Bytes(), "a.pdf", 100);

        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_DISABLED));
    }

    [Fact]
    public async Task NomeDoArquivoComCaminhoOuCaracteresDeControle_EhLimpo()
    {
        var h = new Harness();

        await h.For(Dona).EnviarAsync(Meta(), Bytes(), "..\\..\\etc/passwd\u0007.pdf", 100);

        h.Documents.Items.Single().NomeArquivo.Should().Be("passwd.pdf");
    }

    [Fact]
    public async Task DocumentoPrivadoDeOutroUsuario_NaoApareceNaListaNemNaConsulta_NemParaSocio()
    {
        var h = new Harness();
        await h.For(Dona).EnviarAsync(Meta("privado"), Bytes("privado"), "p.pdf", 100);
        await h.For(Dona).EnviarAsync(Meta("escritorio"), Bytes("publico"), "e.pdf", 100);
        var privado = h.Documents.Items.Single(d => d.NomeArquivo == "p.pdf").Id.Value;

        (await h.For(Outro).ListarAsync(1, 20)).Value.Should().ContainSingle().Which.NomeArquivo.Should().Be("e.pdf");
        (await h.For(Dona).ListarAsync(1, 20)).Value.Should().HaveCount(2);
        (await h.For(Outro).ObterAsync(privado)).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_DOCUMENT_NOT_FOUND));
        (await h.For(Socio).ObterAsync(privado)).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_DOCUMENT_NOT_FOUND));
        (await h.For(Dona).ObterAsync(privado)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Remover_ApagaDoIndiceVetorialEDoBanco_SoQuemPode()
    {
        var h = new Harness();
        await h.For(Dona).EnviarAsync(Meta(), Bytes(), "e.pdf", 100);
        var id = h.Documents.Items.Single().Id.Value;

        (await h.For(Outro).RemoverAsync(id)).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_DOCUMENT_NOT_FOUND));
        h.Documents.Items.Should().ContainSingle();

        // Sócio pode remover documento do ESCRITÓRIO; o dono sempre pode.
        (await h.For(Socio).RemoverAsync(id)).IsSuccess.Should().BeTrue();
        h.Documents.Items.Should().BeEmpty();
        h.Store.Deleted.Should().ContainSingle().Which.Should().Be((Ids.Tenant, id));
    }

    [Fact]
    public async Task Remover_DocumentoPrivadoSoPeloDono()
    {
        var h = new Harness();
        await h.For(Dona).EnviarAsync(Meta("privado"), Bytes(), "p.pdf", 100);
        var id = h.Documents.Items.Single().Id.Value;

        (await h.For(Socio).RemoverAsync(id)).IsFailure.Should().BeTrue();
        (await h.For(Dona).RemoverAsync(id)).IsSuccess.Should().BeTrue();
    }

    // ------------------------------------------------------------ worker de indexação

    [Fact]
    public async Task Indexacao_GeraEmbeddingsGravaNoQdrantELimpaOTextoDoBancoRelacional()
    {
        var h = new Harness();
        await h.For(Dona).EnviarAsync(Meta(), Bytes(), "acordao.pdf", 100);
        var doc = h.Documents.Items.Single();
        var handler = NewHandler(h);

        var outcome = await handler.ProcessarAsync(doc.Id.Value, 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Concluido);
        doc.Status.Should().Be(IndexedDocumentStatus.Indexado);
        doc.Trechos.Should().BeGreaterThan(0);
        doc.PaginasJson.Should().BeNull("depois de indexado o texto vive só no banco vetorial");
        var ponto = h.Store.Upserted.First();
        ponto.TenantId.Should().Be(Ids.Tenant);
        ponto.Meta.UsuarioId.Should().Be(Ids.User);
        ponto.Meta.Visibilidade.Should().Be("escritorio");
        ponto.Meta.DocumentoOrigemId.Should().Be(doc.Id.Value);
    }

    [Fact]
    public async Task Indexacao_FalhaTransitoria_ReagendaAteEsgotarTentativas()
    {
        var h = new Harness();
        await h.For(Dona).EnviarAsync(Meta(), Bytes(), "acordao.pdf", 100);
        var doc = h.Documents.Items.Single();
        var handler = NewHandler(h, new FailingEmbeddings());

        var primeira = await handler.ProcessarAsync(doc.Id.Value, 1);
        doc.Status.Should().Be(IndexedDocumentStatus.Pendente);
        var ultima = await handler.ProcessarAsync(doc.Id.Value, 3);

        primeira.Kind.Should().Be(WorkOutcomeKind.Reagendar);
        ultima.Kind.Should().Be(WorkOutcomeKind.Falhou);
        doc.Status.Should().Be(IndexedDocumentStatus.Falhou);
        doc.Erro.Should().Contain("LlmServiceException");
    }

    [Fact]
    public async Task Indexacao_DeDocumentoRemovidoNaEspera_NaoEhFalha()
    {
        var h = new Harness();
        var handler = NewHandler(h);

        var outcome = await handler.ProcessarAsync(Guid.NewGuid(), 1);

        outcome.Kind.Should().Be(WorkOutcomeKind.Cancelado);
    }

    private static IndexacaoWorkHandler NewHandler(Harness h, IEmbeddingService? embeddings = null) =>
        new(
            h.Documents,
            new DocumentIndexer(embeddings ?? new FakeEmbeddings(), h.Store, NullLogger<DocumentIndexer>.Instance),
            h.UnitOfWork,
            h.Clock,
            Options.Create(new FilaOptions { MaxTentativas = 3 }),
            NullLogger<IndexacaoWorkHandler>.Instance);

    private sealed class FailingEmbeddings : IEmbeddingService
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default) =>
            throw new LlmServiceException("O Ollama respondeu HTTP 503.");
    }
}
