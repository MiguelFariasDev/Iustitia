using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Drafting.Api;

/// <summary>
/// Acervo normativo público (leis, súmulas, decisões). A ingestão em si roda no Worker; aqui só se pede
/// uma sincronização, se acompanha o histórico e se consulta a norma por trás de uma citação.
/// </summary>
public static class NormasEndpoints
{
    public static IEndpointRouteBuilder MapNormasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/normas", "Normas");

        // Rotas literais ANTES de /{id:guid}.
        group.MapPost("/admin/sincronizar", async Task<IResult> (bool? reprocessar, INormaAdminService service, CancellationToken cancellationToken) =>
            {
                var result = await service.SolicitarSincronizacaoAsync(reprocessar ?? false, cancellationToken);

                return result.ToHttpResult(value => Results.Accepted("/api/v1/normas/admin/jobs", value));
            })
            .WithName("SincronizarNormas")
            .WithSummary("Pede uma sincronização do acervo normativo. Responde 202: o worker executa (só arquivos novos, alterados ou removidos).")
            .WithDescription("?reprocessar=true ignora o hash e reindexa tudo que está no escopo (use só após mudar chunking ou modelo de embedding).")
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapGet("/admin/jobs", async (int? limite, INormaAdminService service, CancellationToken cancellationToken) =>
                (await service.ListarJobsAsync(limite ?? 20, cancellationToken)).ToHttpResult())
            .WithName("JobsIngestaoNormas")
            .WithSummary("Histórico das sincronizações do acervo (processados, ignorados, erros).")
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, INormaAdminService service, CancellationToken cancellationToken) =>
                (await service.ObterNormaAsync(id, cancellationToken)).ToHttpResult())
            .WithName("ObterNorma")
            .WithSummary("Metadados, vigência e fonte oficial de uma norma citada numa peça.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }
}
