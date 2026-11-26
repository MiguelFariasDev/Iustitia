using Advocacia.Modules.Legal.Application.Features.Movimentacoes;
using Advocacia.Modules.Legal.Application.Features.NotificacoesCliente;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Legal.Api;

/// <summary>
/// Acompanhamento de movimentações (ADR-075): timeline do processo, sincronização sob demanda e a
/// fila de revisão dos avisos ao cliente. Processo confidencial segue <c>ProcessoAcesso</c>: só o
/// responsável e a sociedade veem (os demais recebem 404).
///
/// Nenhum aviso sai sem um advogado chamar /enviar: o job só cria rascunhos.
/// </summary>
public static class MovimentacoesEndpoints
{
    public static IEndpointRouteBuilder MapMovimentacoesEndpoints(this IEndpointRouteBuilder app)
    {
        var processos = app.MapVersionedGroup("/processos", "Movimentações");

        processos.MapGet("/{id:guid}/movimentacoes", async (
                Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListMovimentacoesQuery(id, page ?? 1, pageSize ?? 20), cancellationToken)).ToHttpResult())
            .WithName("ListMovimentacoes")
            .WithSummary("Andamentos capturados do processo, do mais recente para o mais antigo.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        processos.MapPost("/{id:guid}/movimentacoes/sincronizar", async (
                Guid id, [FromQuery] string? fonte, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new SincronizarMovimentacoesCommand(id, OrigemJob: false, TrocarParaFonte: fonte), cancellationToken))
                    .ToHttpResult())
            .WithName("SincronizarMovimentacoes")
            .WithSummary("Busca agora os andamentos do processo no tribunal (juscraper; DataJud se não houver cobertura).")
            .WithDescription(
                "A primeira sincronização grava a base sem alertar. Depois, andamento novo gera alerta ao responsável " +
                "e rascunho de aviso ao cliente (nunca enviado sem revisão). ?fonte=juscraper|datajud troca a fonte " +
                "explicitamente, gravando nova base sem notificar.")
            .RequireAuthorization(Policies.LawyerOrAbove)
            .RequireRateLimiting(RateLimitingConfiguration.ExternalQueryPolicy);

        var avisos = app.MapVersionedGroup("/notificacoes-cliente", "Avisos ao cliente");

        avisos.MapGet("/", async (
                [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize,
                ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListNotificacoesClienteQuery(status, page ?? 1, pageSize ?? 20), cancellationToken))
                    .ToHttpResult())
            .WithName("ListNotificacoesCliente")
            .WithSummary("Fila de avisos ao cliente. ?status=PendenteRevisao traz os que esperam revisão.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        avisos.MapPost("/{id:guid}/enviar", async (
                Guid id, EnviarNotificacaoClienteRequest? request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new EnviarNotificacaoClienteCommand(id, request?.Mensagem), cancellationToken)).ToHttpResult())
            .WithName("EnviarNotificacaoCliente")
            .WithSummary("O advogado revisa e envia o aviso por e-mail (texto editado ou o rascunho).")
            .RequireAuthorization(Policies.LawyerOrAbove);

        avisos.MapPost("/{id:guid}/descartar", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DescartarNotificacaoClienteCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DescartarNotificacaoCliente")
            .WithSummary("Descarta o aviso: nada é enviado ao cliente.")
            .RequireAuthorization(Policies.LawyerOrAbove);

        return app;
    }
}

/// <param name="Mensagem">Texto revisado; nulo envia o rascunho como está.</param>
public sealed record EnviarNotificacaoClienteRequest(string? Mensagem);
