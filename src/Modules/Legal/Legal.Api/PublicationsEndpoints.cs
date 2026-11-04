using Advocacia.Modules.Legal.Application.Features.ChangePublicationStatus;
using Advocacia.Modules.Legal.Application.Features.GetPublicationById;
using Advocacia.Modules.Legal.Application.Features.GetPublicationsSummary;
using Advocacia.Modules.Legal.Application.Features.ListPublications;
using Advocacia.Modules.Legal.Application.Features.MarkPublicationAsRead;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Legal.Api;

/// <summary>
/// Endpoints de leitura das publicações capturadas (ver docs/modules/legal/publications.md).
///
/// Não há endpoint de CRIAÇÃO: publicação não é criada por usuário, é capturada do diário
/// pelo job (ver CnjCaptureJob). Expor um POST /publications daria a um cliente da API a
/// capacidade de inventar uma intimação — exatamente o tipo de coisa que não pode existir
/// num sistema onde prazo processual depende do que está persistido.
///
/// Todos exigem autenticação: o tenant vem do JWT (ICurrentUser), nunca da requisição.
/// </summary>
public static class PublicationsEndpoints
{
    public static IEndpointRouteBuilder MapPublicationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/publications", "Publications");

        // A rota estática precisa ser declarada ANTES de "/{id:guid}" para não ser capturada
        // por ela. A restrição :guid já protegeria, mas a ordem explícita evita depender disso.
        group.MapGet("/summary", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetPublicationsSummaryQuery(), cancellationToken)).ToHttpResult())
            .WithName("GetPublicationsSummary")
            .WithSummary("Contadores de publicações do escritório (total, pendentes de revisão, não lidas).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async ([AsParameters] ListPublicationsRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListPublicationsQuery(
                        request.Page ?? 1,
                        request.PageSize ?? 20,
                        request.Status,
                        request.IsRead,
                        request.ProcessId,
                        request.OnlyPendingReview ?? false,
                        request.ClienteId,
                        request.Tribunal,
                        request.DataInicio,
                        request.DataFim,
                        request.Busca,
                        request.Ordem),
                    cancellationToken)).ToHttpResult())
            .WithName("ListPublications")
            .WithSummary("Lista paginada de publicações. Devolve um trecho do conteúdo, nunca o inteiro teor.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetPublicationByIdQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetPublicationById")
            .WithSummary("Publicação completa, incluindo o inteiro teor — é a tela de leitura do advogado.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        // "Marcar como lida" é só o controle de leitura — explicitamente NÃO é aprovação. A
        // revisão humana obrigatória (ADR-012) é o endpoint de status logo abaixo.
        group.MapPatch("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new MarkPublicationAsReadCommand(id), cancellationToken)).ToHttpResult())
            .WithName("MarkPublicationAsRead")
            .WithSummary("Marca a publicação como lida. Não altera o status de revisão. 409 PUBLICATION_ALREADY_READ se já estava.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPatch("/{id:guid}/status", async (
                Guid id, ChangePublicationStatusRequest body, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ChangePublicationStatusCommand(id, body.Status, body.Notes), cancellationToken)).ToHttpResult())
            .WithName("ChangePublicationStatus")
            .WithSummary("Revisão humana: PendingReview → Approved/Corrected/Rejected/Archived. Transições inválidas dão 409 PUBLICATION_INVALID_STATUS_TRANSITION.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }
}

/// <summary>Filtros da listagem, ligados na query string via [AsParameters].</summary>
public sealed record ListPublicationsRequest(
    [FromQuery] int? Page,
    [FromQuery] int? PageSize,
    [FromQuery] string? Status, // um ou mais, separados por vírgula
    [FromQuery] bool? IsRead,
    [FromQuery] Guid? ProcessId,
    [FromQuery] bool? OnlyPendingReview,
    [FromQuery] Guid? ClienteId,
    [FromQuery] string? Tribunal,
    [FromQuery] DateOnly? DataInicio,
    [FromQuery] DateOnly? DataFim,
    [FromQuery] string? Busca,
    [FromQuery] string? Ordem);

/// <param name="Status">Approved, Corrected, Rejected ou Archived.</param>
public sealed record ChangePublicationStatusRequest(string Status, string? Notes);
