using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace Advocacia.Modules.Drafting.Infrastructure.Extracao;

/// <summary>
/// Extrai texto de PDF (por página), DOCX e TXT. O tipo é decidido pelo CONTEÚDO (assinatura do
/// arquivo), não só pela extensão. NÃO há OCR: PDF escaneado, sem camada de texto, é recusado com
/// uma mensagem clara — indexar vazio seria pior do que recusar.
/// </summary>
public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    /// <summary>Teto de texto extraído (proteção contra "bomba" de descompressão em DOCX).</summary>
    public const int MaxChars = 2_000_000;
    public const int MaxPdfPages = 2000;

    public Result<IReadOnlyList<ExtractedPage>> Extract(Stream content, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var header = new byte[4];
        var read = content.Read(header, 0, header.Length);
        content.Position = 0;

        try
        {
            if (extension == ".pdf" && read >= 4 && header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F')
            {
                return ExtractPdf(content);
            }

            // DOCX é um ZIP ("PK").
            if (extension == ".docx" && read >= 2 && header[0] == 'P' && header[1] == 'K')
            {
                return ExtractDocx(content);
            }

            if (extension == ".txt")
            {
                return ExtractText(content);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or InvalidOperationException
                                              or OpenXmlPackageException or ArgumentException or NotSupportedException)
        {
            return Unsupported();
        }
        catch (Exception exception) when (exception.GetType().Namespace?.StartsWith("UglyToad", StringComparison.Ordinal) == true)
        {
            // Erros do PdfPig (PDF corrompido, criptografado…).
            return Unsupported();
        }

        return Unsupported();
    }

    private static Result<IReadOnlyList<ExtractedPage>> ExtractPdf(Stream content)
    {
        using var document = PdfDocument.Open(content);
        if (document.NumberOfPages > MaxPdfPages)
        {
            return Unsupported();
        }

        var pages = new List<ExtractedPage>();
        var total = 0;
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            total += text.Length;
            if (total > MaxChars)
            {
                return Unsupported();
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                pages.Add(new ExtractedPage(page.Number, text));
            }
        }

        // PDF escaneado: páginas sem texto. Não há OCR; recusar é o comportamento honesto.
        return pages.Count == 0 ? Unsupported() : Result.Success<IReadOnlyList<ExtractedPage>>(pages);
    }

    private static Result<IReadOnlyList<ExtractedPage>> ExtractDocx(Stream content)
    {
        using var document = WordprocessingDocument.Open(content, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return Unsupported();
        }

        var text = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            text.AppendLine(paragraph.InnerText);
            if (text.Length > MaxChars)
            {
                return Unsupported();
            }
        }

        // DOCX não tem paginação estável: um bloco só, sem número de página ("p. s/n" nas citações).
        return text.Length == 0
            ? Unsupported()
            : Result.Success<IReadOnlyList<ExtractedPage>>([new ExtractedPage(null, text.ToString())]);
    }

    private static Result<IReadOnlyList<ExtractedPage>> ExtractText(Stream content)
    {
        using var reader = new StreamReader(content, new UTF8Encoding(false, throwOnInvalidBytes: true));
        var text = reader.ReadToEnd();

        // Byte nulo = binário disfarçado de .txt.
        if (text.Length == 0 || text.Length > MaxChars || text.Contains('\0'))
        {
            return Unsupported();
        }

        return Result.Success<IReadOnlyList<ExtractedPage>>([new ExtractedPage(null, text)]);
    }

    private static Result<IReadOnlyList<ExtractedPage>> Unsupported() =>
        Result.Failure<IReadOnlyList<ExtractedPage>>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
}
