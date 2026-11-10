using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Atendimentos.Domain.Events;

namespace Advocacia.Modules.Atendimentos.Domain.Atendimentos;

/// <summary>
/// Uma conversa de WhatsApp entre o escritório e um número de telefone. Raiz de agregado
/// e dona da máquina de estados do handoff:
///
/// <code>
///   Bot ──RequestHandoff──▶ PendingHuman ──AssignToHuman──▶ Human ──ReturnToBot──▶ Bot
///    └────────────AssignToHuman (advogado responde direto)────▶ ▲
///   qualquer estado aberto ──Close──▶ Closed (terminal)
/// </code>
///
/// Há no máximo UM atendimento aberto (não encerrado) por telefone e escritório — o índice
/// único parcial garante sob concorrência. As mensagens NÃO são uma coleção deste agregado
/// (uma conversa longa não pode ser carregada inteira a cada comando): <see cref="AddMessage"/>
/// valida as regras do estado e devolve a mensagem para o repositório persistir.
/// </summary>
public sealed class Atendimento : AggregateRoot<AtendimentoId>, IAuditable, IHasTenant
{
    /// <summary>Limite do corpo de uma mensagem de texto na WhatsApp Cloud API.</summary>
    public const int MaxBodyLength = 4096;

    public const int MaxContactNameLength = 200;

    public Guid TenantId { get; private set; }

    /// <summary>Cliente do escritório vinculado a este número. Opcional: o contato pode ser desconhecido.</summary>
    public Guid? ClientId { get; private set; }

    /// <summary>Só dígitos, com DDI (formato <c>wa_id</c> da Meta). Ver <see cref="TelefoneWhatsApp"/>.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Nome do perfil do WhatsApp informado pela Meta no webhook. É o que a busca do Inbox
    /// usa: o nome cadastrado do cliente mora em Legal, e este módulo não depende dele.
    /// </summary>
    public string? ContactName { get; private set; }

    public AtendimentoStatus Status { get; private set; }

    /// <summary>Advogado que assumiu. Preenchido somente enquanto <see cref="Status"/> é Human.</summary>
    public Guid? AssignedUserId { get; private set; }

    public DateTimeOffset LastMessageAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsOpen => Status != AtendimentoStatus.Closed;

    private Atendimento()
    {
        // EF Core.
    }

    private Atendimento(
        AtendimentoId id, Guid tenantId, string phoneNumber, Guid? clientId, string? contactName, DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        PhoneNumber = phoneNumber;
        ClientId = clientId;
        ContactName = contactName;
        Status = AtendimentoStatus.Bot;
        LastMessageAt = now;
    }

    public static Result<Atendimento> Create(
        Guid tenantId, string? phoneNumber, Guid? clientId, string? contactName, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Atendimento>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        var phone = TelefoneWhatsApp.Normalize(phoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<Atendimento>(phone.Error);
        }

        var atendimento = new Atendimento(
            AtendimentoId.New(), tenantId, phone.Value, clientId == Guid.Empty ? null : clientId,
            NormalizeContactName(contactName), now);

        atendimento.RaiseDomainEvent(new AtendimentoIniciadoEvent(atendimento.Id.Value, tenantId));

        return Result.Success(atendimento);
    }

    /// <summary>
    /// O cliente pediu um humano. Idempotente: o Bot pode repetir a chamada (retry de rede,
    /// cliente digitando "2" duas vezes) sem erro e sem novo evento.
    /// </summary>
    public Result RequestHandoff()
    {
        switch (Status)
        {
            case AtendimentoStatus.Closed:
                return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_CLOSED));
            case AtendimentoStatus.PendingHuman or AtendimentoStatus.Human:
                return Result.Success();
            default:
                Transition(AtendimentoStatus.PendingHuman, assignedUserId: null);
                return Result.Success();
        }
    }

    /// <summary>
    /// O advogado assume — vindo de PendingHuman (fila) ou direto de Bot (responde sem esperar
    /// o pedido do cliente). Idempotente para o MESMO advogado; outro advogado recebe conflito,
    /// para dois não digitarem na mesma conversa sem saber um do outro.
    /// </summary>
    public Result AssignToHuman(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(userId)));
        }

        switch (Status)
        {
            case AtendimentoStatus.Closed:
                return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_CLOSED));
            case AtendimentoStatus.Human when AssignedUserId == userId:
                return Result.Success();
            case AtendimentoStatus.Human:
                return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_ALREADY_ASSIGNED));
            default:
                Transition(AtendimentoStatus.Human, userId);
                return Result.Success();
        }
    }

    /// <summary>Devolve a conversa ao fluxo automático. Vale também para cancelar uma fila (PendingHuman).</summary>
    public Result ReturnToBot()
    {
        switch (Status)
        {
            case AtendimentoStatus.Closed:
                return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_CLOSED));
            case AtendimentoStatus.Bot:
                return Result.Failure(ErrorFactory.From(
                    ErrorCode.ATENDIMENTO_INVALID_STATUS_TRANSITION, Status, AtendimentoStatus.Bot));
            default:
                Transition(AtendimentoStatus.Bot, assignedUserId: null);
                return Result.Success();
        }
    }

    public Result Close()
    {
        if (Status == AtendimentoStatus.Closed)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_CLOSED));
        }

        Transition(AtendimentoStatus.Closed, assignedUserId: null);
        return Result.Success();
    }

    /// <summary>Atualiza o nome do perfil quando o contato o troca. Nome vazio não apaga o que já se sabe.</summary>
    public void UpdateContactName(string? contactName)
    {
        var normalized = NormalizeContactName(contactName);
        if (normalized is not null)
        {
            ContactName = normalized;
        }
    }

    public Result LinkCliente(Guid clientId)
    {
        if (clientId == Guid.Empty)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(clientId)));
        }

        if (ClientId == clientId)
        {
            return Result.Success();
        }

        ClientId = clientId;
        RaiseDomainEvent(new AtendimentoClienteVinculadoEvent(Id.Value, TenantId, clientId));
        return Result.Success();
    }

    /// <summary>
    /// Registra uma mensagem na conversa, aplicando as regras do estado:
    /// <list type="bullet">
    /// <item>entrada é sempre do cliente; saída é do bot ou de um humano;</item>
    /// <item>o bot NUNCA fala com a conversa em Human (regra inegociável do handoff);</item>
    /// <item>em PendingHuman o bot só pode enviar o aviso de espera — o fluxo automático
    /// em si é barrado pelo Bot, que consulta o status antes de responder;</item>
    /// <item>um humano só fala quando assumiu a conversa (Human);</item>
    /// <item>conversa encerrada não recebe mensagens.</item>
    /// </list>
    /// </summary>
    public Result<AtendimentoMensagem> AddMessage(
        MessageDirection direction,
        MessageSender sender,
        string? body,
        string? externalId,
        DateTimeOffset now)
    {
        if (Status == AtendimentoStatus.Closed)
        {
            return Result.Failure<AtendimentoMensagem>(ErrorFactory.From(ErrorCode.ATENDIMENTO_CLOSED));
        }

        var text = body?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return Result.Failure<AtendimentoMensagem>(ErrorFactory.From(ErrorCode.ATENDIMENTO_MESSAGE_EMPTY));
        }

        if (text.Length > MaxBodyLength)
        {
            return Result.Failure<AtendimentoMensagem>(
                ErrorFactory.From(ErrorCode.ATENDIMENTO_MESSAGE_TOO_LONG, MaxBodyLength));
        }

        var senderIsValid = direction == MessageDirection.Inbound
            ? sender == MessageSender.Client
            : sender is MessageSender.Bot or MessageSender.Human;
        if (!senderIsValid)
        {
            return Result.Failure<AtendimentoMensagem>(ErrorFactory.From(ErrorCode.ATENDIMENTO_MESSAGE_INVALID_SENDER));
        }

        if (sender == MessageSender.Bot && Status == AtendimentoStatus.Human)
        {
            return Result.Failure<AtendimentoMensagem>(ErrorFactory.From(ErrorCode.ATENDIMENTO_BOT_BLOCKED));
        }

        if (sender == MessageSender.Human && Status != AtendimentoStatus.Human)
        {
            return Result.Failure<AtendimentoMensagem>(ErrorFactory.From(ErrorCode.ATENDIMENTO_MESSAGE_INVALID_SENDER));
        }

        // Mensagem de saída de um humano nasce Pending: o envio à Meta acontece depois e pode falhar.
        var delivery = sender == MessageSender.Human ? DeliveryStatus.Pending : DeliveryStatus.Sent;
        var normalizedExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();

        var mensagem = new AtendimentoMensagem(
            TenantId, Id, direction, sender, text, normalizedExternalId, delivery, now);

        LastMessageAt = now;
        RaiseDomainEvent(new AtendimentoMensagemAdicionadaEvent(Id.Value, TenantId, mensagem.Id.Value, direction, sender));

        return Result.Success(mensagem);
    }

    private static string? NormalizeContactName(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxContactNameLength ? trimmed[..MaxContactNameLength] : trimmed;
    }

    private void Transition(AtendimentoStatus to, Guid? assignedUserId)
    {
        var from = Status;
        Status = to;
        AssignedUserId = assignedUserId;
        RaiseDomainEvent(new AtendimentoStatusAlteradoEvent(Id.Value, TenantId, from, to, assignedUserId));
    }
}
