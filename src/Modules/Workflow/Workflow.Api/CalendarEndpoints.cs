using Advocacia.Modules.Workflow.Application.Features.Calendar;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Workflow.Api;

/// <summary>
/// Agenda do escritório. Todo o time (inclusive estagiário) lê e agenda; a exclusão exige
/// LawyerOrAbove — apagar um prazo é decisão de quem responde por ele.
/// </summary>
public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/agenda/eventos", "Agenda");

        group.MapGet("/", async ([AsParameters] ListCalendarEventsRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListCalendarEventsQuery(request.From, request.To, request.Tipo, request.ResponsavelId, request.ProcessoId),
                    cancellationToken)).ToHttpResult())
            .WithName("ListCalendarEvents")
            .WithSummary("Eventos da agenda que intersectam o período informado (máximo de 62 dias).")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetCalendarEventByIdQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetCalendarEventById")
            .WithSummary("Detalhe do evento.")
            .RequireAuthorization();

        group.MapPost("/", async (CreateCalendarEventCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/agenda/eventos/{value.Id}", value)))
            .WithName("CreateCalendarEvent")
            .WithSummary("Cria um evento (prazo, audiência, reunião ou tarefa), opcionalmente vinculado a um processo.")
            .RequireAuthorization();

        group.MapPut("/{id:guid}", async (Guid id, UpdateCalendarEventRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new UpdateCalendarEventCommand(
                        id, request.Title, request.Type, request.StartAt, request.EndAt, request.AllDay,
                        request.Description, request.Location, request.ProcessId, request.AssignedUserId),
                    cancellationToken)).ToHttpResult())
            .WithName("UpdateCalendarEvent")
            .WithSummary("Atualiza o evento.")
            .RequireAuthorization();

        group.MapPatch("/{id:guid}/reschedule", async (Guid id, RescheduleCalendarEventRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new RescheduleCalendarEventCommand(id, request.StartAt, request.EndAt, request.AllDay),
                    cancellationToken)).ToHttpResult())
            .WithName("RescheduleCalendarEvent")
            .WithSummary("Muda só data/hora do evento (arrastar e soltar). Zera os lembretes já enviados.")
            .RequireAuthorization();

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DeleteCalendarEventCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DeleteCalendarEvent")
            .WithSummary("Exclui o evento (exclusão lógica).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }
}

public sealed record ListCalendarEventsRequest(
    [FromQuery] DateTimeOffset From,
    [FromQuery] DateTimeOffset To,
    [FromQuery] string? Tipo,
    [FromQuery] Guid? ResponsavelId,
    [FromQuery] Guid? ProcessoId);

public sealed record UpdateCalendarEventRequest(
    string Title,
    string Type,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt = null,
    bool AllDay = false,
    string? Description = null,
    string? Location = null,
    Guid? ProcessId = null,
    Guid? AssignedUserId = null);

public sealed record RescheduleCalendarEventRequest(DateTimeOffset StartAt, DateTimeOffset? EndAt = null, bool? AllDay = null);
