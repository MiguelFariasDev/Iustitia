namespace Advocacia.BuildingBlocks.Domain.Errors;

/// <summary>
/// Exceção para erros realmente excepcionais (bugs, falhas de infraestrutura) — nunca para
/// erros de negócio esperados, que devem usar <see cref="ErrorFactory"/> + Result Pattern
/// (ver docs/errors/conventions.md e ADR-033). Capturada por
/// Platform.Api.Middleware.ExceptionHandlingMiddleware e convertida em ProblemDetails com
/// os mesmos metadados de <see cref="ErrorCatalog"/>.
/// </summary>
public class AppException : Exception
{
    public ErrorCode Code { get; }

    public ErrorGroup Group { get; }

    public int HttpStatus { get; }

    public AppException(ErrorCode code, string? customMessage = null, Exception? inner = null)
        : base(customMessage ?? ErrorCatalog.Get(code).Message, inner)
    {
        var definition = ErrorCatalog.Get(code);
        Code = code;
        Group = definition.Group;
        HttpStatus = definition.HttpStatus;
    }
}
