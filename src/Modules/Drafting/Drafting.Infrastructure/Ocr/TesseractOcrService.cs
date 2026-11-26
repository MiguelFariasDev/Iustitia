using System.Globalization;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Ocr;

/// <summary>OCR local com Tesseract: nenhum documento sai da máquina. A saída TSV traz a confiança de cada palavra.</summary>
public sealed class TesseractOcrService(IOptions<OcrOptions> options, ILogger<TesseractOcrService> logger) : IOcrService
{
    private bool? _disponivel;

    public async Task<bool> DisponivelAsync(CancellationToken cancellationToken = default)
    {
        if (_disponivel is { } conhecido)
        {
            return conhecido;
        }

        var saida = await ProcessoExterno.ExecutarAsync(
            options.Value.Executavel, ["--list-langs"], null, TimeSpan.FromSeconds(30), cancellationToken);
        var idiomas = saida is null ? string.Empty : Encoding.UTF8.GetString(saida.Saida) + saida.Erro;
        _disponivel = saida?.Codigo == 0
            && options.Value.Idioma.Split('+').All(i => idiomas.Split('\n').Any(l => l.Trim() == i));
        if (_disponivel == false)
        {
            logger.LogWarning("OCR indisponível: Tesseract ausente ou sem o idioma '{Idioma}'.", options.Value.Idioma);
        }

        return _disponivel.Value;
    }

    public async Task<Result<ResultadoOcr>> ReconhecerAsync(byte[] imagem, CancellationToken cancellationToken = default)
    {
        if (!await DisponivelAsync(cancellationToken))
        {
            return Result.Failure<ResultadoOcr>(ErrorFactory.From(ErrorCode.DRAFTING_CONVERSION_UNAVAILABLE, "imagem ou PDF escaneado (OCR não instalado)"));
        }

        var o = options.Value;
        var saida = await ProcessoExterno.ExecutarAsync(
            o.Executavel,
            ["stdin", "stdout", "-l", o.Idioma, "--dpi", o.Dpi.ToString(CultureInfo.InvariantCulture), "--psm", "3", "tsv"],
            imagem, TimeSpan.FromSeconds(o.TimeoutSegundos), cancellationToken);

        return saida is null || saida.Codigo != 0
            ? Result.Failure<ResultadoOcr>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE))
            : Result.Success(LerTsv(Encoding.UTF8.GetString(saida.Saida)));
    }

    /// <summary>Colunas: level page block par line word left top width height conf text. Nível 5 = palavra.</summary>
    internal static ResultadoOcr LerTsv(string tsv)
    {
        var texto = new StringBuilder();
        string? paragrafo = null;
        string? linha = null;
        double soma = 0;
        long pesos = 0;

        foreach (var registro in tsv.Split('\n').Skip(1))
        {
            var c = registro.TrimEnd('\r').Split('\t');
            if (c.Length < 12 || c[0] != "5" || string.IsNullOrWhiteSpace(c[11])
                || !double.TryParse(c[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confianca) || confianca < 0)
            {
                continue;
            }

            var chaveParagrafo = $"{c[1]}.{c[2]}.{c[3]}";
            var chaveLinha = $"{chaveParagrafo}.{c[4]}";
            if (paragrafo is not null && chaveParagrafo != paragrafo)
            {
                texto.Append("\n\n");
            }
            else if (linha is not null && chaveLinha != linha)
            {
                texto.Append('\n');
            }
            else if (linha is not null)
            {
                texto.Append(' ');
            }

            texto.Append(c[11].Trim());
            paragrafo = chaveParagrafo;
            linha = chaveLinha;
            soma += confianca * c[11].Trim().Length;
            pesos += c[11].Trim().Length;
        }

        return new ResultadoOcr(texto.ToString(), pesos == 0 ? 0 : Math.Round(soma / pesos / 100, 4));
    }
}
