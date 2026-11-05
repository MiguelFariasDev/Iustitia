using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Application.Features.Inbox;

public sealed class SendHumanMessageHandler(
    IAtendimentoRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    IBotGateway botGateway,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<SendHumanMessageCommand, SendMessageResponse>
{
    public async Task<Result<SendMessageResponse>> Handle(SendHumanMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUser is not { UserId: { } userId, TenantId: { } tenantId })
        {
            return Result.Failure<SendMessageResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(request.AtendimentoId), cancellationToken);
        if (atendimento is null)
        {
            return Result.Failure<SendMessageResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND));
        }

        var now = clock.UtcNow;

        // Falar com o cliente é assumir a conversa: o bot passa a ficar em silêncio ANTES de
        // a mensagem sair, nunca depois.
        if (atendimento.Status != AtendimentoStatus.Human)
        {
            var before = atendimento.Status;
            var assigned = atendimento.AssignToHuman(userId);
            if (assigned.IsFailure)
            {
                return Result.Failure<SendMessageResponse>(assigned.Error);
            }

            repository.AddAuditoria(AtendimentoAuditoria.Record(
                atendimento, AtendimentoAuditAction.Assigned, AuditActorType.User, userId, before, now));
        }
        else if (atendimento.AssignedUserId != userId)
        {
            return Result.Failure<SendMessageResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_ALREADY_ASSIGNED));
        }

        var mensagem = atendimento.AddMessage(MessageDirection.Outbound, MessageSender.Human, request.Body, null, now);
        if (mensagem.IsFailure)
        {
            return Result.Failure<SendMessageResponse>(mensagem.Error);
        }
        repository.AddMensagem(mensagem.Value);
        repository.AddAuditoria(AtendimentoAuditoria.Record(
            atendimento, AtendimentoAuditAction.HumanMessageSent, AuditActorType.User, userId, atendimento.Status, now));

        // Persiste ANTES de chamar o Bot: a mensagem do advogado nunca se perde se o envio
        // falhar, e não há transação de banco aberta durante uma chamada HTTP.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var delivery = await botGateway.SendTextAsync(
            tenantId, atendimento.Id.Value, mensagem.Value.Id.Value, atendimento.PhoneNumber, mensagem.Value.Body, cancellationToken);

        if (delivery.IsFailure)
        {
            mensagem.Value.MarkFailed();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<SendMessageResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_DELIVERY_FAILED));
        }

        mensagem.Value.MarkSent(delivery.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new SendMessageResponse(mensagem.Value.Id.Value, mensagem.Value.Delivery.ToString()));
    }
}

public sealed class ChangeAtendimentoStatusHandler(
    IAtendimentoRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<ChangeAtendimentoStatusCommand>
{
    public async Task<Result> Handle(ChangeAtendimentoStatusCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(request.AtendimentoId), cancellationToken);
        if (atendimento is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND));
        }

        var target = Enum.Parse<AtendimentoStatus>(request.Status, ignoreCase: true);
        var before = atendimento.Status;

        var (result, action) = target switch
        {
            AtendimentoStatus.Human => (atendimento.AssignToHuman(userId), AtendimentoAuditAction.Assigned),
            AtendimentoStatus.Bot => (atendimento.ReturnToBot(), AtendimentoAuditAction.ReturnedToBot),
            _ => (atendimento.Close(), AtendimentoAuditAction.Closed),
        };

        if (result.IsFailure)
        {
            return result;
        }

        // Idempotente (ex.: assumir de novo o que já é seu): sem mudança, sem auditoria.
        if (atendimento.Status != before)
        {
            repository.AddAuditoria(AtendimentoAuditoria.Record(
                atendimento, action, AuditActorType.User, userId, before, clock.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

public sealed class LinkClienteHandler(
    IAtendimentoRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<LinkClienteCommand>
{
    public async Task<Result> Handle(LinkClienteCommand request, CancellationToken cancellationToken)
    {
        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(request.AtendimentoId), cancellationToken);
        if (atendimento is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND));
        }

        var before = atendimento.ClientId;
        var result = atendimento.LinkCliente(request.ClienteId);
        if (result.IsFailure)
        {
            return result;
        }

        if (before != atendimento.ClientId)
        {
            repository.AddAuditoria(AtendimentoAuditoria.Record(
                atendimento, AtendimentoAuditAction.ClienteLinked, AuditActorType.User, currentUser.UserId,
                atendimento.Status, clock.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
