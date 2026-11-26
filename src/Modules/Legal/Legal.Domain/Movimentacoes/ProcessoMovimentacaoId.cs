namespace Advocacia.Modules.Legal.Domain.Movimentacoes;

public readonly record struct ProcessoMovimentacaoId(Guid Value)
{
    public static ProcessoMovimentacaoId New() => new(Guid.NewGuid());

    public static ProcessoMovimentacaoId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct NotificacaoClienteId(Guid Value)
{
    public static NotificacaoClienteId New() => new(Guid.NewGuid());

    public static NotificacaoClienteId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
