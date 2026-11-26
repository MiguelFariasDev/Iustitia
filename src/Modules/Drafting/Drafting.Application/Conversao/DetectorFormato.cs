using System.Text;
using Advocacia.Modules.Drafting.Domain.Casos;

namespace Advocacia.Modules.Drafting.Application.Conversao;

/// <summary>
/// Decide o formato pelo CONTEÚDO (assinatura) e exige que a extensão concorde: um executável renomeado para
/// .pdf não passa, e um .docx que na verdade é .xlsx também não. Formato não reconhecido = recusado.
/// </summary>
public static class DetectorFormato
{
    private static readonly Dictionary<string, FormatoDocumento> PorExtensao = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = FormatoDocumento.Pdf,
        [".docx"] = FormatoDocumento.Docx,
        [".doc"] = FormatoDocumento.Doc,
        [".txt"] = FormatoDocumento.Texto,
        [".md"] = FormatoDocumento.Markdown,
        [".markdown"] = FormatoDocumento.Markdown,
        [".png"] = FormatoDocumento.Imagem,
        [".jpg"] = FormatoDocumento.Imagem,
        [".jpeg"] = FormatoDocumento.Imagem,
        [".tif"] = FormatoDocumento.Imagem,
        [".tiff"] = FormatoDocumento.Imagem,
        [".bmp"] = FormatoDocumento.Imagem,
        [".webp"] = FormatoDocumento.Imagem,
        [".xlsx"] = FormatoDocumento.Planilha,
        [".xls"] = FormatoDocumento.Planilha,
        [".ods"] = FormatoDocumento.Planilha,
        [".csv"] = FormatoDocumento.Planilha,
        [".eml"] = FormatoDocumento.Email,
        [".mp3"] = FormatoDocumento.Audio,
        [".wav"] = FormatoDocumento.Audio,
        [".m4a"] = FormatoDocumento.Audio,
        [".ogg"] = FormatoDocumento.Audio,
        [".opus"] = FormatoDocumento.Audio,
        [".flac"] = FormatoDocumento.Audio,
        [".webm"] = FormatoDocumento.Audio,
    };

    public static IEnumerable<string> Extensoes => PorExtensao.Keys;

    public static FormatoDocumento? Detectar(ReadOnlySpan<byte> conteudo, string nomeArquivo)
    {
        var extensao = Path.GetExtension(nomeArquivo);
        if (conteudo.Length == 0 || !PorExtensao.TryGetValue(extensao, out var formato))
        {
            return null;
        }

        var confere = formato switch
        {
            FormatoDocumento.Pdf => Contem(conteudo[..Math.Min(1024, conteudo.Length)], "%PDF-"u8),
            FormatoDocumento.Docx => Zip(conteudo) && Contem(conteudo, "word/"u8),
            FormatoDocumento.Doc => Ole(conteudo) || Comeca(conteudo, "{\\rtf"u8),
            FormatoDocumento.Planilha => Planilha(conteudo, extensao),
            FormatoDocumento.Imagem => Imagem(conteudo),
            FormatoDocumento.Audio => Audio(conteudo),
            FormatoDocumento.Email => Texto(conteudo) && CabecalhoDeEmail(conteudo),
            _ => Texto(conteudo),
        };

        return confere ? formato : null;
    }

    public static string ContentType(FormatoDocumento formato, string nomeArquivo) => formato switch
    {
        FormatoDocumento.Pdf => "application/pdf",
        FormatoDocumento.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        FormatoDocumento.Doc => "application/msword",
        FormatoDocumento.Texto or FormatoDocumento.Markdown => "text/plain; charset=utf-8",
        FormatoDocumento.Email => "message/rfc822",
        // Nunca um tipo que o navegador execute ou renderize como página.
        _ => "application/octet-stream",
    };

    private static bool Planilha(ReadOnlySpan<byte> c, string extensao) => extensao.ToLowerInvariant() switch
    {
        ".xlsx" => Zip(c) && Contem(c, "xl/"u8),
        ".ods" => Zip(c) && Contem(c, "opendocument.spreadsheet"u8),
        ".xls" => Ole(c),
        _ => Texto(c),
    };

    private static bool Imagem(ReadOnlySpan<byte> c) =>
        Comeca(c, [0x89, 0x50, 0x4E, 0x47]) || Comeca(c, [0xFF, 0xD8, 0xFF]) || Comeca(c, "BM"u8)
        || Comeca(c, [0x49, 0x49, 0x2A, 0x00]) || Comeca(c, [0x4D, 0x4D, 0x00, 0x2A])
        || (Comeca(c, "RIFF"u8) && c.Length > 12 && c[8..12].SequenceEqual("WEBP"u8));

    private static bool Audio(ReadOnlySpan<byte> c) =>
        Comeca(c, "ID3"u8) || (c.Length > 2 && c[0] == 0xFF && (c[1] & 0xE0) == 0xE0)
        || (Comeca(c, "RIFF"u8) && c.Length > 12 && c[8..12].SequenceEqual("WAVE"u8))
        || Comeca(c, "OggS"u8) || Comeca(c, "fLaC"u8) || Comeca(c, [0x1A, 0x45, 0xDF, 0xA3])
        || (c.Length > 12 && c[4..8].SequenceEqual("ftyp"u8));

    private static bool CabecalhoDeEmail(ReadOnlySpan<byte> c)
    {
        var inicio = Encoding.Latin1.GetString(c[..Math.Min(8192, c.Length)]);

        return new[] { "From:", "Received:", "Return-Path:", "MIME-Version:", "Subject:", "Date:" }
            .Count(h => inicio.StartsWith(h, StringComparison.OrdinalIgnoreCase) || inicio.Contains("\n" + h, StringComparison.OrdinalIgnoreCase)) >= 2;
    }

    /// <summary>Byte nulo = binário disfarçado (UTF-16 com BOM é a exceção aceita).</summary>
    private static bool Texto(ReadOnlySpan<byte> c) =>
        Comeca(c, [0xFF, 0xFE]) || Comeca(c, [0xFE, 0xFF]) || c[..Math.Min(65536, c.Length)].IndexOf((byte)0) < 0;

    private static bool Zip(ReadOnlySpan<byte> c) => Comeca(c, [0x50, 0x4B, 0x03, 0x04]);

    private static bool Ole(ReadOnlySpan<byte> c) => Comeca(c, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);

    private static bool Comeca(ReadOnlySpan<byte> c, ReadOnlySpan<byte> prefixo) => c.StartsWith(prefixo);

    private static bool Contem(ReadOnlySpan<byte> c, ReadOnlySpan<byte> trecho) => c.IndexOf(trecho) >= 0;
}
