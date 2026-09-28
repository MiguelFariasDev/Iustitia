using System.Reflection;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Me.GetMe;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Advocacia.Platform.Api.Endpoints;

/// <summary>
/// Endpoints de status versionados (ver Parte 1.3/10 da Etapa 0.7) — complementam
/// (não substituem) os endpoints raiz /health/live, /health/ready e /health (Etapa 0.6,
/// usados por orquestradores de container que não conhecem versionamento de API).
/// </summary>
public static class StatusEndpoints
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup(string.Empty, "Status");

        group.MapGet("/health/live", () => Results.Ok())
            .WithSummary("Liveness probe")
            .WithDescription("Confirma que o processo está de pé, sem checar dependências externas.")
            .AllowAnonymous();

        group.MapGet("/health/ready", async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
            {
                var report = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
                return report.Status == HealthStatus.Unhealthy ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : Results.Ok();
            })
            .WithSummary("Readiness probe")
            .WithDescription("Retorna 503 se alguma dependência crítica (Postgres, Redis, mensageria, outbox) estiver fora do ar.")
            .AllowAnonymous();

        group.MapGet("/version", () => Results.Ok(new
            {
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                buildDate = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location),
            }))
            .WithSummary("Versão da API")
            .WithDescription("Versão do assembly, ambiente e data de build — útil para confirmar qual deploy está no ar.")
            .AllowAnonymous();

        group.MapGet("/me", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetMeQuery(), cancellationToken)).ToHttpResult())
            .WithSummary("Usuário autenticado")
            .WithDescription("Dados do usuário logado, extraídos do JWT validado (nenhuma consulta ao banco).")
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        return app;
    }
}
