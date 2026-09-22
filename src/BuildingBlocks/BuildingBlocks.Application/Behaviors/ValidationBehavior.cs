using System.Reflection;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Advocacia.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Valida a request com todos os <see cref="IValidator{T}"/> registrados para o tipo
/// (via <c>AddValidatorsFromAssembly</c>, ver Platform.Api/Extensions/
/// ServiceCollectionExtensions) antes do handler rodar. Em caso de falha, curto-circuita
/// sem chamar o próximo passo do pipeline e sem lançar exceção — retorna um
/// <see cref="Result"/>/<see cref="Result{TValue}"/> de falha com
/// <see cref="ErrorType.Validation"/>, já que todo Command/Query implementa
/// <c>IRequest&lt;Result&gt;</c> ou <c>IRequest&lt;Result{TValue}&gt;</c> (ver ICommand/IQuery).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var failures = validationResults.SelectMany(result => result.Errors).ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var errorCode = MapToErrorCode(failures[0]);
        var error = ErrorFactory.From(errorCode, failures[0].PropertyName);

        return BuildValidationFailureResponse(error);
    }

    /// <summary>
    /// FluentValidation preenche <see cref="ValidationFailure.ErrorCode"/> com o nome do
    /// validador interno (ex.: "NotEmptyValidator") a menos que a regra sobrescreva via
    /// <c>WithErrorCode</c> — mapeamento verificado empiricamente contra FluentValidation
    /// 11.11 (ver ADR-031). Com <c>CascadeMode.Stop</c> configurado globalmente (ver
    /// Platform.Application.DependencyInjection.AddPlatformApplication), só a primeira
    /// falha do objeto inteiro chega aqui — daí olhar apenas <c>failures[0]</c>.
    /// </summary>
    private static ErrorCode MapToErrorCode(ValidationFailure failure) => failure.ErrorCode switch
    {
        "NotEmptyValidator" or "NotNullValidator" => ErrorCode.VALIDATION_REQUIRED_FIELD,
        "EmailValidator" => ErrorCode.VALIDATION_INVALID_FORMAT,
        "MaximumLengthValidator" => ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED,
        "MinimumLengthValidator" => ErrorCode.VALIDATION_MIN_LENGTH_NOT_MET,
        "GreaterThanValidator" or "LessThanValidator"
            or "GreaterThanOrEqualValidator" or "LessThanOrEqualValidator" => ErrorCode.VALIDATION_OUT_OF_RANGE,
        "EnumValidator" => ErrorCode.VALIDATION_INVALID_ENUM,
        _ => ErrorCode.VALIDATION_INVALID_FORMAT,
    };

    /// <summary>
    /// TResponse é sempre <see cref="Result"/> ou <see cref="Result{TValue}"/> (nunca outro
    /// tipo — garantido pelas restrições de ICommand/ICommand{T}/IQuery{T}), mas o C# não
    /// permite expressar essa união como constraint genérica de IPipelineBehavior. Reflection
    /// aqui é a única forma de construir o Result{TValue}.Failure(error) correto sem cada
    /// handler precisar reimplementar essa checagem.
    /// </summary>
    private static TResponse BuildValidationFailureResponse(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, BindingFlags.Public | BindingFlags.Static, null, [typeof(Error)], null)!
                .MakeGenericMethod(valueType);

            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new InvalidOperationException(
            $"ValidationBehavior só suporta respostas Result/Result<T> — {typeof(TResponse).Name} não é suportado.");
    }
}
