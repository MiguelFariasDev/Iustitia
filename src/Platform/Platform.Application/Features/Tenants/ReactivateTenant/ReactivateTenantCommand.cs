using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Tenants.ReactivateTenant;

public sealed record ReactivateTenantCommand(Guid TenantId) : ICommand<ReactivateTenantResponse>;
