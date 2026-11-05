using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Atendimentos.Api;

/// <summary>
/// Define o tenant da requisição do Bot. O <c>TenantContextMiddleware</c> roda ANTES da
/// autorização e só enxerga o JWT padrão; a chave de API é autenticada depois, pela policy do
/// endpoint. Sem este filtro o DbContext rodaria sem tenant (sem filtro global e com o RLS
/// devolvendo zero linhas) — falharia fechado, mas o Bot nunca funcionaria.
///
/// É filtro de endpoint (e não middleware) porque o tenant fica num AsyncLocal: só vale para o
/// que roda DENTRO da chamada que o definiu.
/// </summary>
public sealed class BotTenantEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var claim = context.HttpContext.User.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(claim, out var tenantId))
        {
            return ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        context.HttpContext.RequestServices.GetRequiredService<ITenantContext>().SetTenant(tenantId);

        return next(context);
    }
}
