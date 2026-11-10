using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Atendimentos.Application.Features.Inbox;

public sealed record ListAtendimentosQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    Guid? AssignedUserId = null,
    string? Search = null) : IQuery<ListAtendimentosResponse>;

public sealed record GetAtendimentoByIdQuery(Guid AtendimentoId) : IQuery<AtendimentoResponse>;

/// <summary>
/// Histórico da conversa em ordem cronológica. Pagina "para trás": <c>Before</c> traz as
/// mensagens anteriores a um instante, e a tela carrega mais ao rolar para cima.
/// </summary>
public sealed record ListMensagensQuery(Guid AtendimentoId, DateTimeOffset? Before = null, int Limit = 50)
    : IQuery<ListMensagensResponse>;

/// <summary>
/// O advogado fala com o cliente. Se a conversa ainda não é dele, este comando a assume
/// (Bot/PendingHuman → Human) antes de enviar — o mesmo efeito de "Assumir" + digitar.
/// </summary>
public sealed record SendHumanMessageCommand(Guid AtendimentoId, string Body) : ICommand<SendMessageResponse>;

/// <summary>Muda o status manualmente. <c>Status</c> aceita "Human" (assumir), "Bot" (devolver) e "Closed" (encerrar).</summary>
public sealed record ChangeAtendimentoStatusCommand(Guid AtendimentoId, string Status) : ICommand;

public sealed record LinkClienteCommand(Guid AtendimentoId, Guid ClienteId) : ICommand;

public sealed record AtendimentoResponse(
    Guid Id,
    string PhoneNumber,
    string? ContactName,
    Guid? ClientId,
    string Status,
    Guid? AssignedUserId,
    DateTimeOffset LastMessageAt,
    DateTimeOffset CreatedAt,
    string? LastMessagePreview);

public sealed record ListAtendimentosResponse(
    IReadOnlyList<AtendimentoResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record MensagemResponse(
    Guid Id,
    string Direction,
    string Sender,
    string Body,
    string Delivery,
    DateTimeOffset CreatedAt);

public sealed record ListMensagensResponse(IReadOnlyList<MensagemResponse> Items, bool HasMore);

public sealed record SendMessageResponse(Guid MessageId, string Delivery);
