using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Clientes;

/// <summary>Nome da pessoa física ou razão social, normalizado (espaços colapsados).</summary>
public sealed class NomeCliente : ValueObject
{
    public const int MaxLength = 200;

    public string Value { get; }

    private NomeCliente(string value)
    {
        Value = value;
    }

    public static Result<NomeCliente> Create(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure<NomeCliente>(ErrorFactory.From(ErrorCode.CLIENT_NAME_REQUIRED));
        }

        var normalized = string.Join(' ', nome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<NomeCliente>(
                ErrorFactory.From(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED, nameof(NomeCliente)));
        }

        return Result.Success(new NomeCliente(normalized));
    }

    public static NomeCliente FromPersistence(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
