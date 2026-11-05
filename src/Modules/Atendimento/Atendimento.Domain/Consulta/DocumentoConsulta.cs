namespace Advocacia.Modules.Atendimentos.Domain.Consulta;

/// <summary>
/// Normalização do documento (CPF/CNPJ) digitado pelo cliente no WhatsApp: "123.456.789-09",
/// "123 456 789 09" e "12345678909" são o mesmo documento. O resultado é usado SÓ para a comparação
/// e nunca é gravado nem logado (ver ADR-068).
/// </summary>
public static class DocumentoConsulta
{
    public const int CpfLength = 11;
    public const int CnpjLength = 14;

    public static string Normalize(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsAsciiDigit).ToArray());

    /// <summary>
    /// Só o tamanho: dígitos verificadores não importam aqui. Um documento com dígito inválido simplesmente
    /// não confere com o do cliente e conta como falha, como qualquer outro erro — validar o dígito
    /// à parte só daria ao atacante um sinal a mais.
    /// </summary>
    public static bool HasPlausibleLength(string digits) => digits.Length is CpfLength or CnpjLength;
}
