using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.FeatureFlags.UpdateFeatureFlag;

/// <param name="Key">Chave normalizada (ver FeatureFlagKey) da feature flag a criar/atualizar.</param>
/// <param name="IsEnabled">Estado desejado — idempotente: chamar de novo com o mesmo valor não falha.</param>
public sealed record UpdateFeatureFlagCommand(string Key, bool IsEnabled) : ICommand<UpdateFeatureFlagResponse>;
