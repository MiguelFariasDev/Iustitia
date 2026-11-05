using Advocacia.Modules.Atendimentos.Application.Features.Bot;
using Advocacia.Modules.Atendimentos.Application.Features.Consulta;
using Advocacia.Platform.Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Atendimentos.Api;

/// <summary>
/// API server-to-server consumida pelo Bot (<c>X-Api-Key</c>). O escritório vem da chave,
/// nunca do corpo. Não há JWT aqui, e nenhuma policy de advogado aceita a chave do Bot.
/// </summary>
public static class BotEndpoints
{
    public static IEndpointRouteBuilder MapBotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/bot/atendimentos", "Bot")
            .RequireAuthorization(BotAuthentication.Policy)
            .AddEndpointFilter<BotTenantEndpointFilter>();

        group.MapPost("/", async (StartAtendimentoCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .WithName("BotStartAtendimento")
            .WithSummary("Abre (ou recupera) o atendimento em aberto do telefone. Idempotente.");

        group.MapPost("/{id:guid}/mensagens", async (
                Guid id, BotMessageRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new AddBotMessageCommand(id, request.Direction, request.Sender, request.Body, request.ExternalId),
                    cancellationToken)).ToHttpResult())
            .WithName("BotAddMessage")
            .WithSummary("Registra uma mensagem recebida do cliente ou enviada pelo bot. Idempotente por externalId.");

        group.MapPost("/{id:guid}/handoff", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RequestHandoffCommand(id), cancellationToken);
                return result.IsSuccess ? Results.Ok(new HandoffResponse(true)) : result.ToHttpResult();
            })
            .WithName("BotRequestHandoff")
            .WithSummary("O cliente pediu um humano: o atendimento vai para PendingHuman. Idempotente.");

        group.MapGet("/{id:guid}/status", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetAtendimentoStatusQuery(id), cancellationToken)).ToHttpResult())
            .WithName("BotGetAtendimentoStatus")
            .WithSummary("Status atual — o Bot consulta ANTES de responder qualquer mensagem.");

        // Consulta de andamento de processo, com verificação de identidade feita NO CORE (ADR-068).
        // Só funciona com o bot conduzindo a conversa (senão 409); o escritório vem da chave.
        group.MapPost("/{id:guid}/consulta/iniciar", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new IniciarConsultaCommand(id), cancellationToken)).ToHttpResult())
            .WithName("BotIniciarConsulta")
            .WithSummary("Inicia a verificação de identidade. Telefone sem cadastro responde 'Negado', como um documento errado.");

        group.MapPost("/{id:guid}/consulta/verificar", async (
                Guid id, VerificarConsultaRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new VerificarConsultaCommand(id, request.Documento ?? string.Empty), cancellationToken)).ToHttpResult())
            .WithName("BotVerificarConsulta")
            .WithSummary("Confere o documento do cliente. 3 falhas bloqueiam por 30 min. Verificado devolve a lista de processos.");

        group.MapGet("/{id:guid}/consulta/processos/{indice:int}", async (
                Guid id, int indice, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetConsultaProcessoQuery(id, indice), cancellationToken)).ToHttpResult())
            .WithName("BotGetConsultaProcesso")
            .WithSummary("Título, fase e último andamento de um processo da lista liberada pela verificação.");

        return app;
    }
}

/// <summary>O id vem da rota; o corpo não o repete.</summary>
public sealed record BotMessageRequest(string Direction, string Sender, string Body, string? ExternalId);

public sealed record HandoffResponse(bool Success);

/// <summary>
/// O documento é dado pessoal: o <c>ToString</c> gerado para o record o mostraria, então é sobrescrito.
/// </summary>
public sealed record VerificarConsultaRequest(string? Documento)
{
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Documento = ***");
        return true;
    }
}
