using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.FeatureFlags.GetFeatureFlags;

/// <summary>Lista todas as feature flags do tenant corrente (via ICurrentUser.TenantId).</summary>
public sealed record GetFeatureFlagsQuery : IQuery<GetFeatureFlagsResponse>;
