using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Atendimentos.Domain.Atendimentos;

public enum AtendimentoAuditAction
{
    Started,
    HandoffRequested,
    Assigned,
    ReturnedToBot,
    Closed,
    ClienteLinked,
    HumanMessageSent,
}

public enum AuditActorType
{
    Client,
    Bot,
    User,
}

/// <summary>
/// Trilha de auditoria IMUTÁVEL do atendimento: quem fez o quê e quando. Existe separada das
/// mensagens porque o que o advogado fez (assumir, devolver, encerrar) precisa ser provável
/// mesmo que nenhuma mensagem tenha sido trocada. Nunca é alterada nem apagada.
/// </summary>
public sealed class AtendimentoAuditoria : Entity<Guid>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public AtendimentoId AtendimentoId { get; private set; }

    public AtendimentoAuditAction Action { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    public AtendimentoStatus? FromStatus { get; private set; }

    public AtendimentoStatus? ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private AtendimentoAuditoria()
    {
        // EF Core.
    }

    private AtendimentoAuditoria(
        Guid tenantId,
        AtendimentoId atendimentoId,
        AtendimentoAuditAction action,
        AuditActorType actorType,
        Guid? actorId,
        AtendimentoStatus? from,
        AtendimentoStatus? to,
        DateTimeOffset occurredAt)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        AtendimentoId = atendimentoId;
        Action = action;
        ActorType = actorType;
        ActorId = actorId;
        FromStatus = from;
        ToStatus = to;
        OccurredAt = occurredAt;
    }

    public static AtendimentoAuditoria Record(
        Atendimento atendimento,
        AtendimentoAuditAction action,
        AuditActorType actorType,
        Guid? actorId,
        AtendimentoStatus? from,
        DateTimeOffset occurredAt) =>
        new(atendimento.TenantId, atendimento.Id, action, actorType, actorId, from, atendimento.Status, occurredAt);
}
