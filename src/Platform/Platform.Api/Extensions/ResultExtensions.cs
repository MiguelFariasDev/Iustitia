using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>
/// Converte Result/Result{T} (retornado por todo handler — ver ADR-007) em respostas HTTP.
/// Erros de negócio nunca viram exceção: cada ErrorType mapeia para o status correto de
/// ProblemDetails (RFC 7807) diretamente aqui, na borda da API.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<TValue>(this Result<TValue> result, Func<TValue, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value)
            : ToProblem(result.Error);

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : ToProblem(result.Error);

    private static IResult ToProblem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        // error.Code é o nome do ErrorCode (ex.: "TENANT_NOT_FOUND") para todo erro
        // originado do catálogo (ErrorFactory.From) — a esmagadora maioria. Erros dinâmicos
        // de infraestrutura externa (ex.: SupabaseAuthService repassando o corpo de erro do
        // Supabase) usam códigos ad-hoc que não correspondem a nenhum ErrorCode; nesse caso
        // simplesmente omitimos a extension errorGroup, sem falhar a resposta por isso.
        var extensions = new Dictionary<string, object?> { ["errorType"] = error.Type.ToString() };
        if (Enum.TryParse<ErrorCode>(error.Code, out var errorCode))
        {
            extensions["errorGroup"] = ErrorCatalog.Get(errorCode).Group.ToString();
        }

        return Results.Problem(title: error.Code, detail: error.Message, statusCode: statusCode, extensions: extensions);
    }
}
