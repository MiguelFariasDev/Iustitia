using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Platform.Domain.Identity.Events;

public sealed record UserCreatedEvent(UserId UserId, Guid TenantId) : DomainEvent;
