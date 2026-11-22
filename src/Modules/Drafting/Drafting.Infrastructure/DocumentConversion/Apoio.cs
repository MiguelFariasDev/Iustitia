using System.IO.Compression;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;

namespace Advocacia.Modules.Drafting.Infrastructure.DocumentConversion;

internal static class Apoio
{
    private const long MaxDescompactado = 400L * 1024 * 1024;

    public static Result<DocumentoConvertido> NaoSuportado() =>
        Result.Failure<DocumentoConvertido>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE));

    public static Result<DocumentoConvertido> Indisponivel(string motivo) =>
        Result.Failure<DocumentoConvertido>(ErrorFactory.From(ErrorCode.DRAFTING_CONVERSION_UNAVAILABLE, motivo));

    public static Result<DocumentoConvertido> Pronto(
        string? titulo, IReadOnlyList<BlocoConvertido> blocos, IReadOnlyDictionary<string, string>? metadados = null,
        IReadOnlyList<string>? avisos = null) =>
        Result.Success(new DocumentoConvertido(titulo, blocos, metadados ?? new Dictionary<string, string>(), avisos ?? []));

    /// <summary>"Bomba" de descompressão: confere o tamanho declarado de tudo antes de abrir.</summary>
    public static bool ZipSeguro(byte[] conteudo)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(conteudo, writable: false), ZipArchiveMode.Read);

            return zip.Entries.Count <= 20_000 && zip.Entries.Sum(e => e.Length) <= MaxDescompactado;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>UTF-8 (com ou sem BOM) e UTF-16 com BOM; o que não for UTF-8 válido é lido como Latin-1.</summary>
    public static string Decodificar(byte[] bytes)
    {
        if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
        {
            using var leitor = new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true);

            return leitor.ReadToEnd();
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>Tabela Markdown. A primeira linha é o cabeçalho; barras e quebras dentro da célula são neutralizadas.</summary>
    public static string Tabela(IReadOnlyList<IReadOnlyList<string>> linhas)
    {
        var colunas = linhas.Count == 0 ? 0 : linhas.Max(l => l.Count);
        if (colunas == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < linhas.Count; i++)
        {
            sb.Append('|');
            for (var c = 0; c < colunas; c++)
            {
                sb.Append(' ').Append(c < linhas[i].Count ? Celula(linhas[i][c]) : string.Empty).Append(" |");
            }

            sb.Append('\n');
            if (i == 0)
            {
                sb.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", colunas))).Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string Celula(string valor) => valor.ReplaceLineEndings(" ").Replace("|", "\\|").Trim();
}
