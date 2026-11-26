using System.Collections.Concurrent;
using System.Globalization;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;

/// <summary>
/// PDF página a página: texto nativo quando existe; OCR só na página que não tem texto (PDF misto é comum:
/// petição digital com documentos escaneados no fim). Cada página guarda número, origem e confiança.
/// </summary>
public sealed class PdfConversor(
    IOcrService ocr,
    IOptions<ConversaoOptions> conversao,
    IOptions<OcrOptions> ocrOptions,
    IOptions<AnexosOptions> anexos,
    ILogger<PdfConversor> logger) : IConversorFormato
{
    /// <summary>Menos letras que isto numa página com imagem = página escaneada (carimbo e número não contam).</summary>
    private const int MinLetras = 40;

    public IReadOnlyList<FormatoDocumento> Formatos { get; } = [FormatoDocumento.Pdf];

    public async Task<Result<DocumentoConvertido>> ConverterAsync(
        DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        Leitura leitura;
        try
        {
            leitura = Ler(documento.Conteudo, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PdfPig lança tipos variados para PDF corrompido ou protegido por senha.
            logger.LogWarning("PDF ilegível ({Tipo}).", ex.GetType().Name);

            return Apoio.NaoSuportado();
        }

        if (leitura.TotalPaginas > anexos.Value.MaxPaginas)
        {
            return Apoio.Indisponivel($"PDF com {leitura.TotalPaginas} páginas (limite: {anexos.Value.MaxPaginas}). Divida o arquivo");
        }

        var avisos = new List<string>();
        var blocos = new List<BlocoConvertido>(leitura.ComTexto);
        if (leitura.SemTexto.Count > 0)
        {
            var reconhecidas = await ReconhecerAsync(documento.Conteudo, leitura, avisos, cancellationToken);
            if (reconhecidas.IsFailure)
            {
                // Documento só de imagem e sem OCR: erro claro. Documento misto: segue com o que tem, avisando.
                if (blocos.Count == 0)
                {
                    return Result.Failure<DocumentoConvertido>(reconhecidas.Error);
                }

                avisos.Add($"{leitura.SemTexto.Count} página(s) escaneada(s) não puderam ser lidas: {Lista(leitura.SemTexto.Keys)}.");
            }
            else
            {
                blocos.AddRange(reconhecidas.Value);
            }
        }

        return blocos.Count == 0
            ? Apoio.NaoSuportado()
            : Apoio.Pronto(leitura.Titulo, blocos.OrderBy(b => b.Pagina).ToList(), null, avisos);
    }

    private sealed record Leitura(
        string? Titulo, int TotalPaginas, List<BlocoConvertido> ComTexto, Dictionary<int, byte[]?> SemTexto);

    private static Leitura Ler(byte[] conteudo, CancellationToken ct)
    {
        using var pdf = PdfDocument.Open(conteudo);
        var comTexto = new List<BlocoConvertido>();
        var semTexto = new Dictionary<int, byte[]?>();

        foreach (var pagina in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var texto = ContentOrderTextExtractor.GetText(pagina, true).Trim();
            if (texto.Count(char.IsLetterOrDigit) >= MinLetras)
            {
                comTexto.Add(new BlocoConvertido(pagina.Number, null, texto, OrigemTexto.Nativo));
                continue;
            }

            var imagem = MaiorImagem(pagina);
            if (imagem is not null || pagina.GetImages().Any())
            {
                semTexto[pagina.Number] = imagem;
            }
            else if (texto.Length > 0)
            {
                comTexto.Add(new BlocoConvertido(pagina.Number, null, texto, OrigemTexto.Nativo));
            }
        }

        var titulo = pdf.Information.Title?.Trim();

        return new Leitura(string.IsNullOrWhiteSpace(titulo) ? null : titulo, pdf.NumberOfPages, comTexto, semTexto);
    }

    /// <summary>A maior imagem da página (PNG ou JPEG): num escaneado, é a página inteira.</summary>
    private static byte[]? MaiorImagem(Page pagina)
    {
        foreach (var imagem in pagina.GetImages().OrderByDescending(i => (long)i.WidthInSamples * i.HeightInSamples))
        {
            if (imagem.TryGetPng(out var png))
            {
                return png;
            }

            var bruto = imagem.RawBytes.ToArray();
            if (bruto is [0xFF, 0xD8, 0xFF, ..])
            {
                return bruto;
            }
        }

        return null;
    }

    private async Task<Result<IReadOnlyList<BlocoConvertido>>> ReconhecerAsync(
        byte[] conteudo, Leitura leitura, List<string> avisos, CancellationToken ct)
    {
        if (!await ocr.DisponivelAsync(ct))
        {
            return Result.Failure<IReadOnlyList<BlocoConvertido>>(
                Apoio.Indisponivel("PDF escaneado (OCR não instalado)").Error);
        }

        var paginas = leitura.SemTexto.Keys.Order().ToList();
        if (paginas.Count > anexos.Value.MaxPaginasOcr)
        {
            avisos.Add($"Só as primeiras {anexos.Value.MaxPaginasOcr} páginas escaneadas foram lidas; ficaram de fora: {Lista(paginas.Skip(anexos.Value.MaxPaginasOcr))}.");
            paginas = paginas.Take(anexos.Value.MaxPaginasOcr).ToList();
        }

        return await ProcessoExterno.EmDiretorioTemporarioAsync(async diretorio =>
        {
            var arquivo = Path.Combine(diretorio, "entrada.pdf");
            await File.WriteAllBytesAsync(arquivo, conteudo, ct);

            var blocos = new ConcurrentBag<BlocoConvertido>();
            var perdidas = new ConcurrentBag<int>();
            await Parallel.ForEachAsync(
                paginas,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, ocrOptions.Value.Paralelismo), CancellationToken = ct },
                async (numero, token) =>
                {
                    var imagem = await RenderizarAsync(diretorio, arquivo, numero, token) ?? leitura.SemTexto[numero];
                    var lido = imagem is null ? null : await ocr.ReconhecerAsync(imagem, token);
                    if (lido is { IsSuccess: true } && !string.IsNullOrWhiteSpace(lido.Value.Texto))
                    {
                        blocos.Add(new BlocoConvertido(numero, null, lido.Value.Texto, OrigemTexto.Ocr, lido.Value.Confianca));
                    }
                    else
                    {
                        perdidas.Add(numero);
                    }
                });

            if (!perdidas.IsEmpty)
            {
                avisos.Add($"Página(s) sem texto reconhecível: {Lista(perdidas)}.");
            }

            return Result.Success<IReadOnlyList<BlocoConvertido>>(blocos.ToList());
        });
    }

    /// <summary>Renderiza a página em cinza. Pega rotação, recortes e formatos (CCITT, JBIG2) que a imagem embutida não resolve.</summary>
    private async Task<byte[]?> RenderizarAsync(string diretorio, string arquivo, int pagina, CancellationToken ct)
    {
        var numero = pagina.ToString(CultureInfo.InvariantCulture);
        var prefixo = Path.Combine(diretorio, "p" + numero);
        var saida = await ProcessoExterno.ExecutarAsync(
            conversao.Value.Pdftoppm,
            ["-f", numero, "-l", numero, "-r", ocrOptions.Value.Dpi.ToString(CultureInfo.InvariantCulture), "-gray", "-png", "-singlefile", arquivo, prefixo],
            null, TimeSpan.FromSeconds(conversao.Value.TimeoutSegundos), ct);
        var png = prefixo + ".png";

        return saida?.Codigo == 0 && File.Exists(png) ? await File.ReadAllBytesAsync(png, ct) : null;
    }

    private static string Lista(IEnumerable<int> paginas)
    {
        var ordenadas = paginas.Order().ToList();

        return string.Join(", ", ordenadas.Take(30)) + (ordenadas.Count > 30 ? "…" : string.Empty);
    }
}
