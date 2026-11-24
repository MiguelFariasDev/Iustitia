using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Drafting.Infrastructure.Extracao;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

public class DocumentTextExtractorTests
{
    private readonly DocumentTextExtractor _extractor = new();

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    private static MemoryStream Docx(params string[] paragraphs)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(paragraphs.Select(p => new Paragraph(new Run(new Text(p))))));
        }

        stream.Position = 0;

        return stream;
    }

    [Fact]
    public void Txt_ViraUmBlocoSemNumeroDePagina()
    {
        var result = _extractor.Extract(Utf8("Art. 186 do Código Civil.\nAquele que causar dano…"), "lei.txt");

        result.IsSuccess.Should().BeTrue();
        var page = result.Value.Should().ContainSingle().Subject;
        page.Numero.Should().BeNull();
        page.Texto.Should().Contain("Art. 186").And.Contain("causar dano");
    }

    [Fact]
    public void Docx_ExtraiOsParagrafos()
    {
        var result = _extractor.Extract(Docx("CLÁUSULA 1 — Do objeto.", "O contratado prestará o serviço."), "contrato.docx");

        result.IsSuccess.Should().BeTrue();
        var texto = result.Value.Should().ContainSingle().Subject.Texto;
        texto.Should().Contain("CLÁUSULA 1").And.Contain("prestará o serviço");
    }

    [Fact]
    public void ExtensaoTxtComConteudoBinario_ERecusado()
    {
        var binario = new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x01]);

        _extractor.Extract(binario, "falso.txt").Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
    }

    [Fact]
    public void TxtComBytesInvalidosEmUtf8_ERecusado()
    {
        var latin1 = new MemoryStream([0x41, 0xE7, 0xE3, 0x6F, 0xFF, 0xFE]); // "Ação" em Latin-1, inválido como UTF-8

        _extractor.Extract(latin1, "antigo.txt").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TxtVazio_ERecusado() =>
        _extractor.Extract(Utf8(string.Empty), "vazio.txt").IsFailure.Should().BeTrue();

    [Fact]
    public void PdfComExtensaoMasSemAssinatura_ERecusado() =>
        _extractor.Extract(Utf8("isto não é um pdf"), "falso.pdf").Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_UNSUPPORTED_FILE));

    [Fact]
    public void PdfCorrompidoComAssinatura_ERecusadoSemLancar()
    {
        var corrompido = Utf8("%PDF-1.7\n1 0 obj\n<< /Broken >>\nendobj\n%%EOF");

        var act = () => _extractor.Extract(corrompido, "quebrado.pdf");

        act.Should().NotThrow();
        _extractor.Extract(Utf8("%PDF-1.7\nlixo"), "quebrado.pdf").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DocxQueNaoEUmZip_ERecusado() =>
        _extractor.Extract(Utf8("texto puro"), "falso.docx").IsFailure.Should().BeTrue();

    [Fact]
    public void ExtensaoDeImagem_ENaoSuportada_PorqueNaoHaOcr() =>
        _extractor.Extract(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]), "scan.jpg").Error.Code
            .Should().Be(nameof(ErrorCode.DRAFTING_UNSUPPORTED_FILE));

    [Fact]
    public void TextoAcimaDoTeto_ERecusado()
    {
        var gigante = new string('a', DocumentTextExtractor.MaxChars + 1);

        _extractor.Extract(Utf8(gigante), "gigante.txt").IsFailure.Should().BeTrue();
    }
}
