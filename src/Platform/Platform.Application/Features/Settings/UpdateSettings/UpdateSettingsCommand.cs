using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Settings.UpdateSettings;

/// <param name="Key">Chave normalizada (ver SettingKey) da configuração a criar/atualizar.</param>
/// <param name="ValueJson">Novo valor, como JSON — substitui o valor anterior por completo.</param>
public sealed record UpdateSettingsCommand(string Key, string ValueJson) : ICommand<UpdateSettingsResponse>;
