using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Drafting.Api;

/// <summary>
/// Geração assistida de peças (ver docs/adr/072-geracao-de-pecas-com-rag-local.md).
///
/// A geração é ASSÍNCRONA: <c>POST /pecas/gerar</c> registra o pedido, enfileira e responde 202 na
/// hora; o processamento (inclusive ligar a GPU) acontece no worker. Acompanhe por <c>/status</c>
/// (polling), <c>/stream</c> (NDJSON em tempo real) ou pelo alerta in-app ao término.
///
/// Autenticação obrigatória (JWT). O escritório e o usuário vêm SEMPRE do token. O frontend nunca
/// vê URL, porta, chave, modelo ou instância: só fala com esta API.
/// </summary>
public static class PecasEndpoints
{
    public static IEndpointRouteBuilder MapPecasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/pecas", "Pecas");

        group.MapPost("/gerar", async (
                GerarPecaRequest request, IGeracaoPecaService service, CancellationToken cancellationToken) =>
                (await service.SubmeterAsync(request, cancellationToken))
                    .ToHttpResult(value => Results.Accepted(value.StatusUrl, value)))
            .WithName("GerarPeca")
            .WithSummary("Registra um pedido de minuta com IA e o enfileira. Responde 202 com o id; a geração é assíncrona.")
            .WithDescription(
                "Valida e sanitiza a entrada, grava a solicitação como Pendente e a coloca na fila. A GPU só é ligada " +
                "quando há trabalho. Acompanhe em GET /pecas/{id}/status (polling) ou GET /pecas/{id}/stream (NDJSON). " +
                "A minuta é sempre rascunho: exige revisão e validação por profissional habilitado.")
            .RequireAuthorization(Policies.LawyerOrAbove)
            .RequireRateLimiting(RateLimitingConfiguration.AiGenerationPolicy);

        group.MapGet("/", async (
                int? pagina, int? tamanho, IGeracaoPecaService service, CancellationToken cancellationToken) =>
                (await service.ListarAsync(pagina ?? 1, tamanho ?? 20, cancellationToken)).ToHttpResult())
            .WithName("ListarPecas")
            .WithSummary("Histórico de solicitações (as suas; sócios e titulares veem as do escritório).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        // Rota literal ANTES de /{id:guid}: uma rota fixa declarada depois de um parâmetro nunca é alcançada.
        group.MapGet("/admin/metricas", async (IPecaMetricasService metricas, CancellationToken cancellationToken) =>
                (await metricas.ObterAsync(cancellationToken)).ToHttpResult())
            .WithName("MetricasPecas")
            .WithSummary("Gerações, falhas, tempo médio e custo estimado do escritório.")
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapGet("/{id:guid}/status", async (Guid id, IGeracaoPecaService service, CancellationToken cancellationToken) =>
                (await service.ObterStatusAsync(id, cancellationToken)).ToHttpResult())
            .WithName("StatusPeca")
            .WithSummary("Status, etapa e progresso aproximado da solicitação.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, IGeracaoPecaService service, CancellationToken cancellationToken) =>
                (await service.ObterAsync(id, cancellationToken)).ToHttpResult())
            .WithName("ObterPeca")
            .WithSummary("A minuta (quando concluída), as fontes usadas, os avisos e os metadados.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapGet("/{id:guid}/stream", async Task<IResult> (Guid id, IGeracaoPecaService service, CancellationToken cancellationToken) =>
            {
                // Acesso e estado são verificados ANTES de abrir o stream (erro = HTTP 4xx).
                var opened = await service.AbrirStreamAsync(id, cancellationToken);

                return opened.IsFailure
                    ? opened.ToHttpResult()
                    : new NdjsonStreamResult(opened.Value);
            })
            .WithName("StreamPeca")
            .WithSummary("Acompanha a geração em tempo real (NDJSON). Só enquanto a solicitação está em processamento.")
            .WithDescription(
                "Eventos: {\"tipo\":\"status\",\"etapa\":\"…\",\"mensagem\":\"…\",\"progresso\":…}, " +
                "{\"tipo\":\"fragmento\",\"conteudo\":\"…\"}, {\"tipo\":\"fim\",\"fontes\":[…],\"avisos\":[…],\"aviso\":\"…\",\"tempoGeracaoMs\":…} " +
                "ou {\"tipo\":\"erro\",\"mensagem\":\"…\"}. Se aparecer status Pendente depois de fragmentos, a geração foi " +
                "reagendada e o texto recomeça: limpe o que já mostrou. 409 DRAFTING_NOT_STREAMING se já terminou.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/{id:guid}/cancelar", async (Guid id, IGeracaoPecaService service, CancellationToken cancellationToken) =>
                (await service.CancelarAsync(id, cancellationToken)).ToHttpResult())
            .WithName("CancelarPeca")
            .WithSummary("Cancela uma solicitação em andamento. 409 DRAFTING_CANNOT_CANCEL se já terminou.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        group.MapPost("/{id:guid}/regenerar", async (
                Guid id, RegenerarPecaRequest request, IGeracaoPecaService service, CancellationToken cancellationToken) =>
                (await service.RegenerarAsync(id, request, cancellationToken))
                    .ToHttpResult(value => Results.Accepted(value.StatusUrl, value)))
            .WithName("RegenerarPeca")
            .WithSummary("Cria uma nova versão da peça (histórico preservado). Exige \"confirmar\": true.")
            .RequireAuthorization(Policies.LawyerOrAbove)
            .RequireRateLimiting(RateLimitingConfiguration.AiGenerationPolicy);

        return app;
    }
}
