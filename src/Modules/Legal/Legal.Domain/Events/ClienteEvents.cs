using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;

namespace Advocacia.Modules.Legal.Domain.Events;

/// <summary>
/// Um cliente foi cadastrado. Como todo evento que atravessa a outbox e a mensageria, NÃO
/// carrega dado pessoal: nem documento, nem e-mail, nem telefone (ver claude.md, seção 14).
/// Quem consumir e precisar desses campos consulta o agregado.
/// </summary>
public sealed record ClienteCriadoEvent(Guid ClienteId, Guid TenantId, TipoCliente Tipo) : DomainEvent;

public sealed record ClienteAtualizadoEvent(Guid ClienteId, Guid TenantId) : DomainEvent;

public sealed record ClienteDesativadoEvent(Guid ClienteId, Guid TenantId) : DomainEvent;

public sealed record ClienteReativadoEvent(Guid ClienteId, Guid TenantId) : DomainEvent;
