namespace Advocacia.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Criptografia autenticada de campos individuais e índice cego para busca por igualdade.
/// O parâmetro <c>context</c> (ex.: "cliente.documento") é amarrado ao texto cifrado como
/// dado autenticado adicional: um valor copiado de uma coluna para outra não decifra.
/// </summary>
public interface IFieldEncryptor
{
    string Encrypt(string plaintext, string context);

    /// <summary>Decifra um valor produzido por <see cref="Encrypt"/>. Aceita legado em texto puro se permitido.</summary>
    string Decrypt(string stored, string context);

    /// <summary>
    /// HMAC-SHA256 determinístico do valor (hex minúsculo, 64 caracteres). Permite igualdade
    /// e unicidade sobre um campo cifrado de forma não-determinística, sem expor o valor.
    /// O chamador normaliza antes (ex.: só dígitos do CPF).
    /// </summary>
    string BlindIndex(string value, string context);

    bool IsEncrypted(string stored);
}
