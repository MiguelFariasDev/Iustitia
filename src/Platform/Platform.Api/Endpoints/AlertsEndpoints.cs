using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Alerts;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Advocacia.Platform.Api.Endpoints;

/// <summary>
/// Alertas in-app do usuário autenticado. O usuário e o escritório vêm do JWT (ICurrentUser):
/// ninguém lê ou altera o alerta de outra pessoa, e não há endpoint de criação — alertas
/// nascem dos eventos do sistema (ver ADR-065).
/// </summary>
public static class AlertsEndpoints
{
    public static IEndpointRouteBuilder MapAlertsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/alerts", "Alerts");

        group.MapGet("/summary", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetAlertsSummaryQuery(), cancellationToken)).ToHttpResult())
            .WithName("GetAlertsSummary")
            .WithSummary("Contadores de alertas do usuário: total, não lidos e não lidos por tipo.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async ([AsParameters] ListAlertsRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListAlertsQuery(request.Page ?? 1, request.PageSize ?? 20, request.Type, request.IsRead, request.From, request.To),
                    cancellationToken)).ToHttpResult())
            .WithName("ListAlerts")
            .WithSummary("Lista paginada de alertas (mais recentes primeiro), sem os descartados.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetAlertByIdQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetAlertById")
            .WithSummary("Detalhe de um alerta. ALERT_NOT_FOUND (404) também para alerta de outro usuário.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPatch("/read-all", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new MarkAllAlertsAsReadCommand(), cancellationToken)).ToHttpResult())
            .WithName("MarkAllAlertsAsRead")
            .WithSummary("Marca todos os alertas não lidos do usuário como lidos. Devolve a quantidade alterada.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPatch("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new MarkAlertAsReadCommand(id), cancellationToken)).ToHttpResult())
            .WithName("MarkAlertAsRead")
            .WithSummary("Marca o alerta como lido. 409 ALERT_ALREADY_READ se já estava.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DismissAlertCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DismissAlert")
            .WithSummary("Descarta o alerta (some das listagens; o registro permanece para auditoria).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }
}

public sealed record ListAlertsRequest(
    [FromQuery] int? Page,
    [FromQuery] int? PageSize,
    [FromQuery] string? Type,
    [FromQuery] bool? IsRead,
    [FromQuery] DateTimeOffset? From,
    [FromQuery] DateTimeOffset? To);
