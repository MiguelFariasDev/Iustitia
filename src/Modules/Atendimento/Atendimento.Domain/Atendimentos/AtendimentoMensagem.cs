using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Atendimentos.Domain.Atendimentos;

/// <summary>
/// Uma mensagem da conversa. Imutável depois de criada, exceto pelo resultado do envio
/// (<see cref="Delivery"/> e <see cref="ExternalId"/>): o histórico é prova de atendimento e
/// fica retido pelo prazo da política (5 anos), por isso não há soft delete nem edição.
/// </summary>
public sealed class AtendimentoMensagem : Entity<AtendimentoMensagemId>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public AtendimentoId AtendimentoId { get; private set; }

    public MessageDirection Direction { get; private set; }

    public MessageSender Sender { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Id da mensagem na Meta (<c>wamid</c>). É a chave de idempotência: a Meta reentrega
    /// webhooks, e a mesma mensagem não pode virar duas linhas.
    /// </summary>
    public string? ExternalId { get; private set; }

    public DeliveryStatus Delivery { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AtendimentoMensagem()
    {
        // EF Core.
    }

    internal AtendimentoMensagem(
        Guid tenantId,
        AtendimentoId atendimentoId,
        MessageDirection direction,
        MessageSender sender,
        string body,
        string? externalId,
        DeliveryStatus delivery,
        DateTimeOffset createdAt)
        : base(AtendimentoMensagemId.New())
    {
        TenantId = tenantId;
        AtendimentoId = atendimentoId;
        Direction = direction;
        Sender = sender;
        Body = body;
        ExternalId = externalId;
        Delivery = delivery;
        CreatedAt = createdAt;
    }

    public void MarkSent(string? externalId)
    {
        Delivery = DeliveryStatus.Sent;
        ExternalId = string.IsNullOrWhiteSpace(externalId) ? ExternalId : externalId.Trim();
    }

    public void MarkFailed() => Delivery = DeliveryStatus.Failed;
}
