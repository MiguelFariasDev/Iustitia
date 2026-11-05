namespace Advocacia.Modules.CRM.Domain.Contacts;

public readonly record struct ContactId(Guid Value)
{
    public static ContactId New() => new(Guid.NewGuid());

    public static ContactId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
