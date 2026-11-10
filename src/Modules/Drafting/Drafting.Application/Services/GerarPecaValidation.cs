using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Models;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Validação e sanitização da entrada. Devolve erros do catálogo (ErrorFactory) para que a API
/// responda ProblemDetails com o código certo — e tudo isso ANTES de abrir o stream.
/// </summary>
public static class GerarPecaValidation
{
    public const int FatosMax = 8000;
    public const int PedidosMax = 4000;
    public const int InstrucoesMax = 2000;
    public const int DocumentosMax = 20;

    public static Result<PecaInput> Validate(GerarPecaRequest request)
    {
        var tipo = (request.TipoPeca ?? string.Empty).Trim().ToLowerInvariant();
        if (!PecaCatalogo.Tipos.ContainsKey(tipo))
        {
            return Result.Failure<PecaInput>(ErrorFactory.From(ErrorCode.DRAFTING_INVALID_PIECE_TYPE));
        }

        var area = (request.Area ?? string.Empty).Trim().ToLowerInvariant();
        if (!PecaCatalogo.Areas.ContainsKey(area))
        {
            return Result.Failure<PecaInput>(ErrorFactory.From(ErrorCode.DRAFTING_INVALID_AREA));
        }

        var fatos = InputSanitizer.Clean(request.Fatos);
        var pedidos = InputSanitizer.Clean(request.Pedidos);  // opcional: vazio é aceito
        var instrucoes = InputSanitizer.Clean(request.InstrucoesAdicionais);

        var campos = new (string Nome, string Valor, int Max, bool Obrigatorio)[]
        {
            ("fatos", fatos, FatosMax, true),
            ("pedidos", pedidos, PedidosMax, false),
            ("instrucoesAdicionais", instrucoes, InstrucoesMax, false),
        };

        foreach (var (nome, valor, max, obrigatorio) in campos)
        {
            if (obrigatorio && valor.Length == 0)
            {
                return Result.Failure<PecaInput>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, nome));
            }

            if (valor.Length > max)
            {
                return Result.Failure<PecaInput>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, nome, max));
            }

            if (PromptInjectionGuard.Detect(valor))
            {
                return Result.Failure<PecaInput>(ErrorFactory.From(ErrorCode.DRAFTING_PROMPT_INJECTION, nome));
            }
        }

        var documentos = (request.DocumentosIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (documentos.Count > DocumentosMax)
        {
            return Result.Failure<PecaInput>(ErrorFactory.From(ErrorCode.DRAFTING_TOO_MANY_DOCUMENTS, DocumentosMax));
        }

        return Result.Success(new PecaInput(
            tipo, area, fatos, pedidos, instrucoes.Length == 0 ? null : instrucoes, documentos, null));
    }
}
