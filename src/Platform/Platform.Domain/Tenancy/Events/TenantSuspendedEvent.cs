using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Platform.Domain.Tenancy.Events;

public sealed record TenantSuspendedEvent(TenantId TenantId) : DomainEvent;
