using Advocacia.Modules.Legal.Domain.Movimentacoes;

namespace Advocacia.Modules.Legal.Application.Features.Movimentacoes;

internal static class MovimentacaoMapping
{
    internal static MovimentacaoResponse ToResponse(this ProcessoMovimentacao movimentacao) => new(
        movimentacao.Id.Value,
        movimentacao.Nome,
        movimentacao.Complemento,
        movimentacao.DataHora,
        movimentacao.Instancia,
        movimentacao.PossuiDocumento,
        movimentacao.Fonte,
        movimentacao.CapturadaEm);

    /// <summary>Como o andamento aparece no aviso ao cliente: nome e, quando acrescenta algo, o complemento.</summary>
    internal static AndamentoDoAviso ToAviso(this ProcessoMovimentacao movimentacao) => new(
        movimentacao.DataHora,
        string.IsNullOrWhiteSpace(movimentacao.Complemento)
            || movimentacao.Complemento.Equals(movimentacao.Nome, StringComparison.OrdinalIgnoreCase)
            ? movimentacao.Nome
            : $"{movimentacao.Nome} — {Resumir(movimentacao.Complemento)}");

    private static string Resumir(string complemento) =>
        complemento.Length <= 300 ? complemento : complemento[..300].TrimEnd() + "…";
}
