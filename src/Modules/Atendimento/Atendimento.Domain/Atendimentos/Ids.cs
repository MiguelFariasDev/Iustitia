namespace Advocacia.Modules.Atendimentos.Domain.Atendimentos;

public readonly record struct AtendimentoId(Guid Value)
{
    public static AtendimentoId New() => new(Guid.NewGuid());

    public static AtendimentoId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct AtendimentoMensagemId(Guid Value)
{
    public static AtendimentoMensagemId New() => new(Guid.NewGuid());

    public static AtendimentoMensagemId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
