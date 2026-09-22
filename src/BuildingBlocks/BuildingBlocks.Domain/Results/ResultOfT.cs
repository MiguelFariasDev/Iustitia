namespace Advocacia.BuildingBlocks.Domain.Results;

/// <summary>
/// Resultado que carrega um valor em caso de sucesso. Acessar <see cref="Value"/>
/// em um resultado de falha lança exceção — sempre verifique <see cref="Result.IsSuccess"/> antes.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado de falha.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
