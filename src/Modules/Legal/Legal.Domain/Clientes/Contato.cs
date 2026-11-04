using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Clientes;

/// <summary>
/// Dados de contato do cliente. Agrupados num único value object porque são opcionais em
/// conjunto — um cliente pode ser cadastrado só com nome e documento, e é comum que seja
/// (a advogada cadastra o processo primeiro e completa o contato depois).
///
/// E-mail e telefone são dado pessoal (LGPD): nunca logar (ver claude.md, seção 14).
/// </summary>
public sealed partial class Contato : ValueObject
{
    public static readonly Contato Vazio = new(null, null);

    public string? Email { get; }

    /// <summary>Somente dígitos, com DDD. A máscara é responsabilidade da apresentação.</summary>
    public string? Telefone { get; }

    private Contato(string? email, string? telefone)
    {
        Email = email;
        Telefone = telefone;
    }

    public static Result<Contato> Create(string? email, string? telefone)
    {
        string? emailNormalizado = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            emailNormalizado = email.Trim().ToLowerInvariant();

            if (!EmailFormatRegex().IsMatch(emailNormalizado))
            {
                return Result.Failure<Contato>(ErrorFactory.From(ErrorCode.CLIENT_EMAIL_INVALID));
            }
        }

        string? telefoneNormalizado = null;
        if (!string.IsNullOrWhiteSpace(telefone))
        {
            var digits = new string(telefone.Where(char.IsDigit).ToArray());

            // 10 dígitos (fixo com DDD) ou 11 (celular com DDD). Fora disso é digitação
            // incompleta, e um telefone quebrado só aparece quando alguém precisa ligar.
            if (digits.Length is not (10 or 11))
            {
                return Result.Failure<Contato>(ErrorFactory.From(ErrorCode.CLIENT_PHONE_INVALID));
            }

            telefoneNormalizado = digits;
        }

        return Result.Success(new Contato(emailNormalizado, telefoneNormalizado));
    }

    public static Contato FromPersistence(string? email, string? telefone) => new(email, telefone);

    public string? TelefoneMascarado() => Telefone is null
        ? null
        : Telefone.Length == 11
            ? $"({Telefone[..2]}) {Telefone.Substring(2, 5)}-{Telefone.Substring(7, 4)}"
            : $"({Telefone[..2]}) {Telefone.Substring(2, 4)}-{Telefone.Substring(6, 4)}";

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormatRegex();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Email;
        yield return Telefone;
    }

    /// <summary>Não expõe e-mail nem telefone: são dado pessoal e ToString() vaza fácil em log.</summary>
    public override string ToString() =>
        $"Contato(email: {(Email is null ? "ausente" : "presente")}, telefone: {(Telefone is null ? "ausente" : "presente")})";
}
