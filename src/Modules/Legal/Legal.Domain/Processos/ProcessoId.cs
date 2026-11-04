namespace Advocacia.Modules.Legal.Domain.Processos;

public readonly record struct ProcessoId(Guid Value)
{
    public static ProcessoId New() => new(Guid.NewGuid());

    public static ProcessoId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
