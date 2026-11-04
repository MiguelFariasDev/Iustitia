using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Platform.Domain.Tenancy.Events;

public sealed record TenantCancelledEvent(TenantId TenantId) : DomainEvent;
