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

namespace Advocacia.Modules.Legal.Application.Features.Movimentacoes;

/// <summary>
/// Sincroniza os andamentos de um processo (ADR-075):
/// <list type="number">
/// <item>janela: do último andamento capturado menos <c>JanelaDias</c>; na primeira vez, o histórico inteiro;</item>
/// <item>grava só os ids ainda não vistos (índice único por processo);</item>
/// <item>primeira sincronização (ou troca explícita de fonte) é BASE: grava sem alerta e sem aviso
/// — senão um processo recém-cadastrado dispararia dezenas de "novidades" antigas;</item>
/// <item>com novidade: alerta in-app ao responsável (só número e quantidade) e rascunho de aviso
/// ao cliente, se ele tiver e-mail, estiver ativo e não anonimizado. O rascunho NUNCA é enviado
/// aqui: o advogado revisa (<see cref="NotificacoesCliente.EnviarNotificacaoClienteHandler"/>).</item>
/// </list>
/// </summary>
public sealed class SincronizarMovimentacoesHandler(
    IProcessoRepository processoRepository,
    IClienteRepository clienteRepository,
    IProcessoMovimentacaoRepository movimentacaoRepository,
    INotificacaoClienteRepository notificacaoRepository,
    IAcompanhamentoProcessual acompanhamento,
    IAlertPublisher alertPublisher,
    ILegalUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<SincronizarMovimentacoesCommand, SincronizarMovimentacoesResponse>
{
    public const string TipoAlerta = "Movimentacao";

    public async Task<Result<SincronizarMovimentacoesResponse>> Handle(
        SincronizarMovimentacoesCommand request, CancellationToken cancellationToken)
    {
        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(request.ProcessoId), cancellationToken);
        if (processo is null || (!request.OrigemJob && !ProcessoAcesso.PodeVer(processo, currentUser)))
        {
            return Result.Failure<SincronizarMovimentacoesResponse>(ErrorFactory.From(ErrorCode.PROCESS_NOT_FOUND));
        }

        var processoId = processo.Id.Value;
        var existente = await movimentacaoRepository.UltimaCapturaAsync(processoId, cancellationToken);
        var trocandoDeFonte = request.TrocarParaFonte is { } nova && existente is not null && nova != existente.Fonte;
        var ehBase = existente is null || trocandoDeFonte;
        var fonte = request.TrocarParaFonte ?? existente?.Fonte;

        // A janela sobreposta é barata (a deduplicação absorve a repetição) e cobre andamento que o
        // cartório lança com data retroativa.
        DateOnly? desde = ehBase || existente!.UltimoAndamento is not { } ultimo
            ? null
            : DateOnly.FromDateTime(ultimo.ToOffset(TimeSpan.FromHours(-3)).Date).AddDays(-request.JanelaDias);

        var obtido = await acompanhamento.ObterAsync(processo.CnjNumber, fonte, desde, cancellationToken);
        if (obtido.IsFailure)
        {
            return Result.Failure<SincronizarMovimentacoesResponse>(obtido.Error);
        }

        var captura = obtido.Value;
        if (!captura.Encontrado)
        {
            return Result.Success(new SincronizarMovimentacoesResponse(false, captura.Fonte, ehBase, 0, false, false));
        }

        var candidatos = captura.Andamentos
            .Where(andamento => !string.IsNullOrWhiteSpace(andamento.ExternalId))
            .DistinctBy(andamento => andamento.ExternalId)
            .ToList();
        var jaGravados = await movimentacaoRepository.ExternalIdsExistentesAsync(
            processoId, candidatos.Select(andamento => andamento.ExternalId!).ToList(), cancellationToken);

        var agora = clock.UtcNow;
        var novos = new List<ProcessoMovimentacao>();
        foreach (var andamento in candidatos.Where(andamento => !jaGravados.Contains(andamento.ExternalId!)))
        {
            var criado = ProcessoMovimentacao.Create(
                processo.TenantId, processoId, andamento.ExternalId, captura.Fonte, andamento.Nome, andamento.Complemento,
                andamento.DataHora, andamento.Instancia, andamento.PossuiDocumento, agora);
            if (criado.IsSuccess)
            {
                movimentacaoRepository.Add(criado.Value);
                novos.Add(criado.Value);
            }
        }

        var notificar = !ehBase && novos.Count > 0;
        var avisoAtualizado = notificar && await PrepararAvisoAoClienteAsync(processo, novos, agora, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var alertou = false;
        if (notificar)
        {
            var maisRecente = novos.OrderByDescending(m => m.DataHora ?? DateTimeOffset.MinValue).First();

            // Só número e quantidade: o texto do andamento é conteúdo do processo e o alerta trafega
            // pelo Realtime (claude.md, seção 14).
            alertou = await alertPublisher.PublishAsync(
                new AlertRequest(
                    processo.TenantId,
                    processo.ResponsavelUserId,
                    TipoAlerta,
                    novos.Count == 1 ? "Nova movimentação" : $"{novos.Count} movimentações novas",
                    $"Processo {processo.CnjNumber.Value} — {novos.Count} andamento(s) novo(s)",
                    maisRecente.Id.Value),
                cancellationToken);
        }

        return Result.Success(new SincronizarMovimentacoesResponse(
            true, captura.Fonte, ehBase, novos.Count, alertou, avisoAtualizado));
    }

    /// <summary>
    /// Cria o rascunho ou soma os andamentos ao que já está pendente. Cliente sem e-mail, inativo ou
    /// anonimizado não recebe aviso — o alerta ao advogado basta.
    /// </summary>
    private async Task<bool> PrepararAvisoAoClienteAsync(
        Processo processo, IReadOnlyList<ProcessoMovimentacao> novos, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var cliente = await clienteRepository.GetByIdAsync(ClienteId.From(processo.ClienteId), cancellationToken);
        if (cliente is null || !cliente.IsActive || cliente.Anonimizado || string.IsNullOrWhiteSpace(cliente.Contato.Email))
        {
            return false;
        }

        var processoId = processo.Id.Value;
        var pendente = await notificacaoRepository.GetPendenteDoProcessoAsync(processoId, cancellationToken);
        if (pendente is null)
        {
            notificacaoRepository.Add(NotificacaoCliente.CriarRascunho(
                processo.TenantId, processoId, processo.ClienteId, processo.CnjNumber.Value,
                novos.Select(m => m.ToAviso()).ToList(), agora));

            return true;
        }

        var anteriores = await movimentacaoRepository.ListCapturadasDesdeAsync(processoId, pendente.AndamentosDesde, cancellationToken);
        var todos = anteriores.Concat(novos).DistinctBy(m => m.Id).Select(m => m.ToAviso()).ToList();
        if (pendente.Acumular(processo.CnjNumber.Value, todos).IsFailure)
        {
            return false;
        }

        notificacaoRepository.Update(pendente);

        return true;
    }
}

/// <summary>Timeline do processo, do andamento mais recente para o mais antigo.</summary>
public sealed class ListMovimentacoesHandler(
    IProcessoRepository processoRepository,
    IProcessoMovimentacaoRepository movimentacaoRepository,
    ICurrentUser currentUser) : IQueryHandler<ListMovimentacoesQuery, ListMovimentacoesResponse>
{
    public async Task<Result<ListMovimentacoesResponse>> Handle(ListMovimentacoesQuery request, CancellationToken cancellationToken)
    {
        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(request.ProcessoId), cancellationToken);
        if (processo is null || !ProcessoAcesso.PodeVer(processo, currentUser))
        {
            return Result.Failure<ListMovimentacoesResponse>(ErrorFactory.From(ErrorCode.PROCESS_NOT_FOUND));
        }

        var skip = (request.Page - 1) * request.PageSize;
        var itens = await movimentacaoRepository.ListAsync(request.ProcessoId, skip, request.PageSize, cancellationToken);
        var total = await movimentacaoRepository.CountAsync(request.ProcessoId, cancellationToken);

        return Result.Success(new ListMovimentacoesResponse(
            itens.Select(item => item.ToResponse()).ToList(), request.Page, request.PageSize, total));
    }
}
