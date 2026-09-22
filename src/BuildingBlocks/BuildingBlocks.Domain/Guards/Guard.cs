namespace Advocacia.BuildingBlocks.Domain.Guards;

/// <summary>
/// Validações de pré-condição ("fail fast") para erros de programação — argumentos nulos,
/// vazios ou fora de faixa passados a construtores/métodos. Diferente do Result pattern:
/// Guard lança exceção porque indica uso incorreto da API, não uma regra de negócio violada
/// que o chamador deva tratar.
/// </summary>
public static class Guard
{
    public static T AgainstNull<T>(T? value, string paramName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(paramName);
    }

    public static string AgainstNullOrEmpty(string? value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("O valor não pode ser nulo ou vazio.", paramName);
        }

        return value;
    }

    public static string AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O valor não pode ser nulo, vazio ou conter apenas espaços.", paramName);
        }

        return value;
    }

    public static int AgainstNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "O valor não pode ser negativo.");
        }

        return value;
    }

    public static decimal AgainstNegative(decimal value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "O valor não pode ser negativo.");
        }

        return value;
    }

    public static Guid AgainstEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("O identificador não pode ser Guid.Empty.", paramName);
        }

        return value;
    }

    public static void AgainstOutOfRange(int value, int min, int max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"O valor deve estar entre {min} e {max}.");
        }
    }
}
