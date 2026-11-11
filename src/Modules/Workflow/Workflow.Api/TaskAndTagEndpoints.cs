using Advocacia.Modules.Workflow.Application.Features.Tags;
using Advocacia.Modules.Workflow.Application.Features.Tasks;
using Advocacia.Platform.Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Workflow.Api;

/// <summary>Tarefas do escritório e tags de processos/advogados. Todo o time lê e edita.</summary>
public static class TaskAndTagEndpoints
{
    public static IEndpointRouteBuilder MapTaskAndTagEndpoints(this IEndpointRouteBuilder app)
    {
        var tasks = app.MapVersionedGroup("/tarefas", "Tarefas");

        tasks.MapGet("/", async ([AsParameters] ListWorkTasksRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListWorkTasksQuery(request.ProcessoId, request.ResponsavelId), cancellationToken)).ToHttpResult())
            .WithName("ListWorkTasks")
            .WithSummary("Tarefas do escritório, opcionalmente filtradas por processo ou responsável.")
            .RequireAuthorization();

        tasks.MapPost("/", async (CreateWorkTaskCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/tarefas/{value.Id}", value)))
            .WithName("CreateWorkTask")
            .WithSummary("Cria uma tarefa, opcionalmente ligada a um processo e a um responsável.")
            .RequireAuthorization();

        tasks.MapPut("/{id:guid}", async (Guid id, UpdateWorkTaskRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new UpdateWorkTaskCommand(id, request.Title, request.DueDate, request.Description, request.ProcessId, request.AssignedUserId),
                    cancellationToken)).ToHttpResult())
            .WithName("UpdateWorkTask")
            .WithSummary("Atualiza a tarefa.")
            .RequireAuthorization();

        tasks.MapPatch("/{id:guid}/status", async (Guid id, ChangeWorkTaskStatusRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ChangeWorkTaskStatusCommand(id, request.Status), cancellationToken)).ToHttpResult())
            .WithName("ChangeWorkTaskStatus")
            .WithSummary("Move a tarefa de coluna (Todo, InProgress, InReview, Done).")
            .RequireAuthorization();

        tasks.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DeleteWorkTaskCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DeleteWorkTask")
            .WithSummary("Exclui a tarefa (exclusão lógica).")
            .RequireAuthorization();

        var tags = app.MapVersionedGroup("/tags", "Tags");

        tags.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListTagsQuery(), cancellationToken)).ToHttpResult())
            .WithName("ListTags")
            .WithSummary("Tags do escritório e onde cada uma está aplicada.")
            .RequireAuthorization();

        tags.MapPost("/", async (CreateTagCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .WithName("CreateTag")
            .WithSummary("Cria uma tag; se o nome já existe, devolve a existente.")
            .RequireAuthorization();

        tags.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DeleteTagCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DeleteTag")
            .WithSummary("Apaga a tag e todas as ligações dela.")
            .RequireAuthorization();

        tags.MapPut("/atribuicoes", async (SetTagAssignmentCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .WithName("SetTagAssignment")
            .WithSummary("Coloca ou tira uma tag de um processo ou advogado (idempotente).")
            .RequireAuthorization();

        return app;
    }
}

public sealed record ListWorkTasksRequest([FromQuery] Guid? ProcessoId, [FromQuery] Guid? ResponsavelId);

public sealed record UpdateWorkTaskRequest(
    string Title,
    DateOnly? DueDate = null,
    string? Description = null,
    Guid? ProcessId = null,
    Guid? AssignedUserId = null);

public sealed record ChangeWorkTaskStatusRequest(string Status);
