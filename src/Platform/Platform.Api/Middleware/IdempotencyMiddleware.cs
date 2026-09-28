using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.BuildingBlocks.Infrastructure.Observability;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Aplica idempotência (header <c>Idempotency-Key</c>, ver ADR-041) aos POSTs críticos
/// listados em <see cref="CriticalPathSuffixes"/> — criar tenant, convidar usuário, aceitar
/// convite. Fora dessa lista, a requisição segue direto para o próximo middleware, sem
/// custo. Roda depois de Authentication/TenantContext/Authorization (ver ordem em
/// Program.cs) — a chave de idempotência é sempre avaliada com o tenant/usuário já
/// resolvidos, então dois tenants nunca colidem na mesma chave por coincidência.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    private static readonly string[] CriticalPathSuffixes = ["/tenants", "/users/invite", "/users/accept-invite"];
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public async Task InvokeAsync(HttpContext context, ICacheService cacheService)
    {
        if (!ShouldApply(context))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyHeader)
            || string.IsNullOrWhiteSpace(idempotencyKeyHeader))
        {
            await WriteProblemAsync(context, ErrorCode.VALIDATION_REQUIRED_FIELD, "Idempotency-Key");
            return;
        }

        var idempotencyKey = idempotencyKeyHeader.ToString();
        var requestHash = await ComputeRequestHashAsync(context.Request);
        var cacheKey = $"idempotency:{idempotencyKey}";

        var existing = await cacheService.GetAsync<IdempotencyRecord>(cacheKey, context.RequestAborted);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                await WriteConflictAsync(context);
                return;
            }

            await ReplayResponseAsync(context, existing);
            return;
        }

        var originalBody = context.Response.Body;
        await using var capturedBody = new MemoryStream();
        context.Response.Body = capturedBody;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        capturedBody.Seek(0, SeekOrigin.Begin);

        // Só registra respostas de sucesso: uma falha (validação, erro de negócio) não deve
        // "travar" a chave — o cliente precisa poder corrigir o payload e tentar de novo com
        // a mesma Idempotency-Key.
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            var record = new IdempotencyRecord(requestHash, context.Response.StatusCode, context.Response.ContentType, capturedBody.ToArray());
            await cacheService.SetAsync(cacheKey, record, CacheDuration, context.RequestAborted);
        }

        capturedBody.Seek(0, SeekOrigin.Begin);
        await capturedBody.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static bool ShouldApply(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method)
        && CriticalPathSuffixes.Any(suffix => context.Request.Path.Value?.TrimEnd('/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase) == true);

    private static async Task<string> ComputeRequestHashAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hashBytes);
    }

    private static async Task ReplayResponseAsync(HttpContext context, IdempotencyRecord record)
    {
        context.Response.StatusCode = record.StatusCode;
        if (record.ContentType is not null)
        {
            context.Response.ContentType = record.ContentType;
        }

        context.Response.Headers["Idempotency-Replayed"] = "true";
        await context.Response.Body.WriteAsync(record.Body);
    }

    private static async Task WriteConflictAsync(HttpContext context)
    {
        var definition = ErrorCatalog.Get(ErrorCode.COMMON_CONFLICT);
        context.Response.StatusCode = definition.HttpStatus;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = definition.Code,
            status = definition.HttpStatus,
            detail = definition.Message,
            instance = context.Request.Path.Value,
            errorCode = definition.Code,
            errorGroup = definition.Group.ToString(),
            correlationId = LogEnrichmentContext.CorrelationId,
            timestamp = DateTimeOffset.UtcNow,
        }));
    }

    private static async Task WriteProblemAsync(HttpContext context, ErrorCode code, params object[] args)
    {
        var definition = ErrorCatalog.Get(code);
        var detail = args.Length > 0 ? string.Format(definition.Message, args) : definition.Message;

        context.Response.StatusCode = definition.HttpStatus;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = definition.Code,
            status = definition.HttpStatus,
            detail,
            instance = context.Request.Path.Value,
            errorCode = definition.Code,
            errorGroup = definition.Group.ToString(),
            correlationId = LogEnrichmentContext.CorrelationId,
            timestamp = DateTimeOffset.UtcNow,
        }));
    }

    private sealed record IdempotencyRecord(string RequestHash, int StatusCode, string? ContentType, byte[] Body);
}
