using System.Net.Http.Json;
using System.Text.Json;

namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Minimal APIs serializam a resposta com os "web defaults" do System.Text.Json
/// (propriedades em camelCase) — estes helpers desserializam com as mesmas opções, para
/// que os testes possam usar os records de resposta (PascalCase) sem cada teste ter que
/// lembrar de configurar isso.
/// </summary>
public static class HttpJsonExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Task<T?> ReadAsWebJsonAsync<T>(this HttpContent content, CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(Options, cancellationToken);

    /// <summary>
    /// POST com um Idempotency-Key novo por chamada (ver ADR-041/IdempotencyMiddleware) —
    /// use no lugar de <see cref="HttpClientJsonExtensions.PostAsJsonAsync{TValue}(HttpClient, string?, TValue, CancellationToken)"/>
    /// para os POSTs críticos (/tenants, /users/invite, /users/accept-invite), que exigem o
    /// header.
    /// </summary>
    public static async Task<HttpResponseMessage> PostIdempotentAsJsonAsync<TValue>(
        this HttpClient client, string requestUri, TValue value, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(value),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        return await client.SendAsync(request, cancellationToken);
    }
}
