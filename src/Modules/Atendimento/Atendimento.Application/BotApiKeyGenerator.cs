using System.Security.Cryptography;
using System.Text;

namespace Advocacia.Modules.Atendimentos.Application;

/// <summary>Geração e hash das chaves de API do Bot. Funções puras: o hash é o mesmo na emissão e na validação.</summary>
public static class BotApiKeyGenerator
{
    public const string KeyPrefix = "adv_bot_";
    private static readonly int PrefixDisplayLength = KeyPrefix.Length + 6;

    public static (string PlainText, string Prefix, string Hash) Generate()
    {
        var plain = KeyPrefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        return (plain, plain[..PrefixDisplayLength], Hash(plain));
    }

    public static string Hash(string plainText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainText))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
