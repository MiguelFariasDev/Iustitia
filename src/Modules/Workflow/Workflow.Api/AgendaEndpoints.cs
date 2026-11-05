using Advocacia.Modules.Workflow.Application.Features.Agenda;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Workflow.Api;

/// <summary>
/// Agenda do usuário autenticado: compromissos com horário (audiências, reuniões) e os prazos
/// (tarefas) que vencem no período, num só lugar.
/// </summary>
public static class AgendaEndpoints
{
    public static IEndpointRouteBuilder MapAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/agenda", "Agenda");

        group.MapGet("/", async (DateOnly? start, DateOnly? end, ISender sender, CancellationToken cancellationToken) =>
                start is null || end is null
                    ? Results.Problem(
                        title: "AGENDA_PERIOD_INVALID",
                        detail: "Informe start e end (AAAA-MM-DD).",
                        statusCode: StatusCodes.Status400BadRequest,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "AGENDA_PERIOD_INVALID" })
                    : (await sender.Send(new GetAgendaQuery(start.Value, end.Value), cancellationToken)).ToHttpResult())
            .WithName("GetAgenda")
            .WithSummary("Eventos e prazos do usuário entre start e end (AAAA-MM-DD, ambos inclusivos).")
            .WithDescription(
                "Cada item traz Date (o dia no fuso do escritório), para o app agrupar por dia. Prazos vêm com " +
                "Kind=Task, Type=Deadline e AllDay=true; eventos, com horário (StartsAt/EndsAt em UTC). " +
                "O intervalo tem no máximo 62 dias: AGENDA_PERIOD_INVALID (400) se faltar start/end, se end < start ou se passar disso.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/events", async (CreateCalendarEventCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/agenda/events/{value.Id}", value)))
            .WithName("CreateCalendarEvent")
            .WithSummary("Cria um compromisso (audiência, reunião ou outro) para o próprio usuário.")
            .WithDescription("Type: Hearing, Meeting ou Other. Erros: AGENDA_EVENT_PERIOD_INVALID (fim antes do início), campos obrigatórios (400).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }
}
