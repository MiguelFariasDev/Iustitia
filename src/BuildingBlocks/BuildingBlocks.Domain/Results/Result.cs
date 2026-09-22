namespace Advocacia.BuildingBlocks.Domain.Results;

/// <summary>
/// Resultado de uma operação que pode falhar por motivo de negócio (não excepcional).
/// Erros de negócio nunca devem ser representados por exceptions — exceptions ficam
/// reservadas para falhas realmente excepcionais/infraestrutura.
/// </summary>
public class Result
{
    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("Um resultado de sucesso não pode carregar um erro.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Um resultado de falha precisa de um erro.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}
