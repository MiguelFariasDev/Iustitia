namespace Advocacia.Platform.Application.Features.Settings.UpdateSettings;

public sealed record UpdateSettingsResponse(string Key, string ValueJson, DateTimeOffset UpdatedAt);
