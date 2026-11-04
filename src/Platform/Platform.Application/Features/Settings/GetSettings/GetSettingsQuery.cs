using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Settings.GetSettings;

/// <summary>Lista todas as configurações do tenant corrente (via ICurrentUser.TenantId).</summary>
public sealed record GetSettingsQuery : IQuery<GetSettingsResponse>;
