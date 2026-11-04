namespace Advocacia.Modules.Legal.Domain.Clientes;

public readonly record struct ClienteId(Guid Value)
{
    public static ClienteId New() => new(Guid.NewGuid());

    public static ClienteId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
