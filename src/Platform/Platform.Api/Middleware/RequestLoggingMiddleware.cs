using System.Diagnostics;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Loga método, path, status, duração, User-Agent e IP (mascarado — só o /24, ver
/// <see cref="MaskIp"/>) de cada requisição, com TenantId/UserId como propriedades
/// estruturadas (não como texto livre) — nunca loga corpo de requisição/resposta, que
/// poderia conter dados sensíveis (ver docs/requisitos/12-seguranca-lgpd-oab-detalhado.md).
/// ICurrentUser lê direto das claims do JWT (ver CurrentUserAccessor) — já disponível logo
/// após Authentication, então este middleware não depende de rodar depois de
/// TenantContextMiddleware (ver ordem do pipeline em Program.cs).
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(context);

        stopwatch.Stop();

        logger.LogInformation(
            "{Method} {Path} respondeu {StatusCode} em {ElapsedMilliseconds}ms " +
            "(TenantId={TenantId}, UserId={UserId}, Ip={Ip}, UserAgent={UserAgent})",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            currentUser.TenantId,
            currentUser.UserId,
            MaskIp(context.Connection.RemoteIpAddress),
            context.Request.Headers.UserAgent.ToString());
    }

    /// <summary>Mantém só o /24 (IPv4) ou /48 (IPv6) do endereço — suficiente para detectar padrões de abuso sem identificar o dispositivo exato (ver ADR-037/LGPD).</summary>
    private static string MaskIp(System.Net.IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        var bytes = address.GetAddressBytes();
        if (bytes.Length == 4)
        {
            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0";
        }

        if (bytes.Length == 16)
        {
            return $"{bytes[0]:x2}{bytes[1]:x2}:{bytes[2]:x2}{bytes[3]:x2}:{bytes[4]:x2}{bytes[5]:x2}::";
        }

        return "unknown";
    }
}
