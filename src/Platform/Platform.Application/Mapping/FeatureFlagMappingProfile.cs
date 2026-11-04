using Advocacia.Platform.Application.Features.FeatureFlags.GetFeatureFlags;
using Advocacia.Platform.Domain.FeatureFlags;
using Mapster;

namespace Advocacia.Platform.Application.Mapping;

public sealed class FeatureFlagMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config) =>
        config.NewConfig<FeatureFlag, FeatureFlagItem>()
            .Map(dest => dest.Key, src => src.Key.Value);
}
