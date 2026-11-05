using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Consulta;

namespace Advocacia.Modules.Atendimentos.Application.Features.Consulta;

/// <summary>
/// Pré-condição comum às três chamadas: o atendimento existe (neste escritório) e está com o BOT.
/// Com um humano na conversa — ou encerrada — o bot não oferece consulta nenhuma: quem decide o que
/// o cliente vê é o advogado.
/// </summary>
internal static class ConsultaPreconditions
{
    public static async Task<Result<Atendimento>> LoadWithBotAsync(
        IAtendimentoRepository repository, Guid atendimentoId, CancellationToken cancellationToken)
    {
        var atendimento = await repository.GetByIdAsync(AtendimentoId.From(atendimentoId), cancellationToken);

        if (atendimento is null)
        {
            return Result.Failure<Atendimento>(ErrorFactory.From(ErrorCode.ATENDIMENTO_NOT_FOUND));
        }

        return atendimento.Status == AtendimentoStatus.Bot
            ? Result.Success(atendimento)
            : Result.Failure<Atendimento>(ErrorFactory.From(ErrorCode.ATENDIMENTO_CONSULTA_REQUER_BOT));
    }

    public static string Name(ConsultaEtapa etapa) => etapa.ToString();
}

public sealed class IniciarConsultaHandler(
    IAtendimentoRepository atendimentos,
    IConsultaSessaoRepository sessoes,
    IClientIdentityDirectory identity,
    IAtendimentoUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<IniciarConsultaCommand, ConsultaEtapaResponse>
{
    public async Task<Result<ConsultaEtapaResponse>> Handle(IniciarConsultaCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ConsultaEtapaResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var loaded = await ConsultaPreconditions.LoadWithBotAsync(atendimentos, request.AtendimentoId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<ConsultaEtapaResponse>(loaded.Error);
        }

        var atendimento = loaded.Value;
        var now = clock.UtcNow;

        var clientId = await identity.FindActiveClientByPhoneAsync(tenantId, atendimento.PhoneNumber, cancellationToken);
        if (clientId is null)
        {
            // Telefone sem cadastro (ou compartilhado entre clientes): a MESMA resposta de um documento
            // errado. Quem pergunta não descobre se o número é de um cliente do escritório.
            return await FinishAsync(atendimento, AtendimentoAuditAction.ConsultaNegada, null, ConsultaEtapa.Negado, now, cancellationToken);
        }

        if (await sessoes.GetBlockedUntilAsync(tenantId, clientId.Value, now, cancellationToken) is not null)
        {
            return await FinishAsync(atendimento, AtendimentoAuditAction.ConsultaBloqueada, clientId, ConsultaEtapa.Bloqueado, now, cancellationToken);
        }

        // Recomeçar encerra a sessão anterior (só vale a mais nova), mas as falhas dela continuam valendo.
        var previous = await sessoes.GetLatestByAtendimentoAsync(atendimento.Id, cancellationToken);
        previous?.Invalidate(now);

        var inheritedFailures = await sessoes.GetRecentFailuresAsync(
            tenantId, clientId.Value, now - ConsultaSessao.FailureWindow, cancellationToken);

        sessoes.Add(ConsultaSessao.Start(tenantId, atendimento.Id, clientId.Value, inheritedFailures, now));

        return await FinishAsync(
            atendimento, AtendimentoAuditAction.ConsultaIniciada, clientId, ConsultaEtapa.AguardandoDocumento, now, cancellationToken);
    }

    private async Task<Result<ConsultaEtapaResponse>> FinishAsync(
        Atendimento atendimento, AtendimentoAuditAction action, Guid? clientId, ConsultaEtapa etapa, DateTimeOffset now, CancellationToken cancellationToken)
    {
        atendimentos.AddAuditoria(AtendimentoAuditoria.RecordConsulta(atendimento, action, clientId, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConsultaEtapaResponse(ConsultaPreconditions.Name(etapa)));
    }
}

public sealed class VerificarConsultaHandler(
    IAtendimentoRepository atendimentos,
    IConsultaSessaoRepository sessoes,
    IClientIdentityDirectory identity,
    IClientProcessReader processes,
    IAtendimentoUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<VerificarConsultaCommand, VerificarConsultaResponse>
{
    public async Task<Result<VerificarConsultaResponse>> Handle(VerificarConsultaCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<VerificarConsultaResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var loaded = await ConsultaPreconditions.LoadWithBotAsync(atendimentos, request.AtendimentoId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<VerificarConsultaResponse>(loaded.Error);
        }

        var atendimento = loaded.Value;
        var now = clock.UtcNow;

        var sessao = await sessoes.GetLatestByAtendimentoAsync(atendimento.Id, cancellationToken);
        if (sessao is null)
        {
            return Respond(ConsultaEtapa.Expirada);
        }

        var etapa = sessao.Etapa(now);
        switch (etapa)
        {
            case ConsultaEtapa.Expirada:
                return Respond(ConsultaEtapa.Expirada);

            case ConsultaEtapa.Bloqueado:
                atendimentos.AddAuditoria(AtendimentoAuditoria.RecordConsulta(
                    atendimento, AtendimentoAuditAction.ConsultaBloqueada, sessao.ClientId, now));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Respond(ConsultaEtapa.Bloqueado);

            case ConsultaEtapa.Verificado:
                // Já verificada: não precisa (nem deve) conferir um documento de novo.
                return Respond(ConsultaEtapa.Verificado, await ListFixedAsync(tenantId, sessao, cancellationToken));
        }

        // O documento existe só nesta variável local: é normalizado, comparado e descartado.
        var digits = DocumentoConsulta.Normalize(request.Documento);
        var matches = DocumentoConsulta.HasPlausibleLength(digits)
            && await identity.DocumentMatchesAsync(tenantId, sessao.ClientId, digits, cancellationToken);

        var result = sessao.RegisterAttempt(matches, now);

        IReadOnlyList<ConsultaProcessoItem>? items = null;
        if (result == ConsultaEtapa.Verificado)
        {
            var found = await processes.ListAsync(tenantId, sessao.ClientId, ConsultaSessao.MaxProcesses, cancellationToken);
            sessao.AttachProcesses(found.Select(process => process.ProcessId));
            items = found.Select((process, index) => new ConsultaProcessoItem(index + 1, process.Title)).ToList();
        }

        var action = result switch
        {
            ConsultaEtapa.Verificado => AtendimentoAuditAction.ConsultaVerificada,
            ConsultaEtapa.Bloqueado => AtendimentoAuditAction.ConsultaBloqueada,
            _ => AtendimentoAuditAction.ConsultaNegada,
        };

        atendimentos.AddAuditoria(AtendimentoAuditoria.RecordConsulta(atendimento, action, sessao.ClientId, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Respond(result, items);
    }

    private async Task<IReadOnlyList<ConsultaProcessoItem>> ListFixedAsync(
        Guid tenantId, ConsultaSessao sessao, CancellationToken cancellationToken)
    {
        var items = new List<ConsultaProcessoItem>();

        for (var index = 0; index < sessao.ProcessIds.Count; index++)
        {
            // Pelo id fixado na verificação, preservando o índice que o cliente já viu. Se o processo
            // foi arquivado no meio tempo, some da lista e o número dele não é reaproveitado.
            var process = await processes.GetAsync(tenantId, sessao.ClientId, sessao.ProcessIds[index], cancellationToken);
            if (process is not null)
            {
                items.Add(new ConsultaProcessoItem(index + 1, process.Title));
            }
        }

        return items;
    }

    private static Result<VerificarConsultaResponse> Respond(ConsultaEtapa etapa, IReadOnlyList<ConsultaProcessoItem>? items = null) =>
        Result.Success(new VerificarConsultaResponse(ConsultaPreconditions.Name(etapa), items));
}

public sealed class GetConsultaProcessoHandler(
    IAtendimentoRepository atendimentos,
    IConsultaSessaoRepository sessoes,
    IClientProcessReader processes,
    IAtendimentoUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<GetConsultaProcessoQuery, ConsultaProcessoResponse>
{
    public async Task<Result<ConsultaProcessoResponse>> Handle(GetConsultaProcessoQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ConsultaProcessoResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var loaded = await ConsultaPreconditions.LoadWithBotAsync(atendimentos, request.AtendimentoId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<ConsultaProcessoResponse>(loaded.Error);
        }

        var atendimento = loaded.Value;
        var now = clock.UtcNow;

        var sessao = await sessoes.GetLatestByAtendimentoAsync(atendimento.Id, cancellationToken);
        if (sessao is null || sessao.Etapa(now) != ConsultaEtapa.Verificado)
        {
            return Result.Failure<ConsultaProcessoResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_CONSULTA_NAO_VERIFICADA));
        }

        var notFound = Result.Failure<ConsultaProcessoResponse>(ErrorFactory.From(ErrorCode.ATENDIMENTO_CONSULTA_PROCESSO_NOT_FOUND));
        if (request.Indice < 1 || request.Indice > sessao.ProcessIds.Count)
        {
            return notFound;
        }

        // O id vem do que a VERIFICAÇÃO fixou, nunca do pedido; e a leitura ainda exige que o processo
        // seja deste cliente neste escritório — duas travas independentes contra ver processo alheio.
        var process = await processes.GetAsync(tenantId, sessao.ClientId, sessao.ProcessIds[request.Indice - 1], cancellationToken);
        if (process is null)
        {
            return notFound;
        }

        atendimentos.AddAuditoria(AtendimentoAuditoria.RecordConsulta(
            atendimento, AtendimentoAuditAction.ConsultaProcessoVisualizado, sessao.ClientId, now, process.ProcessId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConsultaProcessoResponse(process.Title, process.Phase, process.LastMovement, process.UpdatedAt));
    }
}
