namespace Advocacia.Platform.Domain.Auditing;

public readonly record struct AuditLogId(Guid Value)
{
    public static AuditLogId New() => new(Guid.NewGuid());

    public static AuditLogId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
