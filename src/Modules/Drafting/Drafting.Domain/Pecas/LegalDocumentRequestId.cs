namespace Advocacia.Modules.Drafting.Domain.Pecas;

public readonly record struct LegalDocumentRequestId(Guid Value)
{
    public static LegalDocumentRequestId New() => new(Guid.NewGuid());

    public static LegalDocumentRequestId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
