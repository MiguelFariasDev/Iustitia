using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.RateLimiting;
using Advocacia.BuildingBlocks.Domain.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>Categoria de limite aplicada a uma requisição (ver <see cref="RateLimitingConfiguration.Classify"/>).</summary>
public enum RateLimitCategory
{
    Login,
    ForgotPassword,
    Auth,
    Webhook,
    Write,
    Read,
}

/// <summary>
/// Rate limiting nativo do ASP.NET Core (ver ADR-039/067 e docs/api/conventions.md), num único
/// <see cref="RateLimiterOptions.GlobalLimiter"/>: cobre toda rota sem depender de alguém
/// lembrar de <c>RequireRateLimiting</c> num endpoint novo (fail closed por construção).
///
/// Limites (Etapa 3.9):
///   - <c>/auth/login</c>: 5 tentativas / 5 min por IP — força bruta de senha.
///   - <c>/auth/forgot-password</c>: 3 / 1 h por IP — abuso de envio de e-mail e enumeração.
///   - demais <c>/auth/*</c> (refresh, logout): 10 / min por IP.
///   - <c>/webhooks/*</c>: 1000 / min por IP — a Meta entrega em rajadas a partir de poucos IPs.
///   - escrita (POST/PUT/PATCH/DELETE): 60 / min por usuário.
///   - leitura (GET e demais): 300 / min por usuário.
///
/// "Por usuário" só funciona porque <c>UseRateLimiter</c> roda DEPOIS de
/// <c>UseAuthentication</c> (ver Program.cs): antes disso <c>HttpContext.User</c> está sempre
/// vazio e toda requisição cairia na partição por IP — um escritório inteiro atrás do mesmo
/// NAT dividiria 60 escritas por minuto. Requisição anônima particiona por IP.
///
/// Os contadores são em memória, POR RÉPLICA: com N réplicas o limite efetivo é até N vezes o
/// configurado. Aceito para o piloto (1–2 réplicas); limite distribuído (Redis) fica como
/// evolução, ver ADR-067.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Política nomeada para consultas a APIs públicas externas. Aplicada com
    /// <c>RequireRateLimiting</c> no endpoint, SOMANDO-SE ao limitador global: o global
    /// protege a nossa API, esta protege o serviço do CNJ — que pede expressamente que não
    /// se façam consultas em massa (ver ADR-064).
    /// </summary>
    public const string ExternalQueryPolicy = "external-query";

    private const int ExternalQueryPermitLimit = 10;
    private const int LoginPermitLimit = 5;
    private const int ForgotPasswordPermitLimit = 3;
    private const int AuthPermitLimit = 10;
    private const int WebhookPermitLimit = 1000;
    private const int WritePermitLimit = 60;
    private const int ReadPermitLimit = 300;

    // O Bot é UM cliente HTTP (um IP) que atende todos os contatos do escritório: com o limite
    // de escrita por IP (60/min), ~20 mensagens por minuto já o estrangulariam. A partição dele
    // é a própria chave de API, com folga proporcional ao volume de um escritório.
    private const int BotPermitLimit = 1200;
    private const string BotPathMarker = "/bot/atendimentos";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ForgotPasswordWindow = TimeSpan.FromHours(1);

    public static RateLimitCategory Classify(HttpRequest request)
    {
        // Normaliza: roteamento do ASP.NET ignora caixa e barra final, então o limitador também.
        var path = (request.Path.Value ?? string.Empty).TrimEnd('/');

        if (path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitCategory.Login;
        }

        if (path.EndsWith("/auth/forgot-password", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitCategory.ForgotPassword;
        }

        if (path.Contains("/auth/", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitCategory.Auth;
        }

        if (path.Contains("/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitCategory.Webhook;
        }

        return HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsDelete(request.Method)
            || HttpMethods.IsPatch(request.Method)
                ? RateLimitCategory.Write
                : RateLimitCategory.Read;
    }

    public static IServiceCollection AddAdvocaciaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var category = Classify(httpContext.Request);
                var ip = ClientIpKey(httpContext.Connection.RemoteIpAddress);

                return category switch
                {
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}", _ => NewWindowOptions(AuthPermitLimit));
                }

                // O limitador roda ANTES da autenticação (ver Program.cs), então a identidade do
                // Bot ainda não existe: particiona pelo hash da chave apresentada, nunca por ela
                // em claro. Chave inválida só gasta a cota da própria partição.
                if (path.Contains(BotPathMarker, StringComparison.OrdinalIgnoreCase)
                    && httpContext.Request.Headers.TryGetValue("X-Api-Key", out var apiKey)
                    && !string.IsNullOrWhiteSpace(apiKey))
                {
                    var keyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(apiKey.ToString())))[..16];
                    return RateLimitPartition.GetFixedWindowLimiter($"bot:{keyHash}", _ => NewWindowOptions(BotPermitLimit));
                }

                var partitionKey = httpContext.User.FindFirst("sub")?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                var isWrite = HttpMethods.IsPost(httpContext.Request.Method)
                    || HttpMethods.IsPut(httpContext.Request.Method)
                    || HttpMethods.IsDelete(httpContext.Request.Method)
                    || HttpMethods.IsPatch(httpContext.Request.Method);

                return isWrite
                    ? RateLimitPartition.GetFixedWindowLimiter($"write:{partitionKey}", _ => NewWindowOptions(WritePermitLimit))
                    : RateLimitPartition.GetFixedWindowLimiter($"read:{partitionKey}", _ => NewWindowOptions(ReadPermitLimit));
                    // Janela deslizante nas rotas de credencial: a janela fixa permite 2x o
                    // limite colado na virada (5 no fim de uma janela + 5 no começo da outra).
                    RateLimitCategory.Login => RateLimitPartition.GetSlidingWindowLimiter(
                        $"login:{ip}", _ => NewSlidingOptions(LoginPermitLimit, LoginWindow, segments: 5)),
                    RateLimitCategory.ForgotPassword => RateLimitPartition.GetSlidingWindowLimiter(
                        $"forgot:{ip}", _ => NewSlidingOptions(ForgotPasswordPermitLimit, ForgotPasswordWindow, segments: 6)),
                    RateLimitCategory.Auth => RateLimitPartition.GetFixedWindowLimiter(
                        $"auth:{ip}", _ => NewWindowOptions(AuthPermitLimit)),
                    RateLimitCategory.Webhook => RateLimitPartition.GetFixedWindowLimiter(
                        $"webhook:{ip}", _ => NewWindowOptions(WebhookPermitLimit)),
                    RateLimitCategory.Write => RateLimitPartition.GetFixedWindowLimiter(
                        $"write:{SubjectKey(httpContext, ip)}", _ => NewWindowOptions(WritePermitLimit)),
                    _ => RateLimitPartition.GetFixedWindowLimiter(
                        $"read:{SubjectKey(httpContext, ip)}", _ => NewWindowOptions(ReadPermitLimit)),
                };
            });

            // Partição por usuário: o limite é do advogado que está cadastrando, não do
            // escritório inteiro — dois advogados cadastrando ao mesmo tempo é uso normal.
            options.AddPolicy(ExternalQueryPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"external:{SubjectKey(httpContext, ClientIpKey(httpContext.Connection.RemoteIpAddress))}",
                    _ => NewWindowOptions(ExternalQueryPermitLimit)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                var definition = ErrorCatalog.Get(ErrorCode.COMMON_RATE_LIMITED);

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? (int)Math.Ceiling(retryAfterValue.TotalSeconds)
                    : (int)OneMinute.TotalSeconds;

                httpContext.Response.StatusCode = definition.HttpStatus;
                httpContext.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
                httpContext.Response.ContentType = "application/problem+json";

                // Sem o IP no log: é dado pessoal (LGPD) e o RequestLoggingMiddleware já
                // registra a requisição com o IP mascarado. Aqui só a categoria, para alarme.
                httpContext.RequestServices.GetService<ILoggerFactory>()
                    ?.CreateLogger(typeof(RateLimitingConfiguration).FullName!)
                    .LogWarning("Rate limit excedido na categoria {RateLimitCategory}", Classify(httpContext.Request));

                var problemDetails = new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = definition.Code,
                    status = definition.HttpStatus,
                    detail = definition.Message,
                    instance = httpContext.Request.Path.Value,
                    errorCode = definition.Code,
                    errorGroup = definition.Group.ToString(),
                    correlationId = BuildingBlocks.Infrastructure.Observability.LogEnrichmentContext.CorrelationId,
                    timestamp = DateTimeOffset.UtcNow,
                };

                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails), cancellationToken);
            };
        });

        return services;
    }

    /// <summary>Usuário autenticado (claim "sub"); IP quando anônimo.</summary>
    private static string SubjectKey(HttpContext httpContext, string ipKey)
    {
        var subject = httpContext.User.FindFirst("sub")?.Value;
        return string.IsNullOrEmpty(subject) ? $"ip:{ipKey}" : $"user:{subject}";
    }

    /// <summary>
    /// Chave de partição por cliente. IPv6 é agrupado no /64: um único assinante recebe um /64
    /// inteiro (2^64 endereços), e particionar por endereço completo deixaria o atacante trocar
    /// de IP a cada tentativa e nunca atingir o limite. IPv4 mapeado em IPv6 vira IPv4.
    /// </summary>
    public static string ClientIpKey(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        return $"{new IPAddress(bytes[..8].Concat(new byte[8]).ToArray())}/64";
    }

    private static FixedWindowRateLimiterOptions NewWindowOptions(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = OneMinute,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    };

    private static SlidingWindowRateLimiterOptions NewSlidingOptions(int permitLimit, TimeSpan window, int segments) => new()
    {
        PermitLimit = permitLimit,
        Window = window,
        SegmentsPerWindow = segments,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    };
}
