namespace Advocacia.Modules.Legal.Domain.Publications;

public readonly record struct PublicationId(Guid Value)
{
    public static PublicationId New() => new(Guid.NewGuid());

    public static PublicationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
