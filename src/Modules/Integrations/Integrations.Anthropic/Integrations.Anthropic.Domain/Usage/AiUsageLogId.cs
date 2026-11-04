namespace Advocacia.Modules.Integrations.Anthropic.Domain.Usage;

public readonly record struct AiUsageLogId(Guid Value)
{
    public static AiUsageLogId New() => new(Guid.NewGuid());

    public static AiUsageLogId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
