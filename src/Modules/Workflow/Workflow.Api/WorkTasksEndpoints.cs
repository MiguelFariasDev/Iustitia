using Advocacia.Modules.Workflow.Application.Features.WorkTasks;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Workflow.Api;

/// <summary>
/// Tarefas e prazos do usuário autenticado. O usuário e o escritório vêm do JWT: ninguém lê ou
/// conclui a tarefa de outra pessoa (a tarefa de outro usuário responde 404, não 403).
///
/// O vencimento é uma DATA (AAAA-MM-DD) e "hoje" é o dia no fuso do escritório
/// (<c>Workflow:TimeZoneId</c>, padrão America/Sao_Paulo) — ver ADR-070.
/// </summary>
public static class WorkTasksEndpoints
{
    public static IEndpointRouteBuilder MapWorkTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/tasks", "Tasks");

        // Antes de /{id:guid} de propósito: uma rota literal declarada depois de um parâmetro nunca é alcançada.
        group.MapGet("/summary", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetWorkTasksSummaryQuery(), cancellationToken)).ToHttpResult())
            .WithName("GetWorkTasksSummary")
            .WithSummary("Contadores das tarefas do usuário: pendentes, vencendo hoje, atrasadas, próximos 7 dias e concluídas no mês.")
            .WithDescription(
                "Só tarefas PENDENTES entram em pendentes, hoje, atrasadas e próximos 7 dias. " +
                "\"Hoje\" é o dia no fuso do escritório. Próximos 7 dias vai de hoje até hoje + 7, inclusive.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async ([AsParameters] ListWorkTasksRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListWorkTasksQuery(request.Status, request.Filter, request.Page ?? 1, request.PageSize ?? 50),
                    cancellationToken)).ToHttpResult())
            .WithName("ListWorkTasks")
            .WithSummary("Lista paginada das tarefas do usuário, por vencimento (as mais próximas primeiro).")
            .WithDescription(
                "status: Pending ou Completed (vazio = ambos). filter: all, today, overdue ou next_7_days (vazio = all). " +
                "overdue só devolve tarefas pendentes. Valor inválido: TASK_FILTER_INVALID (400).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/", async (CreateWorkTaskCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/tasks/{value.Id}", value)))
            .WithName("CreateWorkTask")
            .WithSummary("Cria uma tarefa/prazo para o próprio usuário.")
            .WithDescription("Erros: TASK_DUE_DATE_INVALID, campos obrigatórios ou acima do tamanho (400).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPatch("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new CompleteWorkTaskCommand(id), cancellationToken)).ToHttpResult())
            .WithName("CompleteWorkTask")
            .WithSummary("Marca a tarefa como concluída. 409 TASK_ALREADY_COMPLETED se já estava; 404 TASK_NOT_FOUND se não é do usuário.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }
}

public sealed record ListWorkTasksRequest(
    [FromQuery] string? Status,
    [FromQuery] string? Filter,
    [FromQuery] int? Page,
    [FromQuery] int? PageSize);
