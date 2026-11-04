using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Application.Features.Processos;

/// <summary>
/// Cadastro de processo — a ação que liga a captura. A partir daqui o job passa a consultar
/// o DJEN por este número (ver CnjCaptureJob).
/// </summary>
public sealed class CreateProcessoHandler(
    IProcessoRepository processoRepository,
    IClienteRepository clienteRepository,
    ILegalUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateProcessoCommand, ProcessoResponse>
{
    public async Task<Result<ProcessoResponse>> Handle(CreateProcessoCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ProcessoResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var cnjNumber = CNJNumber.Create(request.CnjNumber);
        if (cnjNumber.IsFailure)
        {
            return Result.Failure<ProcessoResponse>(ErrorFactory.From(ErrorCode.PROCESS_CNJ_INVALID));
        }

        if (!Enum.TryParse<AreaDireito>(request.Area, ignoreCase: true, out var area))
        {
            return Result.Failure<ProcessoResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Area)));
        }

        // Consistência ENTRE agregados é da aplicação, não do banco (não há FK para clientes
        // — ver ProcessoConfiguration). Sem esta checagem, um processo poderia apontar para
        // um cliente inexistente e a tela de detalhe quebraria só na hora de exibir.
        if (!await clienteRepository.ExistsAsync(tenantId, ClienteId.From(request.ClienteId), cancellationToken))
        {
            return Result.Failure<ProcessoResponse>(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOT_FOUND));
        }

        var duplicado = await processoRepository.GetByCnjAsync(tenantId, cnjNumber.Value.Digits, cancellationToken);
        if (duplicado is not null)
        {
            return Result.Failure<ProcessoResponse>(ErrorFactory.From(ErrorCode.PROCESS_CNJ_DUPLICATED));
        }

        // Responsável omitido assume quem está cadastrando: é o caso esmagadoramente comum
        // num escritório pequeno, e obrigar a escolher a si mesmo em todo cadastro é atrito.
        var responsavelUserId = request.ResponsavelUserId ?? currentUser.UserId;
        if (responsavelUserId is not { } responsavel || responsavel == Guid.Empty)
        {
            return Result.Failure<ProcessoResponse>(ErrorFactory.From(ErrorCode.PROCESS_RESPONSIBLE_REQUIRED));
        }

        var foro = DadosForo.Create(request.Foro?.Tribunal, request.Foro?.Comarca, request.Foro?.Vara);

        var processo = Processo.Create(
            tenantId,
            cnjNumber.Value,
            request.ClienteId,
            responsavel,
            area,
            foro.Value,
            request.Assunto,
            request.ParteContraria,
            request.ValorCausa,
            request.DataDistribuicao,
            request.Observacoes);

        if (processo.IsFailure)
        {
            return Result.Failure<ProcessoResponse>(processo.Error);
        }

        processoRepository.Add(processo.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(processo.Value.ToResponse());
    }
}

public sealed class UpdateProcessoHandler(
    IProcessoRepository processoRepository,
    IClienteRepository clienteRepository,
    ILegalUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateProcessoCommand>
{
    public async Task<Result> Handle(UpdateProcessoCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(request.ProcessoId), cancellationToken);
        if (processo is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_NOT_FOUND));
        }

        if (!Enum.TryParse<AreaDireito>(request.Area, ignoreCase: true, out var area))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Area)));
        }

        if (!await clienteRepository.ExistsAsync(tenantId, ClienteId.From(request.ClienteId), cancellationToken))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOT_FOUND));
        }

        var foro = DadosForo.Create(request.Foro?.Tribunal, request.Foro?.Comarca, request.Foro?.Vara);

        var resultado = processo.Update(
            request.ClienteId,
            request.ResponsavelUserId,
            area,
            foro.Value,
            request.Assunto,
            request.ParteContraria,
            request.ValorCausa,
            request.DataDistribuicao,
            request.Observacoes);

        if (resultado.IsFailure)
        {
            return resultado;
        }

        processoRepository.Update(processo);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Muda a situação de acompanhamento. Importa para a captura: encerrado e arquivado deixam
/// de ser consultados no diário (ver Processo.GeraCaptura).
/// </summary>
public sealed class ChangeProcessoStatusHandler(
    IProcessoRepository processoRepository,
    ILegalUnitOfWork unitOfWork) : ICommandHandler<ChangeProcessoStatusCommand>
{
    public async Task<Result> Handle(ChangeProcessoStatusCommand request, CancellationToken cancellationToken)
    {
        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(request.ProcessoId), cancellationToken);
        if (processo is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_NOT_FOUND));
        }

        if (!Enum.TryParse<StatusProcesso>(request.Status, ignoreCase: true, out var status))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Status)));
        }

        var resultado = processo.ChangeStatus(status);
        if (resultado.IsFailure)
        {
            return resultado;
        }

        processoRepository.Update(processo);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ArchiveProcessoHandler(
    IProcessoRepository processoRepository,
    ILegalUnitOfWork unitOfWork) : ICommandHandler<ArchiveProcessoCommand>
{
    public async Task<Result> Handle(ArchiveProcessoCommand request, CancellationToken cancellationToken)
    {
        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(request.ProcessoId), cancellationToken);
        if (processo is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_NOT_FOUND));
        }

        var resultado = processo.Archive();
        if (resultado.IsFailure)
        {
            return resultado;
        }

        processoRepository.Update(processo);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class GetProcessoByIdHandler(IProcessoRepository processoRepository)
    : IQueryHandler<GetProcessoByIdQuery, ProcessoResponse>
{
    public async Task<Result<ProcessoResponse>> Handle(GetProcessoByIdQuery request, CancellationToken cancellationToken)
    {
        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(request.ProcessoId), cancellationToken);

        return processo is null
            ? Result.Failure<ProcessoResponse>(ErrorFactory.From(ErrorCode.PROCESS_NOT_FOUND))
            : Result.Success(processo.ToResponse());
    }
}

public sealed class ListProcessosHandler(IProcessoRepository processoRepository, ICurrentUser currentUser)
    : IQueryHandler<ListProcessosQuery, ListProcessosResponse>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<ListProcessosResponse>> Handle(ListProcessosQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ListProcessosResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        AreaDireito? area = null;
        if (!string.IsNullOrWhiteSpace(request.Area))
        {
            if (!Enum.TryParse<AreaDireito>(request.Area, ignoreCase: true, out var parsedArea))
            {
                return Result.Failure<ListProcessosResponse>(
                    ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Area)));
            }

            area = parsedArea;
        }

        StatusProcesso? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<StatusProcesso>(request.Status, ignoreCase: true, out var parsedStatus))
            {
                return Result.Failure<ListProcessosResponse>(
                    ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Status)));
            }

            status = parsedStatus;
        }

        // A sigla é gravada em maiúsculas (DadosForo), então o filtro compara igual.
        var tribunal = string.IsNullOrWhiteSpace(request.Tribunal) ? null : request.Tribunal.Trim().ToUpperInvariant();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await processoRepository.ListAsync(
            tenantId, request.ClienteId, request.ResponsavelUserId, area, status, request.Busca, tribunal, request.DistribuicaoDe, request.DistribuicaoAte, skip, pageSize, cancellationToken);
        var totalCount = await processoRepository.CountAsync(
            tenantId, request.ClienteId, request.ResponsavelUserId, area, status, request.Busca, tribunal, request.DistribuicaoDe, request.DistribuicaoAte, cancellationToken);

        return Result.Success(new ListProcessosResponse(
            items.Select(processo => processo.ToListItem()).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }
}
