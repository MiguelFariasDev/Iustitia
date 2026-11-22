using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;

namespace Advocacia.Modules.Drafting.Application.Conversao;

/// <summary>Escolhe o conversor do formato e normaliza o resultado (sem caracteres invisíveis, sem blocos vazios).</summary>
public sealed class ConversorDocumentoService(IEnumerable<IConversorFormato> conversores) : IConversorDocumentoService
{
    /// <summary>Teto de texto por documento (proteção contra "bomba" de descompressão).</summary>
    public const int MaxChars = 4_000_000;

    public async Task<Result<DocumentoConvertido>> ConverterAsync(
        DocumentoParaConverter documento, CancellationToken cancellationToken = default)
    {
        var conversor = conversores.FirstOrDefault(c => c.Formatos.Contains(documento.Formato));
        if (conversor is null)
        {
            return Result.Failure<DocumentoConvertido>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
        }

        var convertido = await conversor.ConverterAsync(documento, cancellationToken);
        if (convertido.IsFailure)
        {
            return convertido;
        }

        var blocos = convertido.Value.Blocos
            .Select(b => b with { Markdown = InputSanitizer.RemoverInvisiveis(b.Markdown).Trim() })
            .Where(b => b.Markdown.Length > 0)
            .ToList();

        // Arquivo sem texto nenhum: indexar vazio seria pior do que recusar.
        return blocos.Count == 0 || blocos.Sum(b => (long)b.Markdown.Length) > MaxChars
            ? Result.Failure<DocumentoConvertido>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE))
            : Result.Success(convertido.Value with { Blocos = blocos });
    }
}
