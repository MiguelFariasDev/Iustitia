using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Platform.Domain.Identity.Events;

public sealed record UserDeactivatedEvent(UserId UserId, Guid TenantId) : DomainEvent;
