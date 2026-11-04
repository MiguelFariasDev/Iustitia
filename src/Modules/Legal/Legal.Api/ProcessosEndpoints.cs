using Advocacia.Modules.Legal.Application.Features.Processos;
using Advocacia.Platform.Api.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Legal.Api;

/// <summary>
/// Cadastro de processos. É o cadastro que DIRIGE a captura: a partir de um processo ativo,
/// o job passa a consultar o DJEN por aquele número — nenhuma publicação entra na base do
/// escritório sem um processo dele que a justifique (ver CnjCaptureJob).
///
/// Arquivar exige PartnerOrAbove: arquivar interrompe o acompanhamento de publicações, e um
/// processo arquivado por engano deixa de avisar sobre prazos em silêncio.
/// </summary>
public static class ProcessosEndpoints
{
    public static IEndpointRouteBuilder MapProcessosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/processos", "Processos");

        group.MapPost("/", async (CreateProcessoCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/processos/{value.Id}", value)))
            .WithName("CreateProcesso")
            .WithSummary("Cadastra um processo e passa a acompanhar suas publicações no DJEN.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/", async ([AsParameters] ListProcessosRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListProcessosQuery(
                        request.Page ?? 1, request.PageSize ?? 20, request.ClienteId,
                        request.ResponsavelUserId, request.Area, request.Status, request.Busca,
                        request.Tribunal, request.DistribuicaoDe, request.DistribuicaoAte),
                    cancellationToken)).ToHttpResult())
            .WithName("ListProcessos")
            .WithSummary("Lista paginada de processos, com filtros e busca por número ou parte contrária.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        // Antes de /{id:guid} de propósito: uma rota literal declarada depois de um
        // parâmetro nunca é alcançada.
        group.MapPost("/consultar-tribunal", async (
                ConsultarProcessoTribunalRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ConsultarProcessoTribunalQuery(request.CnjNumber), cancellationToken))
                    .ToHttpResult())
            .WithName("ConsultarProcessoTribunal")
            .WithSummary("Consulta os metadados do processo no tribunal para auto-preencher o cadastro.")
            .WithDescription(
                "Usa a API Pública do DataJud (CNJ). O tribunal é derivado do próprio número CNJ. " +
                "Responde 200 com found=false quando o tribunal não conhece o processo — o cadastro " +
                "manual continua disponível. Erros possíveis: PROCESS_CNJ_INVALID (número inválido), " +
                "CNJ_TRIBUNAL_NOT_SUPPORTED (tribunal fora do DataJud), CNJ_UNAVAILABLE, CNJ_TIMEOUT, " +
                "CNJ_RATE_LIMITED, COMMON_RATE_LIMITED (10 consultas/min por usuário). " +
                "USO RESTRITO A PORTFÓLIO — ver docs/compliance/datajud-termo.md.")
            .RequireAuthorization(Policies.LawyerOrAbove)
            .RequireRateLimiting(RateLimitingConfiguration.ExternalQueryPolicy);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetProcessoByIdQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetProcessoById")
            .WithSummary("Detalhe do processo.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPut("/{id:guid}", async (Guid id, UpdateProcessoRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new UpdateProcessoCommand(
                        id, request.ClienteId, request.ResponsavelUserId, request.Area, request.Foro,
                        request.Assunto, request.ParteContraria, request.ValorCausa,
                        request.DataDistribuicao, request.Observacoes),
                    cancellationToken)).ToHttpResult())
            .WithName("UpdateProcesso")
            .WithSummary("Atualiza os dados do processo. O número CNJ não é alterável.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPatch("/{id:guid}/status", async (Guid id, ChangeProcessoStatusRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ChangeProcessoStatusCommand(id, request.Status), cancellationToken)).ToHttpResult())
            .WithName("ChangeProcessoStatus")
            .WithSummary("Muda a situação do processo. Encerrado e arquivado interrompem a captura.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ArchiveProcessoCommand(id), cancellationToken)).ToHttpResult())
            .WithName("ArchiveProcesso")
            .WithSummary("Arquiva o processo — estado terminal, interrompe o acompanhamento.")
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }
}

public sealed record ListProcessosRequest(
    [FromQuery] int? Page,
    [FromQuery] int? PageSize,
    [FromQuery] Guid? ClienteId,
    [FromQuery] Guid? ResponsavelUserId,
    [FromQuery] string? Area,
    [FromQuery] string? Status,
    [FromQuery] string? Busca,
    [FromQuery] string? Tribunal,
    [FromQuery] DateOnly? DistribuicaoDe,
    [FromQuery] DateOnly? DistribuicaoAte);

public sealed record UpdateProcessoRequest(
    Guid ClienteId,
    Guid ResponsavelUserId,
    string Area,
    ForoRequest? Foro,
    string? Assunto,
    string? ParteContraria,
    decimal? ValorCausa,
    DateOnly? DataDistribuicao,
    string? Observacoes);

public sealed record ChangeProcessoStatusRequest(string Status);

/// <param name="CnjNumber">Número CNJ, com ou sem máscara. O tribunal é derivado dele.</param>
public sealed record ConsultarProcessoTribunalRequest(string CnjNumber);
