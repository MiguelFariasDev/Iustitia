using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Authorization;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Movimentacoes;
using Advocacia.Modules.Legal.Domain.Processos;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Legal.Application.Features.NotificacoesCliente;

/// <summary>
/// Fila de revisão dos avisos. Aviso de processo confidencial só aparece a quem pode ver o processo
/// (responsável e sociedade — <see cref="ProcessoAcesso"/>).
/// </summary>
public sealed class ListNotificacoesClienteHandler(
    INotificacaoClienteRepository notificacaoRepository,
    IProcessoRepository processoRepository,
    ICurrentUser currentUser) : IQueryHandler<ListNotificacoesClienteQuery, ListNotificacoesClienteResponse>
{
    public async Task<Result<ListNotificacoesClienteResponse>> Handle(
        ListNotificacoesClienteQuery request, CancellationToken cancellationToken)
    {
        StatusNotificacaoCliente? status = request.Status is null
            ? null
            : Enum.Parse<StatusNotificacaoCliente>(request.Status, ignoreCase: true);

        var skip = (request.Page - 1) * request.PageSize;
        var itens = await notificacaoRepository.ListAsync(status, skip, request.PageSize, cancellationToken);
        var total = await notificacaoRepository.CountAsync(status, cancellationToken);

        var visiveis = new List<NotificacaoClienteResponse>(itens.Count);
        foreach (var item in itens)
        {
            var processo = await processoRepository.GetByIdAsync(ProcessoId.From(item.ProcessoId), cancellationToken);
            if (processo is not null && ProcessoAcesso.PodeVer(processo, currentUser))
            {
                visiveis.Add(item.ToResponse(processo.CnjNumber.Value));
            }
        }

        return Result.Success(new ListNotificacoesClienteResponse(visiveis, request.Page, request.PageSize, total));
    }
}

/// <summary>
/// Envio REVISADO do aviso: o advogado aprova (com ou sem edição) e o e-mail sai para o endereço
/// ATUAL do cadastro do cliente — não o de quando o rascunho foi criado. Falha do provedor deixa o
/// aviso como FalhaEnvio, pronto para reenviar.
/// </summary>
public sealed class EnviarNotificacaoClienteHandler(
    INotificacaoClienteRepository notificacaoRepository,
    IProcessoRepository processoRepository,
    IClienteRepository clienteRepository,
    IEmailSender emailSender,
    ILegalUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<EnviarNotificacaoClienteHandler> logger) : ICommandHandler<EnviarNotificacaoClienteCommand, NotificacaoClienteResponse>
{
    public async Task<Result<NotificacaoClienteResponse>> Handle(
        EnviarNotificacaoClienteCommand request, CancellationToken cancellationToken)
    {
        var notificacao = await notificacaoRepository.GetByIdAsync(NotificacaoClienteId.From(request.NotificacaoId), cancellationToken);
        var processo = notificacao is null
            ? null
            : await processoRepository.GetByIdAsync(ProcessoId.From(notificacao.ProcessoId), cancellationToken);
        if (notificacao is null || !ProcessoAcesso.PodeVer(processo, currentUser))
        {
            return Result.Failure<NotificacaoClienteResponse>(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_NOT_FOUND));
        }

        var cliente = await clienteRepository.GetByIdAsync(ClienteId.From(notificacao.ClienteId), cancellationToken);
        if (cliente is null || !cliente.IsActive || cliente.Anonimizado || cliente.Contato.Email is not { Length: > 0 } email)
        {
            return Result.Failure<NotificacaoClienteResponse>(
                ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_RECIPIENT_UNAVAILABLE));
        }

        if (currentUser.UserId is not { } revisor)
        {
            return Result.Failure<NotificacaoClienteResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var agora = clock.UtcNow;
        var aprovado = notificacao.Aprovar(revisor, request.Mensagem, agora);
        if (aprovado.IsFailure)
        {
            return Result.Failure<NotificacaoClienteResponse>(aprovado.Error);
        }

        try
        {
            await emailSender.SendAsync(notificacao.ToEmail(email), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Sem destinatário nem texto no log: só o id do aviso e o tipo da falha.
            logger.LogWarning("Aviso ao cliente {NotificacaoId}: envio falhou ({Tipo}).", notificacao.Id.Value, exception.GetType().Name);
            notificacao.MarcarFalhaEnvio(agora);
            notificacaoRepository.Update(notificacao);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<NotificacaoClienteResponse>(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_SEND_FAILED));
        }

        var enviado = notificacao.MarcarEnviada(agora);
        if (enviado.IsFailure)
        {
            return Result.Failure<NotificacaoClienteResponse>(enviado.Error);
        }

        notificacaoRepository.Update(notificacao);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(notificacao.ToResponse(processo.CnjNumber.Value));
    }
}

public sealed class DescartarNotificacaoClienteHandler(
    INotificacaoClienteRepository notificacaoRepository,
    IProcessoRepository processoRepository,
    ILegalUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<DescartarNotificacaoClienteCommand, NotificacaoClienteResponse>
{
    public async Task<Result<NotificacaoClienteResponse>> Handle(
        DescartarNotificacaoClienteCommand request, CancellationToken cancellationToken)
    {
        var notificacao = await notificacaoRepository.GetByIdAsync(NotificacaoClienteId.From(request.NotificacaoId), cancellationToken);
        var processo = notificacao is null
            ? null
            : await processoRepository.GetByIdAsync(ProcessoId.From(notificacao.ProcessoId), cancellationToken);
        if (notificacao is null || !ProcessoAcesso.PodeVer(processo, currentUser))
        {
            return Result.Failure<NotificacaoClienteResponse>(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_NOT_FOUND));
        }

        if (currentUser.UserId is not { } revisor)
        {
            return Result.Failure<NotificacaoClienteResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var descartado = notificacao.Descartar(revisor, clock.UtcNow);
        if (descartado.IsFailure)
        {
            return Result.Failure<NotificacaoClienteResponse>(descartado.Error);
        }

        notificacaoRepository.Update(notificacao);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(notificacao.ToResponse(processo.CnjNumber.Value));
    }
}
