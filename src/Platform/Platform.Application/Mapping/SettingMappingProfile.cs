using Advocacia.Platform.Application.Features.Settings.GetSettings;
using Advocacia.Platform.Domain.Settings;
using Mapster;

namespace Advocacia.Platform.Application.Mapping;

public sealed class SettingMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config) =>
        config.NewConfig<Setting, SettingItem>()
            .Map(dest => dest.Key, src => src.Key.Value);
}
