using Advocacia.BuildingBlocks.Domain.Events;
using MassTransit;

namespace Advocacia.BuildingBlocks.Infrastructure.Messaging;

/// <summary>Publisher de eventos de integração via MassTransit — ver ADR-016.</summary>
public sealed class IntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
        publishEndpoint.Publish(integrationEvent, integrationEvent.GetType(), cancellationToken);
}
