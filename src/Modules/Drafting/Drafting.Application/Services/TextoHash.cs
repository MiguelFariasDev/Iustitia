using System.Security.Cryptography;
using System.Text;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Hashes usados para deduplicar conteúdo e registrar eventos sem guardar o texto.</summary>
public static class TextoHash
{
    /// <summary>SHA-256 do texto normalizado (espaços e caixa não contam), em hexadecimal minúsculo.</summary>
    public static string DoTexto(string texto)
    {
        var normalizado = string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizado))).ToLowerInvariant();
    }

    public static string DosBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
