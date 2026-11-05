using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Advocacia.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Cifra a string ao gravar e decifra ao ler (EF Core). Nulos não passam pelo conversor — o EF
/// os grava como NULL. O resultado NÃO é comparável por igualdade (nonce aleatório): para buscar
/// ou impor unicidade use <see cref="BlindIndexConverter"/> numa coluna à parte.
/// </summary>
public sealed class EncryptedStringConverter(string context) : ValueConverter<string?, string?>(
    plaintext => plaintext == null ? null : FieldEncryption.Encrypt(plaintext, context),
    stored => stored == null ? null : FieldEncryption.Decrypt(stored, context));

/// <summary>
/// Grava o índice cego (HMAC) do valor; ao ler devolve o próprio hash (não é reversível).
/// Em consultas, o EF aplica este mesmo conversor ao PARÂMETRO comparado
/// (<c>c.DocumentoBusca == "12345678909"</c> vira <c>documento_busca = '&lt;hmac&gt;'</c>), então
/// o chamador escreve a consulta com o valor em claro.
/// </summary>
public sealed class BlindIndexConverter(string context) : ValueConverter<string, string>(
    value => FieldEncryption.BlindIndex(value, context),
    stored => stored);
