using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Advocacia.Platform.Infrastructure.Auth.Identity;

/// <summary>
/// IP e User-Agent da requisição HTTP atual. O IP é o de <c>Connection.RemoteIpAddress</c> JÁ
/// corrigido pelo <c>UseForwardedHeaders</c> (primeiro middleware do pipeline) — o IP real do
/// cliente, não o do Front Door/ingress. Ver ADR-067.
/// </summary>
public sealed class HttpRequestOrigin(IHttpContextAccessor httpContextAccessor) : IRequestOrigin
{
    // audit_logs.user_agent é varchar(500); um User-Agent malicioso gigante não pode derrubar a gravação.
    private const int MaxUserAgentLength = 500;

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Length <= MaxUserAgentLength ? value : value[..MaxUserAgentLength];
        }
    }
}
