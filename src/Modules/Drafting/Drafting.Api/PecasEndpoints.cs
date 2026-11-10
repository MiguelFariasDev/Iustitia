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
/// Autenticação obrigatória (JWT). O escritório e o usuário vêm SEMPRE do token. O frontend nunca
/// vê URL, porta ou chave do Ollama/Qdrant: só fala com esta API.
/// </summary>
public static class PecasEndpoints
{
    public static IEndpointRouteBuilder MapPecasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/pecas", "Pecas");

        group.MapPost("/gerar", async Task<IResult> (
                GerarPecaRequest request, IPecaGenerationService service, CancellationToken cancellationToken) =>
            {
                // Valida e persiste ANTES de abrir o stream: erro de entrada é HTTP 4xx (ProblemDetails).
                var prepared = await service.PrepareAsync(request, cancellationToken);
                if (prepared.IsFailure)
                {
                    return prepared.ToHttpResult();
                }

                if (!request.Stream)
                {
                    return (await service.GenerateAsync(prepared.Value, cancellationToken)).ToHttpResult();
                }

                return new NdjsonStreamResult(service.StreamAsync(prepared.Value, cancellationToken));
            })
            .WithName("GerarPeca")
            .WithSummary("Gera uma minuta de peça com IA (RAG local). Com stream=true devolve NDJSON; com stream=false, um JSON.")
            .WithDescription(
                "Eventos NDJSON: {\"tipo\":\"fragmento\",\"conteudo\":\"…\"} (vários), depois " +
                "{\"tipo\":\"fim\",\"solicitacaoId\":…,\"fontes\":[…],\"avisos\":[…],\"aviso\":\"…\",\"tempoGeracaoMs\":…} " +
                "ou {\"tipo\":\"erro\",\"mensagem\":\"…\"}. Erros de entrada (400) chegam como ProblemDetails antes do stream. " +
                "A minuta é sempre rascunho: exige revisão e validação por profissional habilitado.")
            .RequireAuthorization(Policies.LawyerOrAbove)
            .RequireRateLimiting(RateLimitingConfiguration.AiGenerationPolicy);

        group.MapPost("/indexar", async (
                IndexDocumentRequest request, IDocumentIndexer indexer, CancellationToken cancellationToken) =>
                (await indexer.IndexAsync(request, cancellationToken)).ToHttpResult())
            .WithName("IndexarDocumentoParaPecas")
            .WithSummary("Indexa o texto de um documento no banco vetorial do escritório (substitui a indexação anterior do mesmo documento).")
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }
}
