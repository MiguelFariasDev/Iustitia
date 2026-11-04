namespace Advocacia.Platform.Application.Features.Settings.GetSettings;

public sealed record SettingItem(string Key, string ValueJson, DateTimeOffset UpdatedAt);

public sealed record GetSettingsResponse(IReadOnlyList<SettingItem> Items);
