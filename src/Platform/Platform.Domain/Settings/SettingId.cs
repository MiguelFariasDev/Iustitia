namespace Advocacia.Platform.Domain.Settings;

public readonly record struct SettingId(Guid Value)
{
    public static SettingId New() => new(Guid.NewGuid());

    public static SettingId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
