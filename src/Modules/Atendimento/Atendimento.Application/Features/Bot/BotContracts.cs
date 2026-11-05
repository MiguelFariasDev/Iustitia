using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Atendimentos.Application.Features.Bot;

/// <summary>
/// Abre (ou recupera) a conversa deste telefone. Idempotente: o Bot chama a cada mensagem
/// recebida e sempre recebe o atendimento aberto, criando-o só se não houver nenhum.
/// O status já vem na resposta, poupando uma segunda ida ao Core antes de responder.
/// </summary>
public sealed record StartAtendimentoCommand(string PhoneNumber, Guid? ClientId = null, string? ContactName = null)
    : ICommand<StartAtendimentoResponse>;

public sealed record StartAtendimentoResponse(Guid AtendimentoId, string Status, bool Created);

/// <summary>
/// Registra uma mensagem da conversa (recebida do cliente ou enviada pelo bot). Idempotente
/// por <c>ExternalId</c>. <c>Direction</c> é "Inbound"/"Outbound" e <c>Sender</c>, "Client"/"Bot".
/// </summary>
public sealed record AddBotMessageCommand(
    Guid AtendimentoId, string Direction, string Sender, string Body, string? ExternalId)
    : ICommand<AddMessageResponse>;

public sealed record AddMessageResponse(Guid MessageId);

public sealed record RequestHandoffCommand(Guid AtendimentoId) : ICommand;

public sealed record GetAtendimentoStatusQuery(Guid AtendimentoId) : IQuery<AtendimentoStatusResponse>;

public sealed record AtendimentoStatusResponse(Guid AtendimentoId, string Status);
