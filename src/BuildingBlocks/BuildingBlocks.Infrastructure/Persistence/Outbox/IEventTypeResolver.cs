namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Resolve o nome completo de um tipo (gravado em <see cref="OutboxMessage.EventType"/> pelo
/// OutboxInterceptor via <c>domainEvent.GetType().FullName</c>) de volta para o <see cref="Type"/>
/// concreto, para que o OutboxProcessor consiga desserializar o payload e publicá-lo.
/// </summary>
public interface IEventTypeResolver
{
    /// <exception cref="InvalidOperationException">Tipo não encontrado nos assemblies carregados.</exception>
    Type Resolve(string eventType);
}
