using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Outbox;

public sealed record TestDomainEvent(string Message) : DomainEvent;
