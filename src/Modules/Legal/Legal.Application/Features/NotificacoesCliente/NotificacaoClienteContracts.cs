using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.NotificacoesCliente;

/// <param name="Status">PendenteRevisao, Enviada, Descartada ou FalhaEnvio; nulo = todos.</param>
public sealed record ListNotificacoesClienteQuery(string? Status = null, int Page = 1, int PageSize = 20)
    : IQuery<ListNotificacoesClienteResponse>;

/// <summary>
/// O advogado revisou o aviso e manda enviar. <paramref name="Mensagem"/> substitui o rascunho;
/// nula envia o rascunho como está. Auditado (quem aprovou e quando), sem o texto.
/// </summary>
[Audited("Approved", "NotificacaoCliente", EntityIdProperty = nameof(NotificacaoId))]
public sealed record EnviarNotificacaoClienteCommand(Guid NotificacaoId, string? Mensagem = null)
    : ICommand<NotificacaoClienteResponse>;

[Audited("Rejected", "NotificacaoCliente", EntityIdProperty = nameof(NotificacaoId))]
public sealed record DescartarNotificacaoClienteCommand(Guid NotificacaoId) : ICommand<NotificacaoClienteResponse>;

public sealed record NotificacaoClienteResponse(
    Guid Id,
    Guid ProcessoId,
    string? NumeroProcesso,
    Guid ClienteId,
    string Status,
    string Mensagem,
    int QuantidadeAndamentos,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevisadoEm,
    DateTimeOffset? EnviadoEm,
    DateTimeOffset? FalhaEnvioEm);

public sealed record ListNotificacoesClienteResponse(
    IReadOnlyList<NotificacaoClienteResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
