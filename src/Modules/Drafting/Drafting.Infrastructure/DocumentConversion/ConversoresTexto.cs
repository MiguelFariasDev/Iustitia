using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Casos;
using MimeKit;

namespace Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;

/// <summary>TXT e Markdown: só decodifica. O Markdown do usuário é preservado.</summary>
public sealed class TextoConversor : IConversorFormato
{
    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Texto, FormatoDocumento.Markdown];

    public Task<Result<DocumentoConvertido>> ConverterAsync(DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        var texto = Apoio.Decodificar(documento.Conteudo);

        return Task.FromResult(texto.Contains('\0')
            ? Apoio.NaoSuportado()
            : Apoio.Pronto(null, [new BlocoConvertido(null, null, texto, OrigemTexto.Nativo)]));
    }
}

/// <summary>E-mail (.eml): remetente, destinatários, data, assunto e corpo. Anexos do e-mail são listados, não extraídos.</summary>
public sealed partial class EmailConversor : IConversorFormato
{
    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Email];

    public async Task<Result<DocumentoConvertido>> ConverterAsync(
        DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        MimeMessage mensagem;
        try
        {
            mensagem = await MimeMessage.LoadAsync(new MemoryStream(documento.Conteudo, writable: false), cancellationToken);
        }
        catch (FormatException)
        {
            return Apoio.NaoSuportado();
        }

        var corpo = mensagem.TextBody ?? (mensagem.HtmlBody is { } html ? TextoDoHtml(html) : null);
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return Apoio.NaoSuportado();
        }

        var cabecalho = new Dictionary<string, string>
        {
            ["Remetente"] = mensagem.From.ToString(),
            ["Destinatário"] = mensagem.To.ToString(),
        };
        if (mensagem.Cc.Count > 0)
        {
            cabecalho["Cópia"] = mensagem.Cc.ToString();
        }

        if (mensagem.Date != DateTimeOffset.MinValue)
        {
            cabecalho["Enviado em"] = mensagem.Date.ToString("dd/MM/yyyy HH:mm zzz", CultureInfo.InvariantCulture);
        }

        cabecalho["Assunto"] = mensagem.Subject ?? "(sem assunto)";

        var anexos = mensagem.Attachments.Select(a => a.ContentDisposition?.FileName ?? a.ContentType.Name ?? "sem nome").ToList();
        var avisos = anexos.Count == 0
            ? []
            : new List<string> { $"O e-mail tem {anexos.Count} anexo(s) não lidos aqui ({string.Join(", ", anexos)}). Anexe-os ao caso separadamente." };

        // Os dados do envelope entram também no bloco: são o que se busca num e-mail ("quem mandou, quando").
        var envelope = string.Join('\n', cabecalho.Select(c => $"**{c.Key}:** {c.Value}  "));

        return Apoio.Pronto(
            mensagem.Subject, [new BlocoConvertido(null, "E-mail", $"{envelope}\n\n{corpo.Trim()}", OrigemTexto.Nativo)], cabecalho, avisos);
    }

    internal static string TextoDoHtml(string html)
    {
        var semScript = ScriptOuEstilo().Replace(html, " ");
        var comQuebras = Quebra().Replace(semScript, "\n");
        var texto = WebUtility.HtmlDecode(Tag().Replace(comQuebras, " "));

        return LinhasVazias().Replace(Espacos().Replace(texto, " "), "\n\n").Trim();
    }

    [GeneratedRegex(@"<(script|style|head)\b.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOuEstilo();

    [GeneratedRegex(@"<(?:br|/p|/div|/tr|/li|/h[1-6])\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex Quebra();

    [GeneratedRegex(@"<[^>]{0,2000}>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Espacos();

    [GeneratedRegex(@"\s*\n\s*\n\s*")]
    private static partial Regex LinhasVazias();
}

/// <summary>Áudio: transcrição com marcação de tempo. É transcrição, não prova original — e vai dito no próprio texto.</summary>
public sealed class AudioConversor(ITranscricaoService transcricao) : IConversorFormato
{
    public const string Aviso = "Transcrição automática de áudio. Não é a prova original: confira a gravação antes de usar.";

    private static readonly TimeSpan Bloco = TimeSpan.FromMinutes(1);

    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Audio];

    public async Task<Result<DocumentoConvertido>> ConverterAsync(
        DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        var resultado = await transcricao.TranscreverAsync(documento.Conteudo, documento.NomeArquivo, cancellationToken);
        if (resultado.IsFailure)
        {
            return Result.Failure<DocumentoConvertido>(resultado.Error);
        }

        var blocos = resultado.Value.Segmentos
            .GroupBy(s => (long)(s.Inicio / Bloco))
            .Select(g => new BlocoConvertido(
                null,
                $"Trecho {g.First().Inicio:hh\\:mm\\:ss}",
                string.Join('\n', g.Select(s => $"[{s.Inicio:hh\\:mm\\:ss}] {s.Texto}")),
                OrigemTexto.Transcricao,
                g.Any(s => s.Confianca is not null) ? g.Where(s => s.Confianca is not null).Average(s => s.Confianca!.Value) : null))
            .ToList();

        return Apoio.Pronto(null, blocos, null, [Aviso]);
    }
}

/// <summary>Imagem: OCR. Baixa confiança exige conferência humana (decidido no domínio).</summary>
public sealed class ImagemConversor(IOcrService ocr) : IConversorFormato
{
    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Imagem];

    public async Task<Result<DocumentoConvertido>> ConverterAsync(
        DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        var lido = await ocr.ReconhecerAsync(documento.Conteudo, cancellationToken);
        if (lido.IsFailure)
        {
            return Result.Failure<DocumentoConvertido>(lido.Error);
        }

        return Apoio.Pronto(
            null, [new BlocoConvertido(1, null, lido.Value.Texto, OrigemTexto.Ocr, lido.Value.Confianca)], null,
            ["Texto obtido por OCR de imagem: confira com o original."]);
    }
}
