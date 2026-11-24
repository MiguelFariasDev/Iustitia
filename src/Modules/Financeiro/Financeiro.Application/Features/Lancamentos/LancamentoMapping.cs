using Advocacia.Modules.Financeiro.Domain.Lancamentos;

namespace Advocacia.Modules.Financeiro.Application.Features.Lancamentos;

internal static class LancamentoMapping
{
    /// <summary>Status exposto: "Atrasado" é derivado do vencimento, nunca gravado.</summary>
    internal static LancamentoResponse ToResponse(this Lancamento lancamento, DateOnly hoje) => new(
        lancamento.Id.Value,
        lancamento.Tipo.ToString(),
        lancamento.Descricao,
        lancamento.Pessoa,
        lancamento.Processo,
        lancamento.Valor,
        lancamento.Vencimento,
        lancamento.EstaAtrasado(hoje) ? "Atrasado" : lancamento.Status.ToString(),
        lancamento.PagoEm,
        lancamento.CreatedAt);
}
