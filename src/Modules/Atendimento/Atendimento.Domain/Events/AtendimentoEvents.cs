using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Domain.Events;

// Como todo evento que atravessa a outbox e a mensageria, estes NÃO carregam dado pessoal:
// nem telefone, nem corpo da mensagem (ver claude.md, seção 14). Quem consome — o push
// Realtime — recebe só ids e o front busca o conteúdo pela API autenticada.

public sealed record AtendimentoIniciadoEvent(Guid AtendimentoId, Guid TenantId) : DomainEvent;

public sealed record AtendimentoStatusAlteradoEvent(
    Guid AtendimentoId,
    Guid TenantId,
    AtendimentoStatus From,
    AtendimentoStatus To,
    Guid? AssignedUserId) : DomainEvent;

public sealed record AtendimentoMensagemAdicionadaEvent(
    Guid AtendimentoId,
    Guid TenantId,
    Guid MensagemId,
    MessageDirection Direction,
    MessageSender Sender) : DomainEvent;

public sealed record AtendimentoClienteVinculadoEvent(Guid AtendimentoId, Guid TenantId, Guid ClienteId) : DomainEvent;
