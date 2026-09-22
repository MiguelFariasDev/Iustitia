using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.BuildingBlocks.Domain.Errors;

/// <summary>
/// Ponto único para transformar um <see cref="ErrorCode"/> num <see cref="Error"/> do
/// Result Pattern — nunca construa um <see cref="Error"/> com string literal fora daqui
/// para erros de regra de negócio (ver docs/errors/conventions.md).
/// </summary>
public static class ErrorFactory
{
    /// <param name="code">Código do catálogo.</param>
    /// <param name="args">
    /// Argumentos para os placeholders (<c>{0}</c>, <c>{1}</c>, ...) da mensagem padrão do
    /// código, quando ela tiver algum. Omitido, usa a mensagem padrão sem formatação.
    /// </param>
    public static Error From(ErrorCode code, params object[] args)
    {
        var definition = ErrorCatalog.Get(code);
        var message = args.Length > 0 ? string.Format(definition.Message, args) : definition.Message;

        return new Error(definition.Code, message, definition.Type);
    }
}
