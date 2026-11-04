namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>Gera CNPJs matematicamente válidos (dígitos verificadores corretos) para testes.</summary>
internal static class TestCnpjGenerator
{
    private static readonly int[] FirstCheckDigitMultipliers = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondCheckDigitMultipliers = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static string Generate()
    {
        int[] digits;

        do
        {
            digits = Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(0, 10)).ToArray();
        }
        while (digits.Distinct().Count() == 1);

        var firstCheckDigit = CalculateCheckDigit(digits, FirstCheckDigitMultipliers);
        var withFirst = digits.Append(firstCheckDigit).ToArray();
        var secondCheckDigit = CalculateCheckDigit(withFirst, SecondCheckDigitMultipliers);

        return string.Concat(withFirst.Append(secondCheckDigit));
    }

    private static int CalculateCheckDigit(int[] digits, int[] multipliers)
    {
        var sum = digits.Select((digit, index) => digit * multipliers[index]).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
