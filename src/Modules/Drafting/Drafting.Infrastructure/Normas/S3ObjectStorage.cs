using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Advocacia.Modules.Drafting.Application.Normas;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Normas;

/// <summary>
/// Cliente S3 mínimo (AWS Signature V4) para Cloudflare R2, Backblaze B2 ou qualquer S3-compatível — só
/// PUT, GET, HEAD e COPY de objetos pequenos (Markdowns, ≤ 20 MB). Escrito à mão para não acrescentar o
/// AWSSDK inteiro (e os riscos de compatibilidade de checksum com R2/B2) por quatro operações.
/// Endereçamento "path-style": {endpoint}/{bucket}/{chave}. Só HTTPS; chaves só por variável de ambiente.
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly HttpClient _http;
    private readonly StorageOptions _options;
    private readonly Uri _endpoint;

    public S3ObjectStorage(HttpClient http, IOptions<NormasIngestaoOptions> options)
    {
        _http = http;
        _options = options.Value.Storage;
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Ingestao:Storage:Endpoint precisa ser uma URL https.");
        }

        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Ingestao:Storage:AccessKey/SecretKey não configuradas (use variáveis de ambiente).");
        }

        _endpoint = endpoint;
    }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, key, content);
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        await SendAsync(request, cancellationToken);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, key, []);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Head, key, []);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    public async Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        var source = $"/{_options.Bucket}/{EncodeKey(sourceKey)}";
        using var request = CreateRequest(HttpMethod.Put, destinationKey, [], ("x-amz-copy-source", source));
        var body = await SendAsync(request, cancellationToken);

        // S3 pode responder 200 com <Error> no corpo numa cópia que falhou no meio.
        if (body.Contains("<Error>", StringComparison.Ordinal))
        {
            throw new HttpRequestException("O storage recusou a cópia do objeto.");
        }
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Não devolve o corpo (pode ecoar nomes/ids); só o código.
            throw new HttpRequestException($"O storage respondeu HTTP {(int)response.StatusCode}.");
        }

        return body;
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method, string key, byte[] payload, (string Name, string Value)? extraHeader = null)
    {
        var path = $"/{_options.Bucket}/{EncodeKey(key)}";
        var uri = new Uri(_endpoint, path);
        var request = new HttpRequestMessage(method, uri);

        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var payloadHash = Hex(SHA256.HashData(payload));
        var host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = host,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = amzDate,
        };
        if (extraHeader is { } extra)
        {
            headers[extra.Name] = extra.Value;
        }

        var canonicalHeaders = string.Concat(headers.Select(h => $"{h.Key}:{h.Value.Trim()}\n"));
        var signedHeaders = string.Join(';', headers.Keys);
        var canonicalRequest = $"{method.Method}\n{uri.AbsolutePath}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        var scope = $"{dateStamp}/{_options.Regiao}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        var signingKey = Hmac(Hmac(Hmac(Hmac(
            Encoding.UTF8.GetBytes("AWS4" + _options.SecretKey), dateStamp), _options.Regiao), "s3"), "aws4_request");
        var signature = Hex(Hmac(signingKey, stringToSign));

        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        if (extraHeader is { } copy)
        {
            request.Headers.TryAddWithoutValidation(copy.Name, copy.Value);
        }

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"AWS4-HMAC-SHA256 Credential={_options.AccessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");

        return request;
    }

    /// <summary>Cada segmento codificado (sem tocar nas barras) — regra do SigV4 para o caminho canônico.</summary>
    private static string EncodeKey(string key) =>
        string.Join('/', key.TrimStart('/').Split('/').Select(Uri.EscapeDataString));

    private static byte[] Hmac(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
