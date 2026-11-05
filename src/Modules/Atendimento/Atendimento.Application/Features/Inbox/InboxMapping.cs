using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Application.Features.Inbox;

internal static class InboxMapping
{
    private const int PreviewLength = 80;

    public static AtendimentoResponse ToResponse(this Atendimento atendimento, string? preview = null) => new(
        atendimento.Id.Value,
        atendimento.PhoneNumber,
        atendimento.ContactName,
        atendimento.ClientId,
        atendimento.Status.ToString(),
        atendimento.AssignedUserId,
        atendimento.LastMessageAt,
        atendimento.CreatedAt,
        preview is { Length: > PreviewLength } ? preview[..PreviewLength] + "…" : preview);

    public static MensagemResponse ToResponse(this AtendimentoMensagem mensagem) => new(
        mensagem.Id.Value,
        mensagem.Direction.ToString(),
        mensagem.Sender.ToString(),
        mensagem.Body,
        mensagem.Delivery.ToString(),
        mensagem.CreatedAt);
}
