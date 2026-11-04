using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Modules.Legal.Domain.Processos;

namespace Advocacia.Modules.Legal.Domain.Events;

/// <summary>
/// Um processo foi cadastrado. É o gatilho da captura: a partir daqui o job passa a
/// consultar o DJEN por este número (ver docs/modules/legal/cnj-capture.md).
/// </summary>
public sealed record ProcessoCriadoEvent(Guid ProcessoId, Guid TenantId, Guid ClienteId, string CnjNumber) : DomainEvent;

public sealed record ProcessoAtualizadoEvent(Guid ProcessoId, Guid TenantId) : DomainEvent;

public sealed record ProcessoStatusAlteradoEvent(
    Guid ProcessoId,
    Guid TenantId,
    StatusProcesso StatusAnterior,
    StatusProcesso NovoStatus) : DomainEvent;
