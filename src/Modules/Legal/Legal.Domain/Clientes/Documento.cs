using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Clientes;

/// <summary>
/// CPF (11 dígitos) ou CNPJ (14), com verificação dos dígitos verificadores. Um único value
/// object para os dois porque é um único campo do cliente — qual deles vale é decidido pelo
/// <see cref="TipoCliente"/>, e o cadastro precisa validar exatamente o que corresponde.
///
/// O algoritmo de CNPJ está duplicado em relação a Platform.Domain.Tenancy.CNPJ de propósito:
/// aquele é o CNPJ DO ESCRITÓRIO (com código de erro TENANT_CNPJ_INVALID e regra do núcleo da
/// plataforma), este é o documento DO CLIENTE do escritório. Compartilhá-los acoplaria o
/// módulo jurídico ao núcleo de tenancy — a duplicação de ~15 linhas de aritmética é mais
/// barata que essa dependência (ver ADR-055).
/// </summary>
public sealed class Documento : ValueObject
{
    private static readonly int[] CnpjFirstMultipliers = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] CnpjSecondMultipliers = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Somente dígitos — a máscara é responsabilidade da apresentação.</summary>
    public string Value { get; }

    public TipoCliente Tipo { get; }

    private Documento(string value, TipoCliente tipo)
    {
        Value = value;
        Tipo = tipo;
    }

    public static Result<Documento> Create(string? documento, TipoCliente tipo)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return Result.Failure<Documento>(ErrorFactory.From(ErrorCode.CLIENT_DOCUMENT_INVALID));
        }

        var digits = new string(documento.Where(char.IsDigit).ToArray());

        var valido = tipo switch
        {
            TipoCliente.PessoaFisica => digits.Length == 11 && HasValidCpfCheckDigits(digits),
            TipoCliente.PessoaJuridica => digits.Length == 14 && HasValidCnpjCheckDigits(digits),
            _ => false,
        };

        // Sequência de dígitos iguais (000…, 111…) passa na aritmética dos verificadores mas
        // não é documento de ninguém — é o valor que aparece quando alguém preenche o campo
        // só para o formulário deixar salvar.
        if (!valido || digits.Distinct().Count() == 1)
        {
            return Result.Failure<Documento>(ErrorFactory.From(ErrorCode.CLIENT_DOCUMENT_INVALID));
        }

        return Result.Success(new Documento(digits, tipo));
    }

    /// <summary>Reconstrói a partir do banco, sem revalidar (ver RawContent.FromPersistence).</summary>
    public static Documento FromPersistence(string value, TipoCliente tipo) => new(value, tipo);

    /// <summary>Forma mascarada para exibição: 000.000.000-00 ou 00.000.000/0000-00.</summary>
    public string ToMasked() => Tipo == TipoCliente.PessoaFisica
        ? $"{Value[..3]}.{Value.Substring(3, 3)}.{Value.Substring(6, 3)}-{Value.Substring(9, 2)}"
        : $"{Value[..2]}.{Value.Substring(2, 3)}.{Value.Substring(5, 3)}/{Value.Substring(8, 4)}-{Value.Substring(12, 2)}";

    private static bool HasValidCpfCheckDigits(string digits)
    {
        var first = CalculateCheckDigit(digits[..9], Enumerable.Range(0, 9).Select(i => 10 - i).ToArray());
        if (first != digits[9] - '0')
        {
            return false;
        }

        var second = CalculateCheckDigit(digits[..10], Enumerable.Range(0, 10).Select(i => 11 - i).ToArray());
        return second == digits[10] - '0';
    }

    private static bool HasValidCnpjCheckDigits(string digits)
    {
        var first = CalculateCheckDigit(digits[..12], CnpjFirstMultipliers);
        if (first != digits[12] - '0')
        {
            return false;
        }

        var second = CalculateCheckDigit(digits[..13], CnpjSecondMultipliers);
        return second == digits[13] - '0';
    }

    private static int CalculateCheckDigit(string digits, int[] multipliers)
    {
        var sum = digits.Select((c, i) => (c - '0') * multipliers[i]).Sum();
        var remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Devolve o documento MASCARADO PARCIALMENTE. CPF e CNPJ são dado pessoal (LGPD) e
    /// ToString() é o caminho mais fácil para eles vazarem num log estruturado — quem precisa
    /// do valor real usa <see cref="Value"/> explicitamente.
    /// </summary>
    public override string ToString() => Tipo == TipoCliente.PessoaFisica
        ? $"***.{Value.Substring(3, 3)}.***-**"
        : $"**.{Value.Substring(2, 3)}.***/****-**";
}
