namespace Advocacia.Modules.Financeiro.Domain.Lancamentos;

/// <summary>
/// Situação GRAVADA. "Atrasado" não existe aqui de propósito: é derivado (pendente com vencimento
/// anterior a hoje) e, se fosse gravado, ficaria velho no dia seguinte sem ninguém atualizá-lo.
/// </summary>
public enum StatusLancamento
{
    Pendente,
    Pago,
}
