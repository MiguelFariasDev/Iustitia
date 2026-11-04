using Advocacia.Modules.Legal.Application.Features.Clientes;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Legal.Api;

/// <summary>
/// Cadastro de clientes do escritório.
///
/// Leitura e escrita exigem <see cref="Policies.LawyerOrAbove"/>: estagiário não cadastra
/// nem consulta carteira de clientes. A DESATIVAÇÃO exige PartnerOrAbove — tirar um cliente
/// de circulação afeta os processos dele e é decisão de sócio.
///
/// Não existe DELETE de verdade: cliente com processo tem histórico que precisa continuar
/// consultável (ver DeactivateClienteHandler).
/// </summary>
public static class ClientesEndpoints
{
    public static IEndpointRouteBuilder MapClientesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/clientes", "Clientes");

        group.MapPost("/", async (CreateClienteCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/clientes/{value.Id}", value)))
            .WithName("CreateCliente")
            .WithSummary("Cadastra um cliente (pessoa física ou jurídica).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async ([AsParameters] ListClientesRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListClientesQuery(
                        request.Page ?? 1, request.PageSize ?? 20, request.Tipo, request.ApenasAtivos, request.Busca),
                    cancellationToken)).ToHttpResult())
            .WithName("ListClientes")
            .WithSummary("Lista paginada de clientes, com busca por nome ou documento.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetClienteByIdQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetClienteById")
            .WithSummary("Detalhe do cliente.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPut("/{id:guid}", async (Guid id, UpdateClienteRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new UpdateClienteCommand(
                        id, request.Nome, request.Contato, request.Endereco,
                        request.DataNascimento, request.Observacoes),
                    cancellationToken)).ToHttpResult())
            .WithName("UpdateCliente")
            .WithSummary("Atualiza os dados cadastrais. Tipo e documento não são alteráveis.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DeactivateClienteCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DeactivateCliente")
            .WithSummary("Desativa o cliente. Não exclui — o histórico permanece consultável.")
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ReactivateClienteCommand(id), cancellationToken)).ToHttpResult())
            .WithName("ReactivateCliente")
            .WithSummary("Reativa um cliente desativado.")
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }
}

public sealed record ListClientesRequest(
    [FromQuery] int? Page,
    [FromQuery] int? PageSize,
    [FromQuery] string? Tipo,
    [FromQuery] bool? ApenasAtivos,
    [FromQuery] string? Busca);

/// <summary>O id vem da rota; o corpo não o repete, para não haver duas fontes divergentes.</summary>
public sealed record UpdateClienteRequest(
    string Nome,
    ContatoRequest? Contato,
    EnderecoRequest? Endereco,
    DateOnly? DataNascimento,
    string? Observacoes);
