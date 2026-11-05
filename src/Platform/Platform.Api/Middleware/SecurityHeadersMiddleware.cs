using Microsoft.Net.Http.Headers;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Caminhos que precisam de uma CSP mais frouxa que a da API (Swagger UI e dashboard do
/// Hangfire são páginas HTML com scripts/estilos inline). Todo o resto responde JSON e usa
/// <c>default-src 'none'</c>. Só expõe prefixos de rotas administrativas/de desenvolvimento.
/// </summary>
public sealed class SecurityHeadersOptions
{
    public IReadOnlyCollection<string> RelaxedCspPathPrefixes { get; set; } = [];
}

/// <summary>
/// Injeta os headers de segurança do OWASP Secure Headers em TODA resposta — inclusive as
/// geradas por middlewares que encerram o pipeline (401/403 de autenticação, 429 do rate
/// limiter, erros tratados). Por isso os headers são escritos em <c>OnStarting</c>, no último
/// instante antes de os headers irem para o cliente, e não antes de chamar <c>next</c>.
///
/// Desvio consciente em relação ao pedido original da Etapa 3.9: <c>X-XSS-Protection</c> vai
/// como <c>0</c>, não <c>1; mode=block</c>. O filtro XSS dos navegadores antigos foi removido
/// por introduzir vulnerabilidades (XS-Leaks e injeções via o próprio filtro); OWASP e
/// Mozilla recomendam desligá-lo explicitamente. A proteção real contra XSS é a CSP.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, SecurityHeadersOptions options)
{
    internal const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    internal const string RelaxedContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var (httpContext, securityOptions) = ((HttpContext, SecurityHeadersOptions))state;
            Apply(httpContext, securityOptions);
            return Task.CompletedTask;
        }, (context, options));

        return next(context);
    }

    private static void Apply(HttpContext context, SecurityHeadersOptions options)
    {
        var headers = context.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["X-XSS-Protection"] = "0";
        headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers.ContentSecurityPolicy = IsRelaxedPath(context.Request.Path, options)
            ? RelaxedContentSecurityPolicy
            : ApiContentSecurityPolicy;

        // Respostas da API carregam dados de clientes (sigilo profissional): nenhum cache
        // intermediário nem de navegador deve guardá-las, a menos que o endpoint já tenha
        // decidido o contrário explicitamente.
        if (!headers.ContainsKey(HeaderNames.CacheControl))
        {
            headers.CacheControl = "no-store";
        }

        // Headers que revelam a stack (versão do servidor/framework).
        headers.Remove(HeaderNames.Server);
        headers.Remove("X-Powered-By");
    }

    private static bool IsRelaxedPath(PathString path, SecurityHeadersOptions options)
    {
        foreach (var prefix in options.RelaxedCspPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
