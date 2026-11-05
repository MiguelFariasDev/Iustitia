using System.Text.Json;
using System.Threading.RateLimiting;
using Advocacia.BuildingBlocks.Domain.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>
/// Rate limiting nativo do ASP.NET Core (ver docs/api/conventions.md), aplicado via um
/// único <see cref="RateLimiterOptions.GlobalLimiter"/> — cobre toda requisição sem precisar
/// de <c>RequireRateLimiting</c> em cada endpoint individualmente. Três categorias, cada
/// uma com sua própria partição (chave de contagem):
///   - Auth (path contém "/auth/"): 10 req/min por IP — login/refresh são o alvo típico de
///     força bruta, então a partição é por IP, não por usuário (que ainda não existe nessas
///     rotas).
///   - Escrita (POST/PUT/DELETE/PATCH): 60 req/min por usuário (claim "sub" do JWT; IP para
///     requisições anônimas).
///   - Leitura (GET/demais): 300 req/min por usuário (mesma regra de partição).
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
    private const int AuthPermitLimit = 10;
    private const int WritePermitLimit = 60;
    private const int ReadPermitLimit = 300;

    // O Bot é UM cliente HTTP (um IP) que atende todos os contatos do escritório: com o limite
    // de escrita por IP (60/min), ~20 mensagens por minuto já o estrangulariam. A partição dele
    // é a própria chave de API, com folga proporcional ao volume de um escritório.
    private const int BotPermitLimit = 1200;
    private const string BotPathMarker = "/bot/atendimentos";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAdvocaciaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var path = httpContext.Request.Path.Value ?? string.Empty;

                if (path.Contains("/auth/", StringComparison.OrdinalIgnoreCase))
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
            });

            // Partição por usuário: o limite é do advogado que está cadastrando, não do
            // escritório inteiro — dois advogados cadastrando ao mesmo tempo é uso normal.
            options.AddPolicy(ExternalQueryPolicy, httpContext =>
            {
                var partitionKey = httpContext.User.FindFirst("sub")?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"external:{partitionKey}", _ => NewWindowOptions(ExternalQueryPermitLimit));
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var definition = ErrorCatalog.Get(ErrorCode.COMMON_RATE_LIMITED);

                context.HttpContext.Response.StatusCode = definition.HttpStatus;
                context.HttpContext.Response.Headers.RetryAfter = ((int)Window.TotalSeconds).ToString();
                context.HttpContext.Response.ContentType = "application/problem+json";

                var problemDetails = new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = definition.Code,
                    status = definition.HttpStatus,
                    detail = definition.Message,
                    instance = context.HttpContext.Request.Path.Value,
                    errorCode = definition.Code,
                    errorGroup = definition.Group.ToString(),
                    correlationId = BuildingBlocks.Infrastructure.Observability.LogEnrichmentContext.CorrelationId,
                    timestamp = DateTimeOffset.UtcNow,
                };

                await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails), cancellationToken);
            };
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions NewWindowOptions(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = Window,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    };
}
