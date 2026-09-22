using System.Diagnostics;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Loga método, path, status e duração de cada requisição, com TenantId/UserId como
/// propriedades estruturadas (não como texto livre) — nunca loga corpo de requisição/
/// resposta, que poderia conter dados sensíveis (ver docs/requisitos/12-seguranca-lgpd-oab-detalhado.md).
/// Deve vir depois de TenantContextMiddleware na pipeline para já ter TenantId disponível.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(context);

        stopwatch.Stop();

        logger.LogInformation(
            "{Method} {Path} respondeu {StatusCode} em {ElapsedMilliseconds}ms (TenantId={TenantId}, UserId={UserId})",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            currentUser.TenantId,
            currentUser.UserId);
    }
}
