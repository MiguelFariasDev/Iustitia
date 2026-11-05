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
    ConsultaIniciada,
    ConsultaVerificada,
    ConsultaNegada,
    ConsultaBloqueada,
    ConsultaProcessoVisualizado,
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

    /// <summary>
    /// A que a ação se refere, quando se refere a algo: na consulta de andamento, o processo visto.
    /// É sempre um id — nunca o documento nem qualquer dado da conversa.
    /// </summary>
    public Guid? ReferenceId { get; private set; }

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
        DateTimeOffset occurredAt,
        Guid? referenceId = null)
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
        ReferenceId = referenceId;
    }

    public static AtendimentoAuditoria Record(
        Atendimento atendimento,
        AtendimentoAuditAction action,
        AuditActorType actorType,
        Guid? actorId,
        AtendimentoStatus? from,
        DateTimeOffset occurredAt) =>
        new(atendimento.TenantId, atendimento.Id, action, actorType, actorId, from, atendimento.Status, occurredAt);

    /// <summary>
    /// Ação da consulta de andamento. Quem consulta é o CLIENTE (o Bot só repassa): o ator é o id do
    /// cliente cadastrado — <c>null</c> quando o telefone não pertence a nenhum. Nunca recebe o documento.
    /// </summary>
    public static AtendimentoAuditoria RecordConsulta(
        Atendimento atendimento,
        AtendimentoAuditAction action,
        Guid? clientId,
        DateTimeOffset occurredAt,
        Guid? referenceId = null) =>
        new(atendimento.TenantId, atendimento.Id, action, AuditActorType.Client, clientId, null, atendimento.Status, occurredAt, referenceId);
}
