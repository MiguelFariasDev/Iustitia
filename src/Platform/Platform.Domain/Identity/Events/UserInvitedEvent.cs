using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Platform.Domain.Identity.Events;

public sealed record UserInvitedEvent(UserId UserId, Guid TenantId, string Email) : DomainEvent;
