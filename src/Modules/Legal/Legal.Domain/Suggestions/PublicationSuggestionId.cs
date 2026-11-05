namespace Advocacia.Modules.Legal.Domain.Suggestions;

public readonly record struct PublicationSuggestionId(Guid Value)
{
    public static PublicationSuggestionId New() => new(Guid.NewGuid());

    public static PublicationSuggestionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
