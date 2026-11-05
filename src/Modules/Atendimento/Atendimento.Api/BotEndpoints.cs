using Advocacia.Modules.Atendimentos.Application.Features.Bot;
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

        return app;
    }
}

/// <summary>O id vem da rota; o corpo não o repete.</summary>
public sealed record BotMessageRequest(string Direction, string Sender, string Body, string? ExternalId);

public sealed record HandoffResponse(bool Success);
