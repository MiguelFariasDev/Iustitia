namespace Advocacia.Modules.Financeiro.Domain.Lancamentos;

public readonly record struct LancamentoId(Guid Value)
{
    public static LancamentoId New() => new(Guid.NewGuid());

    public static LancamentoId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
