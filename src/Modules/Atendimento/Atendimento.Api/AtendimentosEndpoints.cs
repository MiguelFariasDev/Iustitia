using Advocacia.Modules.Atendimentos.Application.Features.ApiKeys;
using Advocacia.Modules.Atendimentos.Application.Features.Inbox;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Atendimentos.Api;

/// <summary>
/// Inbox do advogado (JWT). Exige <see cref="Policies.LawyerOrAbove"/>: a conversa de WhatsApp
/// com um cliente é sigilo profissional, estagiário não a lê. Chaves de API do Bot são do
/// <see cref="Policies.OwnerOnly"/> — emitir uma credencial que fala em nome do escritório é
/// decisão do dono.
/// </summary>
public static class AtendimentosEndpoints
{
    public static IEndpointRouteBuilder MapAtendimentosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/atendimentos", "Atendimentos");

        group.MapGet("/", async ([AsParameters] ListAtendimentosRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListAtendimentosQuery(
                        request.Page ?? 1, request.PageSize ?? 20, request.Status, request.AssignedUserId, request.Search),
                    cancellationToken)).ToHttpResult())
            .WithName("ListAtendimentos")
            .WithSummary("Fila do Inbox, com filtro por status, responsável e busca por nome/telefone.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetAtendimentoByIdQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetAtendimentoById")
            .WithSummary("Detalhe do atendimento.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}/mensagens", async (
                Guid id, [FromQuery] DateTimeOffset? before, [FromQuery] int? limit, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListMensagensQuery(id, before, limit ?? 50), cancellationToken)).ToHttpResult())
            .WithName("ListAtendimentoMensagens")
            .WithSummary("Histórico da conversa, em ordem cronológica. Use 'before' para carregar as anteriores.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/{id:guid}/mensagens", async (
                Guid id, SendMessageRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new SendHumanMessageCommand(id, request.Body), cancellationToken)).ToHttpResult())
            .WithName("SendAtendimentoMensagem")
            .WithSummary("O advogado envia uma mensagem (assume a conversa, se ainda não for dele) e o Bot a entrega.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPatch("/{id:guid}/status", async (
                Guid id, ChangeStatusRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ChangeAtendimentoStatusCommand(id, request.Status), cancellationToken)).ToHttpResult())
            .WithName("ChangeAtendimentoStatus")
            .WithSummary("Assume (Human), devolve ao bot (Bot) ou encerra (Closed) o atendimento.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPut("/{id:guid}/cliente", async (
                Guid id, LinkClienteRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new LinkClienteCommand(id, request.ClienteId), cancellationToken)).ToHttpResult())
            .WithName("LinkAtendimentoCliente")
            .WithSummary("Vincula o atendimento a um cliente do escritório.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        var keys = app.MapVersionedGroup("/bot/api-keys", "Bot");

        keys.MapPost("/", async (IssueBotApiKeyRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new IssueBotApiKeyCommand(request.Name), cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/bot/api-keys/{value.Id}", value)))
            .WithName("IssueBotApiKey")
            .WithSummary("Emite uma chave de API para o Bot do escritório. A chave em claro aparece UMA vez.")
            .RequireAuthorization(Policies.OwnerOnly);

        keys.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListBotApiKeysQuery(), cancellationToken)).ToHttpResult())
            .WithName("ListBotApiKeys")
            .WithSummary("Lista as chaves do Bot (sem o segredo).")
            .RequireAuthorization(Policies.OwnerOnly);

        keys.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new RevokeBotApiKeyCommand(id), cancellationToken)).ToHttpResult())
            .WithName("RevokeBotApiKey")
            .WithSummary("Revoga a chave. Rotação = emitir uma nova, trocar no Bot e revogar a antiga.")
            .RequireAuthorization(Policies.OwnerOnly);

        return app;
    }
}

public sealed record ListAtendimentosRequest(
    [FromQuery] int? Page,
    [FromQuery] int? PageSize,
    [FromQuery] string? Status,
    [FromQuery] Guid? AssignedUserId,
    [FromQuery] string? Search);

public sealed record SendMessageRequest(string Body);

public sealed record ChangeStatusRequest(string Status);

public sealed record LinkClienteRequest(Guid ClienteId);

public sealed record IssueBotApiKeyRequest(string Name);
