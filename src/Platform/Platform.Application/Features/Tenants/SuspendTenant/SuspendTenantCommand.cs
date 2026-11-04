using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Tenants.SuspendTenant;

public sealed record SuspendTenantCommand(Guid TenantId) : ICommand<SuspendTenantResponse>;
