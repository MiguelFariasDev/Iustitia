using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Application.Features.Bot;

/// <summary>
/// O tenant vem SEMPRE da credencial do Bot (a chave de API resolve o escritório), nunca do
/// corpo da requisição — é o que impede um Bot de operar no escritório alheio.
/// </summary>
public sealed class StartAtendimentoHandler(
    IAtendimentoRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<StartAtendimentoCommand, StartAtendimentoResponse>
{
    public async Task<Result<StartAtendimentoResponse>> Handle(
        StartAtendimentoCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<StartAtendimentoResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var phone = TelefoneWhatsApp.Normalize(request.PhoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<StartAtendimentoResponse>(phone.Error);
        }

        var existing = await repository.GetOpenByPhoneAsync(phone.Value, cancellationToken);
        if (existing is not null)
        {
            if (request.ContactName is not null && request.ContactName != existing.ContactName)
            {
                existing.UpdateContactName(request.ContactName);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new StartAtendimentoResponse(existing.Id.Value, existing.Status.ToString(), Created: false));
        }

        var now = clock.UtcNow;
        var created = Atendimento.Create(tenantId, phone.Value, request.ClientId, request.ContactName, now);
        if (created.IsFailure)
        {
            return Result.Failure<StartAtendimentoResponse>(created.Error);
        }

        var atendimento = created.Value;
        repository.Add(atendimento);
        repository.AddAuditoria(AtendimentoAuditoria.Record(
            atendimento, AtendimentoAuditAction.Started, AuditActorType.Bot, actorId: null, from: null, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new StartAtendimentoResponse(atendimento.Id.Value, atendimento.Status.ToString(), Created: true));
    }
}

public sealed class AddBotMessageHandler(
    IAtendimentoRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<AddBotMessageCommand, AddMessageResponse>
{
    public async Task<Result<AddMessageResponse>> Handle(AddBotMessageCommand request, CancellationToken cancellationToken)
    {
        // Idempotência: a Meta reentrega webhooks. A mesma mensagem devolve o mesmo id.
        if (!string.IsNullOrWhiteSpace(request.ExternalId))
        {
            var duplicate = await repository.GetMensagemByExternalIdAsync(request.ExternalId.Trim(), cancellationToken);
            if (duplicate is not null)
            {
                return Result.Success(new AddMessageResponse(duplicate.Id.Value));
            }
        }

        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(request.AtendimentoId), cancellationToken);
        if (atendimento is null)
        {
            return Result.Failure<AddMessageResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND));
        }

        var direction = Enum.Parse<MessageDirection>(request.Direction, ignoreCase: true);
        var sender = Enum.Parse<MessageSender>(request.Sender, ignoreCase: true);

        var mensagem = atendimento.AddMessage(direction, sender, request.Body, request.ExternalId, clock.UtcNow);
        if (mensagem.IsFailure)
        {
            return Result.Failure<AddMessageResponse>(mensagem.Error);
        }
        repository.AddMensagem(mensagem.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AddMessageResponse(mensagem.Value.Id.Value));
    }
}

public sealed class RequestHandoffHandler(
    IAtendimentoRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<RequestHandoffCommand>
{
    public async Task<Result> Handle(RequestHandoffCommand request, CancellationToken cancellationToken)
    {
        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(request.AtendimentoId), cancellationToken);
        if (atendimento is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND));
        }

        var before = atendimento.Status;
        var result = atendimento.RequestHandoff();
        if (result.IsFailure)
        {
            return result;
        }

        // Idempotente: repetir o pedido não muda nada e não gera nova linha de auditoria.
        if (atendimento.Status != before)
        {
            repository.AddAuditoria(AtendimentoAuditoria.Record(
                atendimento, AtendimentoAuditAction.HandoffRequested, AuditActorType.Client, actorId: null, before, clock.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

public sealed class GetAtendimentoStatusHandler(IAtendimentoRepository repository)
    : IQueryHandler<GetAtendimentoStatusQuery, AtendimentoStatusResponse>
{
    public async Task<Result<AtendimentoStatusResponse>> Handle(
        GetAtendimentoStatusQuery request, CancellationToken cancellationToken)
    {
        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(request.AtendimentoId), cancellationToken);

        return atendimento is null
            ? Result.Failure<AtendimentoStatusResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND))
            : Result.Success(new AtendimentoStatusResponse(atendimento.Id.Value, atendimento.Status.ToString()));
    }
}
