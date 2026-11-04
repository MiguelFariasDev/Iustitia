using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Tenants.GetTenantById;

public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<GetTenantByIdResponse>;
