using System.Security.Cryptography;
using System.Text;

namespace Advocacia.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Valida a assinatura HMAC-SHA256 de webhooks no formato da Meta/WhatsApp:
/// header <c>X-Hub-Signature-256: sha256=&lt;hex&gt;</c> calculado sobre o corpo BRUTO da requisição
/// com o app secret. Regras que não podem ser relaxadas:
///   - o HMAC é sobre os BYTES recebidos, nunca sobre o JSON reserializado;
///   - a comparação é em tempo constante (<see cref="CryptographicOperations.FixedTimeEquals"/>);
///   - header ausente, sem o prefixo, não-hex ou de tamanho errado ⇒ inválido (fail closed);
///   - segredo vazio ⇒ exceção, nunca "aceita tudo".
/// Ainda não há endpoint de webhook (WhatsApp é Fase 9); quando houver, ele DEVE chamar isto antes
/// de desserializar o corpo, e a rota cai na categoria "Webhook" do rate limiter (/webhooks/*).
/// </summary>
public static class WebhookSignatureValidator
{
    private const string Prefix = "sha256=";

    public static bool IsValid(ReadOnlySpan<byte> rawBody, string? signatureHeader, string appSecret)
    {
        ArgumentException.ThrowIfNullOrEmpty(appSecret);

        if (string.IsNullOrWhiteSpace(signatureHeader)
            || !signatureHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signatureHeader.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), rawBody);

        // FixedTimeEquals devolve false (sem lançar) para tamanhos diferentes.
        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    /// <summary>Gera o header esperado — útil em testes e no simulador local de webhooks.</summary>
    public static string Sign(ReadOnlySpan<byte> rawBody, string appSecret) =>
        Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), rawBody));
}
