using System.Net;
using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Conversao;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;
using Advocacia.Modules.Drafting.Infrastructure.Exportacao;
using Advocacia.Modules.Drafting.Infrastructure.Ocr;
using Advocacia.Modules.Drafting.Infrastructure.Storage;
using Advocacia.Modules.Drafting.Infrastructure.Transcricao;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

internal sealed class OcrFalso(bool disponivel = true, double confianca = 0.9) : IOcrService
{
    public int Chamadas { get; private set; }

    public Task<bool> DisponivelAsync(CancellationToken cancellationToken = default) => Task.FromResult(disponivel);

    public Task<Result<ResultadoOcr>> ReconhecerAsync(byte[] imagem, CancellationToken cancellationToken = default)
    {
        Chamadas++;

        return Task.FromResult(Result.Success(new ResultadoOcr("TEXTO LIDO POR OCR", confianca)));
    }
}

public class ConversoresTests
{
    private static readonly IOptions<AnexosOptions> Anexos = Options.Create(new AnexosOptions { MaxLinhasPlanilha = 5 });
    private static readonly IOptions<ConversaoOptions> Conversao = Options.Create(new ConversaoOptions());

    private static DocumentoParaConverter Doc(byte[] bytes, string nome, FormatoDocumento formato) => new(bytes, nome, formato);

    // ------------------------------------------------------------ DOCX

    private static byte[] Docx(params OpenXmlElement[] elementos)
    {
        using var memoria = new MemoryStream();
        using (var docx = WordprocessingDocument.Create(memoria, WordprocessingDocumentType.Document))
        {
            docx.AddMainDocumentPart().Document = new Document(new Body(elementos));
        }

        return memoria.ToArray();
    }

    private static Paragraph P(string texto, string? estilo = null) =>
        new(estilo is null ? new ParagraphProperties() : new ParagraphProperties(new ParagraphStyleId { Val = estilo }), new Run(new Text(texto)));

    [Fact]
    public async Task Docx_TitulosListasETabelas_ViramMarkdown_SemOTextoExcluido()
    {
        var lista = new Paragraph(
            new ParagraphProperties(new NumberingProperties(new NumberingLevelReference { Val = 1 }, new NumberingId { Val = 1 })), new Run(new Text("subitem")));
        var revisado = new Paragraph(
            new Run(new Text("Valor: ")), new DeletedRun(new Run(new DeletedText("R$ 1,00"))), new InsertedRun(new Run(new Text("R$ 2,00"))));
        var tabela = new Table(
            new TableRow(new TableCell(P("Parcela")), new TableCell(P("Valor|total"))), new TableRow(new TableCell(P("1")), new TableCell(P("100"))));

        var resultado = await new DocxConversor().ConverterAsync(Doc(Docx(P("Objeto", "Ttulo1"), P("Parágrafo."), lista, revisado, tabela), "c.docx", FormatoDocumento.Docx));

        var md = resultado.Value.Blocos.Single().Markdown;
        md.Should().Contain("## Objeto").And.Contain("Parágrafo.").And.Contain("  - subitem").And.Contain("| Parcela | Valor\\|total |")
            .And.Contain("| --- | --- |").And.Contain("Valor: R$ 2,00").And.NotContain("R$ 1,00");
        resultado.Value.Avisos.Should().ContainSingle(a => a.Contains("controle de alterações"));
    }

    [Fact]
    public async Task Docx_Corrompido_ERecusado_SemExcecao() =>
        (await new DocxConversor().ConverterAsync(Doc([0x50, 0x4B, 3, 4, 1, 2], "c.docx", FormatoDocumento.Docx))).Error.Code
            .Should().Be(nameof(ErrorCode.DRAFTING_UNSUPPORTED_FILE));

    // ------------------------------------------------------------ PDF

    private static byte[] Pdf(params string?[] paginas)
    {
        var builder = new PdfDocumentBuilder();
        var fonte = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var texto in paginas)
        {
            var pagina = builder.AddPage(595, 842);
            if (texto is null)
            {
                // PNG 1x1: página só com imagem, como um escaneado.
                pagina.AddPng(
                    Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="),
                    new PdfRectangle(0, 0, 595, 842));
                continue;
            }

            var y = 800;
            foreach (var linha in texto.Split('\n'))
            {
                pagina.AddText(linha, 12, new PdfPoint(40, y), fonte);
                y -= 20;
            }
        }

        return builder.Build();
    }

    private static PdfConversor PdfConversor(IOcrService ocr, AnexosOptions? anexos = null) =>
        new(ocr, Conversao, Options.Create(new OcrOptions()), Options.Create(anexos ?? new AnexosOptions()), NullLogger<PdfConversor>.Instance);

    private const string PaginaDeTexto = "CONTRATO DE PRESTACAO DE SERVICOS\nClausula 5 - Pagamento\nA contratante pagara o valor acordado.";

    [Fact]
    public async Task Pdf_ComTexto_PreservaPaginaELinhas_SemUsarOcr()
    {
        var ocr = new OcrFalso();

        var resultado = await PdfConversor(ocr).ConverterAsync(Doc(Pdf(PaginaDeTexto, PaginaDeTexto.Replace("5", "6")), "c.pdf", FormatoDocumento.Pdf));

        resultado.Value.Blocos.Select(b => b.Pagina).Should().Equal(1, 2);
        resultado.Value.Blocos[0].Markdown.Should().Contain("Clausula 5 - Pagamento\n").And.Contain("A contratante");
        resultado.Value.Origem.Should().Be(OrigemTexto.Nativo);
        ocr.Chamadas.Should().Be(0);
    }

    [Fact]
    public async Task Pdf_Misto_FazOcrSoDaPaginaSemTexto()
    {
        var ocr = new OcrFalso(confianca: 0.7);

        var resultado = await PdfConversor(ocr).ConverterAsync(Doc(Pdf(PaginaDeTexto, null), "c.pdf", FormatoDocumento.Pdf));

        ocr.Chamadas.Should().Be(1);
        resultado.Value.Blocos.Select(b => (b.Pagina, b.Origem)).Should().Equal((1, OrigemTexto.Nativo), (2, OrigemTexto.Ocr));
        resultado.Value.Origem.Should().Be(OrigemTexto.Ocr);
        resultado.Value.Confianca.Should().Be(0.7);
    }

    [Fact]
    public async Task Pdf_SoEscaneado_SemOcrInstalado_DizPorQue_EMistoSegueComAviso()
    {
        var semOcr = new OcrFalso(disponivel: false);

        (await PdfConversor(semOcr).ConverterAsync(Doc(Pdf(null, null), "c.pdf", FormatoDocumento.Pdf))).Error.Code
            .Should().Be(nameof(ErrorCode.DRAFTING_CONVERSION_UNAVAILABLE));
        (await PdfConversor(semOcr).ConverterAsync(Doc(Pdf(PaginaDeTexto, null), "c.pdf", FormatoDocumento.Pdf))).Value.Avisos
            .Should().ContainSingle(a => a.Contains("não puderam ser lidas: 2"));
    }

    [Fact]
    public async Task Pdf_LimitesDePaginasEDeOcr_SaoAplicadosEAvisados()
    {
        (await PdfConversor(new OcrFalso(), new AnexosOptions { MaxPaginas = 1 }).ConverterAsync(Doc(Pdf(PaginaDeTexto, PaginaDeTexto), "c.pdf", FormatoDocumento.Pdf)))
            .IsFailure.Should().BeTrue();

        var ocr = new OcrFalso();
        var parcial = await PdfConversor(ocr, new AnexosOptions { MaxPaginasOcr = 1 }).ConverterAsync(Doc(Pdf(null, null, null), "c.pdf", FormatoDocumento.Pdf));
        ocr.Chamadas.Should().Be(1);
        parcial.Value.Avisos.Should().ContainSingle(a => a.Contains("ficaram de fora: 2, 3"));
    }

    [Fact]
    public async Task Pdf_Corrompido_ERecusado() =>
        (await PdfConversor(new OcrFalso()).ConverterAsync(Doc("%PDF-1.7 lixo"u8.ToArray(), "c.pdf", FormatoDocumento.Pdf))).IsFailure.Should().BeTrue();

    // ------------------------------------------------------------ planilha

    private static PlanilhaConversor Planilha() => new(new LibreOfficeConversor(Conversao), Anexos);

    [Fact]
    public async Task Csv_DetectaSeparador_RespeitaAspas_ELimitaLinhas()
    {
        const string csv = "Data;Descrição;Valor\n10/03/2024;\"Parcela; com ponto e vírgula\";100\n;;\n\"a \"\"b\"\"\";2;3\nx;y;z\nx;y;z\nx;y;z\n";

        var resultado = await Planilha().ConverterAsync(Doc(Encoding.UTF8.GetBytes(csv), "e.csv", FormatoDocumento.Planilha));

        resultado.Value.Blocos.Single().Markdown.Should().StartWith("| Data | Descrição | Valor |\n| --- | --- | --- |\n| 10/03/2024 | Parcela; com ponto e vírgula | 100 |\n| a \"b\" | 2 | 3 |");
        resultado.Value.Avisos.Should().ContainSingle(a => a.Contains("passou de 5 linhas"));
    }

    [Fact]
    public async Task Csv_EmLatin1_NaoPerdeAcento() =>
        (await Planilha().ConverterAsync(Doc(Encoding.Latin1.GetBytes("Descrição;Situação\nação;não"), "e.csv", FormatoDocumento.Planilha)))
            .Value.Blocos.Single().Markdown.Should().Contain("Descrição").And.Contain("não");

    [Fact]
    public async Task Xlsx_AlinhaColunasVazias_EFormataDatas()
    {
        using var memoria = new MemoryStream();
        using (var xlsx = SpreadsheetDocument.Create(memoria, SpreadsheetDocumentType.Workbook))
        {
            var livro = xlsx.AddWorkbookPart();
            var estilos = livro.AddNewPart<WorkbookStylesPart>();
            estilos.Stylesheet = new S.Stylesheet(
                new S.Fonts(new S.Font()), new S.Fills(new S.Fill()), new S.Borders(new S.Border()), new S.CellStyleFormats(new S.CellFormat()),
                new S.CellFormats(new S.CellFormat(), new S.CellFormat { NumberFormatId = 14, ApplyNumberFormat = true }));
            var aba = livro.AddNewPart<WorksheetPart>();
            static S.Cell Txt(string r, string t) => new() { CellReference = r, DataType = S.CellValues.InlineString, InlineString = new S.InlineString(new S.Text(t)) };
            aba.Worksheet = new S.Worksheet(new S.SheetData(
                new S.Row(Txt("A1", "Data"), Txt("B1", "Obs"), Txt("C1", "Valor")),
                new S.Row(new S.Cell { CellReference = "A2", StyleIndex = 1, CellValue = new S.CellValue("45361") }, new S.Cell { CellReference = "C2", CellValue = new S.CellValue("5000.5") })));
            livro.Workbook = new S.Workbook(new S.Sheets(new S.Sheet { Id = livro.GetIdOfPart(aba), SheetId = 1, Name = "Extrato" }));
        }

        var resultado = await Planilha().ConverterAsync(Doc(memoria.ToArray(), "e.xlsx", FormatoDocumento.Planilha));

        resultado.Value.Blocos.Single().Secao.Should().Be("Extrato");
        resultado.Value.Blocos.Single().Markdown.Should().Contain("| Data | Obs | Valor |").And.Contain("| 10/03/2024 |  | 5000,5 |");
    }

    // ------------------------------------------------------------ e-mail, texto, áudio, imagem

    [Fact]
    public async Task Email_PreservaEnvelope_LimpaHtml_EAvisaDosAnexos()
    {
        const string eml = """
            From: Fulano <fulano@exemplo.com>
            To: Beltrana <beltrana@exemplo.com>
            Date: Sun, 10 Mar 2024 10:00:00 -0300
            Subject: =?utf-8?Q?Cobran=C3=A7a?=
            MIME-Version: 1.0
            Content-Type: multipart/mixed; boundary=b

            --b
            Content-Type: text/html; charset=utf-8

            <html><head><style>x{}</style></head><body><p>Segue a cobran&ccedil;a.</p><script>alert(1)</script><br>Att.</body></html>
            --b
            Content-Type: application/pdf
            Content-Disposition: attachment; filename="boleto.pdf"

            x
            --b--
            """;

        var resultado = (await new EmailConversor().ConverterAsync(Doc(Encoding.UTF8.GetBytes(eml.ReplaceLineEndings("\r\n")), "m.eml", FormatoDocumento.Email))).Value;

        resultado.Titulo.Should().Be("Cobrança");
        resultado.Metadados.Should().ContainKeys("Remetente", "Destinatário", "Enviado em", "Assunto");
        resultado.Blocos.Single().Markdown.Should().Contain("fulano@exemplo.com").And.Contain("Segue a cobrança.").And.NotContain("alert").And.NotContain("<p>");
        resultado.Avisos.Should().ContainSingle(a => a.Contains("boleto.pdf"));
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("iso-8859-1")]
    public async Task Texto_EmQualquerCodificacaoComum_ELido(string codificacao)
    {
        var encoding = Encoding.GetEncoding(codificacao);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("Petição: não há solução.")).ToArray();

        (await new TextoConversor().ConverterAsync(Doc(bytes, "a.txt", FormatoDocumento.Texto))).Value.Blocos.Single().Markdown
            .Should().Be("Petição: não há solução.");
    }

    [Fact]
    public async Task Imagem_UsaOcr_ECarregaAConfianca() =>
        (await new ImagemConversor(new OcrFalso(confianca: 0.5)).ConverterAsync(Doc([1], "f.png", FormatoDocumento.Imagem))).Value.Confianca.Should().Be(0.5);

    private sealed class Handler(Func<HttpRequestMessage, string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, await request.Content!.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Audio_TranscreveComTempo_MarcaComoTranscricao_ENaoMandaONomeDoArquivo()
    {
        string? corpo = null;
        var http = new HttpClient(new Handler((_, c) =>
        {
            corpo = c;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"text":"x","segments":[{"start":1.0,"end":3.0,"text":" Combinamos o preço.","avg_logprob":-0.1},{"start":65,"end":70,"text":"Tchau.","avg_logprob":-0.2}]}""",
                    Encoding.UTF8, "application/json"),
            };
        }));
        var servico = new HttpTranscricaoService(
            http, Options.Create(new TranscricaoOptions { BaseUrl = "http://localhost:9000" }), NullLogger<HttpTranscricaoService>.Instance);

        var resultado = (await new AudioConversor(servico).ConverterAsync(Doc([1], "conversa-com-joao.mp3", FormatoDocumento.Audio))).Value;

        resultado.Origem.Should().Be(OrigemTexto.Transcricao);
        resultado.Blocos.Should().HaveCount(2);
        resultado.Blocos[0].Markdown.Should().Be("[00:00:01] Combinamos o preço.");
        resultado.Avisos.Should().Contain(AudioConversor.Aviso);
        corpo.Should().NotContain("joao");
    }

    [Fact]
    public async Task Audio_SemServidorConfigurado_ERecusadoComMotivo_EHttpRemotoSemTlsNaoSai()
    {
        var sem = new HttpTranscricaoService(new HttpClient(), Options.Create(new TranscricaoOptions()), NullLogger<HttpTranscricaoService>.Instance);
        (await sem.TranscreverAsync([1], "a.mp3")).Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_CONVERSION_UNAVAILABLE));

        var inseguro = new HttpTranscricaoService(
            new HttpClient(), Options.Create(new TranscricaoOptions { BaseUrl = "http://203.0.113.9" }), NullLogger<HttpTranscricaoService>.Instance);
        await ((Func<Task>)(() => inseguro.TranscreverAsync([1], "a.mp3"))).Should().ThrowAsync<InvalidOperationException>();
    }

    // ------------------------------------------------------------ OCR

    [Fact]
    public void Tsv_ReconstroiLinhasEParagrafos_EPonderaAConfianca()
    {
        const string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
            "1\t1\t0\t0\t0\t0\t0\t0\t10\t10\t-1\t\n5\t1\t1\t1\t1\t1\t0\t0\t1\t1\t90\tABCD\n5\t1\t1\t1\t1\t2\t0\t0\t1\t1\t50\tEF\n" +
            "5\t1\t1\t1\t2\t1\t0\t0\t1\t1\t90\tGH\n5\t1\t2\t1\t1\t1\t0\t0\t1\t1\t90\tIJ\n5\t1\t2\t1\t1\t2\t0\t0\t1\t1\t-1\tx\n";

        var resultado = TesseractOcrService.LerTsv(tsv);

        resultado.Texto.Should().Be("ABCD EF\nGH\n\nIJ");
        resultado.Confianca.Should().BeApproximately(0.82, 0.001);
    }

    [Fact]
    public async Task TesseractAusente_NaoQuebra_SoFicaIndisponivel()
    {
        var ocr = new TesseractOcrService(Options.Create(new OcrOptions { Executavel = "tesseract-que-nao-existe" }), NullLogger<TesseractOcrService>.Instance);

        (await ocr.DisponivelAsync()).Should().BeFalse();
        (await ocr.ReconhecerAsync([1])).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DocAntigo_SemLibreOffice_DizOQueFazer()
    {
        var sem = new LibreOfficeConversor(Options.Create(new ConversaoOptions { LibreOffice = "soffice-que-nao-existe" }));

        (await new DocConversor(sem).ConverterAsync(Doc([1], "a.doc", FormatoDocumento.Doc))).Error.Message.Should().Contain("DOCX ou PDF");
    }
}

/// <summary>Contra as ferramentas reais (LibreOffice, poppler). Onde não estão instaladas, o teste não tem o que verificar e sai.</summary>
public class FerramentasExternasTests
{
    private static readonly IOptions<ConversaoOptions> Conversao = Options.Create(new ConversaoOptions());

    private static async Task<bool> InstaladoAsync(string executavel, string argumento) =>
        await ProcessoExterno.ExecutarAsync(executavel, [argumento], null, TimeSpan.FromSeconds(60), default) is not null;

    [Theory]
    [InlineData("doc")]
    [InlineData("xls")]
    [InlineData("ods")]
    public async Task LibreOffice_ConverteFormatosAntigos(string formato)
    {
        if (!await InstaladoAsync("soffice", "--version"))
        {
            return;
        }

        var libreOffice = new LibreOfficeConversor(Conversao);
        var origem = formato == "doc"
            ? (Bytes: new ExportadorDocx().Gerar("t", "CONTRATO ANTIGO\nCláusula décima.", "r"), Extensao: ".docx")
            : (Bytes: Encoding.UTF8.GetBytes("Data;Valor\n10/03/2024;100"), Extensao: ".csv");
        var (_, antigo) = await libreOffice.ConverterAsync(origem.Bytes, origem.Extensao, formato, default);
        antigo.Should().NotBeNull();
        DetectorFormato.Detectar(antigo!, "a." + formato).Should().NotBeNull("a assinatura do arquivo real precisa ser reconhecida");

        IConversorFormato conversor = formato == "doc" ? new DocConversor(libreOffice) : new PlanilhaConversor(libreOffice, Options.Create(new AnexosOptions()));
        var resultado = await conversor.ConverterAsync(new DocumentoParaConverter(antigo!, "a." + formato, conversor.Formatos[0]));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Blocos.Single().Markdown.Should().ContainAny("Cláusula décima", "100");
    }

    private sealed class OcrEspiao : IOcrService
    {
        public List<byte[]> Imagens { get; } = [];

        public Task<bool> DisponivelAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Result<ResultadoOcr>> ReconhecerAsync(byte[] imagem, CancellationToken cancellationToken = default)
        {
            lock (Imagens)
            {
                Imagens.Add(imagem);
            }

            return Task.FromResult(Result.Success(new ResultadoOcr("lido", 0.9)));
        }
    }

    /// <summary>ADVOCACIA_TEST_TESSERACT aponta para outro executável (ex.: um que rode o da imagem do Worker).</summary>
    [Fact]
    public async Task Tesseract_LePaginaEscaneada_ComAcentosEConfianca()
    {
        var tesseract = new TesseractOcrService(
            Options.Create(new OcrOptions { Executavel = Environment.GetEnvironmentVariable("ADVOCACIA_TEST_TESSERACT") ?? "tesseract" }),
            NullLogger<TesseractOcrService>.Instance);
        if (!await InstaladoAsync("pdftoppm", "-v") || !await InstaladoAsync("soffice", "--version") || !await tesseract.DisponivelAsync())
        {
            return;
        }

        // Texto → imagem → PDF só com a imagem: o que sai de um scanner.
        var docx = new ExportadorDocx().Gerar(
            "Contrato", "CONTRATO DE PRESTAÇÃO DE SERVIÇOS\n\nCláusula 5 - Do pagamento\n\nA contratante pagará R$ 1.500,00 até 10/03/2024.", "r");
        var (_, pdf) = await new LibreOfficeConversor(Conversao).ConverterAsync(docx, ".docx", "pdf", default);
        var imagem = await ProcessoExterno.ExecutarAsync(
            "pdftoppm", ["-r", "200", "-png", "-singlefile", "-"], pdf!, TimeSpan.FromSeconds(60), default);
        var escaneado = new PdfDocumentBuilder();
        escaneado.AddPage(595, 842).AddPng(imagem!.Saida, new PdfRectangle(0, 0, 595, 842));
        var conversor = new PdfConversor(
            tesseract, Conversao, Options.Create(new OcrOptions()), Options.Create(new AnexosOptions()), NullLogger<PdfConversor>.Instance);

        var resultado = await conversor.ConverterAsync(new DocumentoParaConverter(escaneado.Build(), "e.pdf", FormatoDocumento.Pdf));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Origem.Should().Be(OrigemTexto.Ocr);
        resultado.Value.Blocos.Single().Markdown.Should().Contain("PRESTAÇÃO DE SERVIÇOS").And.Contain("Cláusula 5").And.Contain("R$ 1.500,00").And.Contain("10/03/2024");
        resultado.Value.Confianca.Should().BeGreaterThan(CasoDocumento.ConfiancaOcrBaixa);
    }

    [Fact]
    public async Task Poppler_RenderizaAPaginaEscaneadaInteira_ParaOOcr()
    {
        if (!await InstaladoAsync("pdftoppm", "-v"))
        {
            return;
        }

        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842).AddPng(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="), new PdfRectangle(0, 0, 595, 842));
        var ocr = new OcrEspiao();
        var conversor = new PdfConversor(
            ocr, Conversao, Options.Create(new OcrOptions { Dpi = 100 }), Options.Create(new AnexosOptions()), NullLogger<PdfConversor>.Instance);

        await conversor.ConverterAsync(new DocumentoParaConverter(builder.Build(), "e.pdf", FormatoDocumento.Pdf));

        // 595x842 pontos a 100 dpi = 827x1170 pixels: largura e altura ficam nos bytes 16–23 do cabeçalho PNG.
        var png = ocr.Imagens.Should().ContainSingle().Subject;
        System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)).Should().BeInRange(825, 829);
        System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)).Should().BeInRange(1168, 1172);
    }

    [Fact]
    public async Task ProcessoQueNaoTermina_EEncerradoNoPrazo()
    {
        if (!File.Exists("/bin/sleep"))
        {
            return;
        }

        var inicio = DateTime.UtcNow;
        await ((Func<Task>)(() => ProcessoExterno.ExecutarAsync("/bin/sleep", ["30"], null, TimeSpan.FromMilliseconds(300), default)))
            .Should().ThrowAsync<TimeoutException>();
        (DateTime.UtcNow - inicio).Should().BeLessThan(TimeSpan.FromSeconds(10));
    }
}

public class CasoArquivoStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("casos-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private CasoArquivoStore Store(FieldEncryptionOptions? chaves = null) => new(
        new LocalObjectStorage(Options.Create(new NormasIngestaoOptions { Storage = { DiretorioLocal = _dir } })), Options.Create(chaves ?? ModuleInit.Chaves));

    private const string Chave = "casos/t/c/d/original";

    [Fact]
    public async Task NoDisco_SoHaBytesCifrados_ELerDevolveOOriginal()
    {
        await Store().GravarAsync(Chave, "SEGREDO DO CLIENTE"u8.ToArray());

        Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(_dir, Chave))).Should().NotContain("SEGREDO");
        Encoding.UTF8.GetString((await Store().LerAsync(Chave))!).Should().Be("SEGREDO DO CLIENTE");
        (await Store().LerAsync("casos/nao/existe")).Should().BeNull();
    }

    [Fact]
    public async Task ArquivoAdulterado_OuMovidoParaOutraChave_NaoDecifra()
    {
        await Store().GravarAsync(Chave, [1, 2, 3]);
        File.Copy(Path.Combine(_dir, Chave), Path.Combine(_dir, Chave + "2"));
        var caminho = Path.Combine(_dir, Chave);
        var bytes = File.ReadAllBytes(caminho);
        bytes[^1] ^= 1;
        File.WriteAllBytes(caminho, bytes);

        await ((Func<Task>)(() => Store().LerAsync(Chave))).Should().ThrowAsync<CryptographicException>();
        await ((Func<Task>)(() => Store().LerAsync(Chave + "2"))).Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task RotacaoDeChave_ContinuaLendoOAntigo()
    {
        await Store().GravarAsync(Chave, [7]);
        var rotacionadas = new FieldEncryptionOptions
        {
            ActiveKeyId = "t2",
            Keys = new(ModuleInit.Chaves.Keys) { ["t2"] = Convert.ToBase64String(new byte[32]) },
            BlindIndexKey = ModuleInit.Chaves.BlindIndexKey,
        };

        (await Store(rotacionadas).LerAsync(Chave)).Should().Equal(7);
    }
}

public class ExportadorDocxTests
{
    [Fact]
    public async Task DocxExportado_ELidoDeVolta_ComOTextoInteiro()
    {
        var bytes = new ExportadorDocx().Gerar("petição inicial", "DOS FATOS\n\nO **autor** pagou & não recebeu <nada>.", "Revisada em 22/11/2026.");

        var lido = await new DocxConversor().ConverterAsync(new DocumentoParaConverter(bytes, "x.docx", FormatoDocumento.Docx));

        lido.Value.Blocos.Single().Markdown.Should().Contain("DOS FATOS").And.Contain("O autor pagou & não recebeu <nada>.");
    }
}

public class MetricasTests
{
    [Fact]
    public void HorasDeGpu_SomaIntervalos_EAbertaContaAteAgora()
    {
        var t = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        Metricas.PecaMetricasService.HorasLigada(
            [(true, t), (true, t.AddMinutes(5)), (false, t.AddHours(1)), (false, t.AddHours(2)), (true, t.AddHours(3))], t.AddHours(5)).Should().Be(3);
    }
}
