namespace Advocacia.Modules.Financeiro.Domain.Lancamentos;

/// <summary>Persistido como texto (nome do membro): o nome não deve mudar.</summary>
public enum TipoLancamento
{
    /// <summary>Honorário do escritório a receber de cliente.</summary>
    Honorario,

    /// <summary>Despesa do escritório (saída).</summary>
    ContaPagar,

    /// <summary>Outro valor a receber de terceiros (custas reembolsáveis, acordos).</summary>
    ContaReceber,
}
