namespace Advocacia.Modules.Financeiro.Domain.Lancamentos;

/// <summary>Como o lançamento nasceu: avulso, parcela de um valor total ou mês de uma cobrança mensal.</summary>
/// Persistido como texto (nome do membro): o nome não deve mudar.
public enum RecorrenciaLancamento
{
    /// <summary>Lançamento avulso.</summary>
    Unica = 0,

    /// <summary>Parcela de um valor total dividido em N vezes (vencimentos mensais).</summary>
    Parcelado = 1,

    /// <summary>Mês de uma cobrança mensal: o mesmo valor se repete por N meses.</summary>
    Mensal = 2,
}
