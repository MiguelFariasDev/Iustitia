using Advocacia.Modules.Drafting.Application.Abstractions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Advocacia.Modules.Drafting.Infrastructure.Exportacao;

/// <summary>Peça revisada em DOCX: texto justificado, seções em negrito e rodapé com a declaração de revisão.</summary>
public sealed class ExportadorDocx : IExportadorDocx
{
    public byte[] Gerar(string titulo, string texto, string rodape)
    {
        using var memoria = new MemoryStream();
        using (var docx = WordprocessingDocument.Create(memoria, WordprocessingDocumentType.Document))
        {
            docx.PackageProperties.Title = titulo;
            var principal = docx.AddMainDocumentPart();
            var corpo = new Body();
            foreach (var linha in texto.ReplaceLineEndings("\n").Split('\n'))
            {
                corpo.Append(Paragrafo(linha));
            }

            var parteRodape = principal.AddNewPart<FooterPart>();
            parteRodape.Footer = new Footer(new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Italic(), new FontSize { Val = "16" }), Texto(rodape))));

            corpo.Append(new SectionProperties(
                new FooterReference { Type = HeaderFooterValues.Default, Id = principal.GetIdOfPart(parteRodape) },
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1701, Left = 1701, Bottom = 1134, Right = 1134, Header = 709, Footer = 709 }));
            principal.Document = new Document(corpo);
        }

        return memoria.ToArray();
    }

    private static Paragraph Paragrafo(string linha)
    {
        var texto = linha.Trim().TrimStart('#').Trim();
        var titulo = linha.TrimStart().StartsWith('#') || (texto.Length is > 3 and < 80 && texto == texto.ToUpperInvariant() && texto.Any(char.IsLetter));
        var propriedades = new ParagraphProperties(
            new SpacingBetweenLines { After = "120", Line = "360", LineRule = LineSpacingRuleValues.Auto },
            new Justification { Val = titulo ? JustificationValues.Left : JustificationValues.Both });
        var paragrafo = new Paragraph(propriedades);

        // Negrito do Markdown (**assim**): pedaços ímpares entre asteriscos.
        var partes = texto.Split("**");
        for (var i = 0; i < partes.Length; i++)
        {
            if (partes[i].Length == 0)
            {
                continue;
            }

            var estilo = new RunProperties(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            if (titulo || i % 2 == 1)
            {
                estilo.Append(new Bold());
            }

            estilo.Append(new FontSize { Val = "24" });
            paragrafo.Append(new Run(estilo, Texto(partes[i])));
        }

        return paragrafo;
    }

    private static Text Texto(string valor) => new(valor) { Space = SpaceProcessingModeValues.Preserve };
}
