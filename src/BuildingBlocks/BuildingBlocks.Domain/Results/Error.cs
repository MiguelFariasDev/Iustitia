namespace Advocacia.BuildingBlocks.Domain.Results;

/// <summary>
/// Representa um erro de negócio de forma tipada, para uso com <see cref="Result"/>/<see cref="Result{TValue}"/>
/// em vez de exceptions. <see cref="Code"/> deve ser estável (ex.: "tenant.cnpj.invalid") para
/// permitir tratamento programático (i18n, mapeamento HTTP, etc.).
/// </summary>
/// <param name="Reference">
/// Identificador do lado de fora para suporte (ex.: <c>request-id</c> da Anthropic numa chamada que falhou).
/// Opcional e nunca exibido ao usuário final.
/// </param>
public sealed record Error(string Code, string Message, ErrorType Type, string? Reference = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
