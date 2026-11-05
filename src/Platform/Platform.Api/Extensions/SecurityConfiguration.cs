using Advocacia.Platform.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>
/// Configuração de transporte/borda da API: confiança nos proxies reversos (Front Door →
/// Container Apps) e registro dos headers de segurança. Ver ADR-067.
/// </summary>
public static class SecurityConfiguration
{
    /// <summary>
    /// Quantos proxies reversos existem entre o cliente e o Kestrel. Em produção a cadeia é
    /// Azure Front Door → ingress do Container Apps (2 saltos); sem proxy nenhum (dev local) é 0.
    /// </summary>
    public const string ForwardLimitKey = "Security:ForwardedHeaders:ForwardLimit";

    /// <summary>
    /// Sem isto, atrás do Front Door/ingress <c>RemoteIpAddress</c> é o IP do proxy — e o rate
    /// limiter por IP (login: 5 tentativas/5 min) passaria a contar TODOS os usuários do
    /// sistema num único balde: cinco logins errados de qualquer pessoa bloqueariam o login
    /// de todo mundo. Também faz <c>Request.IsHttps</c> refletir o esquema real do cliente.
    ///
    /// <c>KnownNetworks/KnownProxies</c> são limpos porque os proxies do Azure não têm IP
    /// fixo conhecido; a garantia de que só o proxy confiável alcança o container é de
    /// infraestrutura (restringir o ingress ao Front Door pelo header X-Azure-FDID — ver
    /// docs/compliance/security-hardening.md). Sem essa restrição, um cliente que fale direto
    /// com o Container App forja o X-Forwarded-For e escapa do rate limit por IP.
    /// </summary>
    public static IServiceCollection AddAdvocaciaForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        var forwardLimit = configuration.GetValue<int?>(ForwardLimitKey) ?? 1;

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = forwardLimit;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    public static IServiceCollection AddAdvocaciaSecurityHeaders(
        this IServiceCollection services, IEnumerable<string> relaxedCspPathPrefixes)
    {
        services.AddSingleton(new SecurityHeadersOptions
        {
            RelaxedCspPathPrefixes = relaxedCspPathPrefixes.Where(prefix => !string.IsNullOrWhiteSpace(prefix)).ToArray(),
        });

        return services;
    }

    /// <summary>
    /// Valida as origens CORS na subida: <c>*</c> nunca; só origens absolutas sem path; e
    /// <c>http://</c> apenas para localhost. Falha cedo, com mensagem clara, em vez de subir
    /// com uma política frouxa por causa de um typo de configuração.
    /// </summary>
    public static string[] ValidateCorsOrigins(IEnumerable<string> origins)
    {
        var validated = new List<string>();

        foreach (var origin in origins)
        {
            if (origin.Contains('*', StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins não aceita curingas ('{origin}'). Liste cada origem explicitamente.");
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || uri.PathAndQuery != "/"
                || origin.EndsWith('/'))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contém uma origem inválida: '{origin}'. Use o formato 'https://host[:porta]', sem path nem barra final.");
            }

            if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins só aceita http:// para localhost ('{origin}'). Use https.");
            }

            validated.Add(origin);
        }

        return [.. validated];
    }
}
