using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>
/// Roda logo após a autenticação (precisa vir depois de UseAuthentication — ver
/// EndpointRouteBuilderExtensions/Program.cs): lê o tenant_id já resolvido por ICurrentUser
/// (a partir das claims do JWT) e o define em ITenantContext, para que o filtro global do
/// EF Core e o TenantContextInterceptor (SET no Postgres, para RLS) enxerguem o tenant certo
/// em toda a requisição — ver ADR-004.
///
/// Usa ILogger.BeginScope (não Serilog.Context diretamente) para anexar TenantId/UserId aos
/// logs da requisição de forma agnóstica ao provider — a configuração completa do Serilog
/// (enrichers, sinks) fica para a Etapa 0.6 (Observabilidade).
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, ITenantContext tenantContext)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            await next(context);
            return;
        }

        tenantContext.SetTenant(tenantId);

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = tenantId,
            ["UserId"] = currentUser.UserId,
        }))
        {
            await next(context);
        }
    }
}
