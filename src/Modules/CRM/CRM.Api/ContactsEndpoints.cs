using Advocacia.Modules.CRM.Application.Features.Contacts;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.CRM.Api;

/// <summary>
/// Contatos do escritório que não são clientes (parte contrária, testemunha, fornecedor).
/// Leitura e escrita exigem LawyerOrAbove; a DESATIVAÇÃO exige PartnerOrAbove. Não existe
/// DELETE de verdade — o histórico de quem foi parte ou testemunha precisa permanecer.
/// </summary>
public static class ContactsEndpoints
{
    public static IEndpointRouteBuilder MapContactsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/contatos", "Contatos");

        group.MapPost("/", async (CreateContactCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/contatos/{value.Id}", value)))
            .WithName("CreateContact")
            .WithSummary("Cadastra um contato (parte contrária, testemunha ou fornecedor).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async ([AsParameters] ListContactsRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListContactsQuery(
                        request.Page ?? 1, request.PageSize ?? 20, request.Tipo, request.ApenasAtivos, request.Busca),
                    cancellationToken)).ToHttpResult())
            .WithName("ListContacts")
            .WithSummary("Lista paginada de contatos, com busca por nome ou documento.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetContactByIdQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetContactById")
            .WithSummary("Detalhe do contato.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPut("/{id:guid}", async (Guid id, UpdateContactRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new UpdateContactCommand(
                        id, request.Type, request.Name, request.Document, request.Email, request.Phone, request.Notes),
                    cancellationToken)).ToHttpResult())
            .WithName("UpdateContact")
            .WithSummary("Atualiza os dados do contato.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DeactivateContactCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DeactivateContact")
            .WithSummary("Desativa o contato. Não exclui — o histórico permanece consultável.")
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ReactivateContactCommand(id), cancellationToken)).ToHttpResult())
            .WithName("ReactivateContact")
            .WithSummary("Reativa um contato desativado.")
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }
}

public sealed record ListContactsRequest(
    [FromQuery] int? Page,
    [FromQuery] int? PageSize,
    [FromQuery] string? Tipo,
    [FromQuery] bool? ApenasAtivos,
    [FromQuery] string? Busca);

public sealed record UpdateContactRequest(
    string Type,
    string Name,
    string? Document = null,
    string? Email = null,
    string? Phone = null,
    string? Notes = null);
