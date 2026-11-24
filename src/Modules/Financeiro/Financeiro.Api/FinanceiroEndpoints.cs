using Advocacia.Modules.Financeiro.Application.Features.Lancamentos;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Advocacia.Modules.Financeiro.Api;

/// <summary>
/// Financeiro do escritório: honorários, contas a pagar e a receber. Tudo exige
/// <see cref="Policies.FinanceAccess"/> (titular, sócio ou financeiro): advogado e estagiário não veem
/// o caixa do escritório.
/// </summary>
public static class FinanceiroEndpoints
{
    public static IEndpointRouteBuilder MapFinanceiroEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/financeiro", "Financeiro");

        group.MapGet("/lancamentos", async ([AsParameters] ListLancamentosRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListLancamentosQuery(request.Status, request.Periodo, request.Page ?? 1, request.PageSize ?? 100),
                    cancellationToken)).ToHttpResult())
            .WithName("ListLancamentos")
            .WithSummary("Lançamentos por vencimento, com filtro de situação (Pendente, Pago, Atrasado) e período.")
            .RequireAuthorization(Policies.FinanceAccess);

        group.MapPost("/lancamentos", async (CreateLancamentoCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/financeiro/lancamentos/{value.Id}", value)))
            .WithName("CreateLancamento")
            .WithSummary("Registra um honorário, uma conta a pagar ou uma conta a receber.")
            .RequireAuthorization(Policies.FinanceAccess);

        group.MapPost("/lancamentos/{id:guid}/pagar", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new MarcarLancamentoPagoCommand(id), cancellationToken)).ToHttpResult())
            .WithName("MarcarLancamentoPago")
            .WithSummary("Marca o lançamento como pago, com a data e hora de agora.")
            .RequireAuthorization(Policies.FinanceAccess);

        group.MapDelete("/lancamentos/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DeleteLancamentoCommand(id), cancellationToken)).ToHttpResult())
            .WithName("DeleteLancamento")
            .WithSummary("Exclui o lançamento (exclusão lógica, auditada).")
            .RequireAuthorization(Policies.FinanceAccess);

        group.MapGet("/resumo", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetResumoFinanceiroQuery(), cancellationToken)).ToHttpResult())
            .WithName("GetResumoFinanceiro")
            .WithSummary("Faturamento do mês, a receber, a pagar e saldo — agregados no banco.")
            .RequireAuthorization(Policies.FinanceAccess);

        group.MapGet("/fluxo-caixa", async ([FromQuery] int? meses, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetFluxoCaixaQuery(meses ?? 6), cancellationToken)).ToHttpResult())
            .WithName("GetFluxoCaixa")
            .WithSummary("Entradas e saídas pagas por mês, dos últimos meses.")
            .RequireAuthorization(Policies.FinanceAccess);

        return app;
    }
}

public sealed record ListLancamentosRequest(
    [FromQuery] string? Status,
    [FromQuery] string? Periodo,
    [FromQuery] int? Page,
    [FromQuery] int? PageSize);
