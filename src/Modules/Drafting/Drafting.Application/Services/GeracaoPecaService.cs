using System.Runtime.CompilerServices;
using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// A porta de entrada do módulo para o usuário: registra o pedido (e o enfileira), informa o
/// progresso, devolve a minuta, cancela, regera e acompanha em tempo real. NADA aqui fala com
/// GPU, Ollama ou Qdrant — isso é do worker. Todo acesso é do escritório do token; a peça é do
/// usuário que a pediu, e sócios/titulares do mesmo escritório também podem vê-la.
/// </summary>
public sealed class GeracaoPecaService(
    IOptions<DraftingOptions> draftingOptions,
    IOptions<FilaOptions> filaOptions,
    ICurrentUser currentUser,
    ILegalDocumentService documents,
    ILegalDocumentRepository repository,
    IGeracaoPecaFila fila,
    IAuditoriaIaService audit,
    ICasoAcessoService acesso,
    ICasoDocumentoRepository anexos,
    ISegurancaIaService seguranca,
    IOptions<JanelaModeloOptions> janela,
    IOptions<RagOptions> rag) : IGeracaoPecaService
{
    private const string Aviso = SystemPrompt.Aviso;

    public async Task<Result<SubmissaoResponse>> SubmeterAsync(
        GerarPecaRequest request, CancellationToken cancellationToken = default)
    {
        if (!draftingOptions.Value.Enabled)
        {
            return Result.Failure<SubmissaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_DISABLED));
        }

        return await ValidarERegistrarAsync(request, null, cancellationToken);
    }

    public async Task<Result<StatusPecaResponse>> ObterStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await LoadAccessibleAsync(id, cancellationToken);

        return request.IsFailure
            ? Result.Failure<StatusPecaResponse>(request.Error)
            : Result.Success(ToStatus(request.Value));
    }

    public async Task<Result<PecaDetalheResponse>> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accessible = await LoadAccessibleAsync(id, cancellationToken);
        if (accessible.IsFailure)
        {
            return Result.Failure<PecaDetalheResponse>(accessible.Error);
        }

        var request = await repository.GetWithSourcesAsync(accessible.Value.Id, cancellationToken) ?? accessible.Value;

        return Result.Success(ToDetalhe(request));
    }

    public async Task<Result<StatusPecaResponse>> CancelarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accessible = await LoadAccessibleAsync(id, cancellationToken);
        if (accessible.IsFailure)
        {
            return Result.Failure<StatusPecaResponse>(accessible.Error);
        }

        var request = accessible.Value;
        var cancelled = await documents.CancelAsync(request, cancellationToken);
        if (cancelled.IsFailure)
        {
            return Result.Failure<StatusPecaResponse>(cancelled.Error);
        }

        // Se ainda estava só na fila, o item sai dela; se já estava sendo gerada, o worker percebe o
        // cancelamento no próximo envio de texto parcial e encerra a conexão com o modelo.
        await fila.CancelarPorReferenciaAsync(request.Id.Value, cancellationToken);
        await audit.RecordAsync(request, "Cancelada", null, cancellationToken);

        return Result.Success(ToStatus(request));
    }

    public async Task<Result<SubmissaoResponse>> RegenerarAsync(
        Guid id, RegenerarPecaRequest request, CancellationToken cancellationToken = default)
    {
        if (!draftingOptions.Value.Enabled)
        {
            return Result.Failure<SubmissaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_DISABLED));
        }

        if (!request.Confirmar)
        {
            return Result.Failure<SubmissaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_CONFIRMATION_REQUIRED));
        }

        var accessible = await LoadAccessibleAsync(id, cancellationToken);
        if (accessible.IsFailure)
        {
            return Result.Failure<SubmissaoResponse>(accessible.Error);
        }

        var original = accessible.Value;

        // Passa pelo MESMO funil de validação: as instruções novas também podem ser uma tentativa de injeção, e
        // o acesso ao caso pode ter sido revogado desde então.
        return await ValidarERegistrarAsync(
            new GerarPecaRequest(
                original.TipoPeca,
                original.Area,
                original.Fatos,
                original.Pedidos,
                ParseGuids(original.DocumentosIdsJson),
                request.InstrucoesAdicionais ?? original.InstrucoesAdicionais,
                CasoId: original.CasoId,
                DataFatos: original.DataFatos),
            original.Id.Value,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<PecaResumoResponse>>> ListarAsync(
        int pagina, int tamanho, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<PecaResumoResponse>>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var take = Math.Clamp(tamanho, 1, 50);
        var skip = Math.Max(0, pagina - 1) * take;
        var items = await repository.ListRecentAsync(CanSeeAll() ? null : userId, skip, take, cancellationToken);

        return Result.Success<IReadOnlyList<PecaResumoResponse>>(items
            .Select(r => new PecaResumoResponse(
                r.Id.Value, r.TipoPeca, r.Area, r.Status.ToString(), r.Versao, r.CriadoEm, r.ConcluidoEm, r.CasoId, r.RevisadoEm))
            .ToList());
    }

    public async Task<Result<IAsyncEnumerable<GenerationEvent>>> AbrirStreamAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var accessible = await LoadAccessibleAsync(id, cancellationToken);
        if (accessible.IsFailure)
        {
            return Result.Failure<IAsyncEnumerable<GenerationEvent>>(accessible.Error);
        }

        // Só vale enquanto está em processamento: depois, o resultado é GET /pecas/{id}.
        if (accessible.Value.IsTerminal)
        {
            return Result.Failure<IAsyncEnumerable<GenerationEvent>>(ErrorFactory.From(ErrorCode.DRAFTING_NOT_STREAMING));
        }

        return Result.Success(TailAsync(accessible.Value.Id, default));
    }

    /// <summary>
    /// Acompanha uma solicitação lendo o estado do BANCO (e não de um canal em memória): funciona com
    /// a API e o worker em processos ou réplicas diferentes. Emite um evento de status a cada mudança
    /// de etapa, os trechos novos do texto e, ao final, <c>fim</c> ou <c>erro</c>. Se a geração for
    /// reagendada, o texto recomeça: o cliente deve limpar o que já mostrou ao ver status "Pendente".
    /// </summary>
    private async IAsyncEnumerable<GenerationEvent> TailAsync(
        LegalDocumentRequestId id, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var options = filaOptions.Value;
        var interval = TimeSpan.FromMilliseconds(Math.Max(1, options.StreamIntervaloMs));
        var deadline = DateTimeOffset.UtcNow.AddMinutes(options.StreamMaxMinutos);
        var sent = 0;
        string? lastStage = null;

        while (!cancellationToken.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = await repository.GetSnapshotAsync(id, cancellationToken);
            if (snapshot is null)
            {
                yield return new ErroEvent(PecaMensagens.FalhaGenerica, id.Value);
                yield break;
            }

            var stageKey = $"{snapshot.Status}|{snapshot.Etapa}";
            if (stageKey != lastStage)
            {
                lastStage = stageKey;
                yield return new StatusEvent(
                    snapshot.Status.ToString(), snapshot.Etapa ?? PecaMensagens.Para(snapshot.Status), snapshot.Progresso);
            }

            var text = snapshot.Texto ?? string.Empty;
            if (text.Length < sent)
            {
                sent = 0; // a geração recomeçou (nova tentativa)
            }

            if (text.Length > sent)
            {
                yield return new FragmentoEvent(text[sent..]);
                sent = text.Length;
            }

            switch (snapshot.Status)
            {
                case LegalDocumentStatus.Concluido:
                    var done = await repository.GetWithSourcesAsync(id, cancellationToken);
                    if (done is not null)
                    {
                        var detalhe = ToDetalhe(done);
                        yield return new FimEvent(
                            done.Id.Value, done.Versao, detalhe.Fontes, detalhe.Avisos, Aviso, done.TempoGeracaoMs ?? 0);
                    }

                    yield break;
                case LegalDocumentStatus.Falhou:
                    yield return new ErroEvent(PecaMensagens.FalhaGenerica, id.Value);
                    yield break;
                case LegalDocumentStatus.Cancelado:
                    yield return new ErroEvent(PecaMensagens.Para(LegalDocumentStatus.Cancelado), id.Value);
                    yield break;
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    private async Task<Result<SubmissaoResponse>> ValidarERegistrarAsync(
        GerarPecaRequest request, Guid? origemId, CancellationToken ct)
    {
        var validado = GerarPecaValidation.Validate(request);
        if (validado.IsFailure)
        {
            if (validado.Error.Code == nameof(ErrorCode.DRAFTING_PROMPT_INJECTION) && currentUser.TenantId is { } tenantId)
            {
                await seguranca.RegistrarAsync(
                    tenantId, currentUser.UserId, "InjecaoNaEntrada", "POST /pecas/gerar",
                    $"{request.Fatos}\n{request.Pedidos}\n{request.InstrucoesAdicionais}", ct);
            }

            return Result.Failure<SubmissaoResponse>(validado.Error);
        }

        var input = validado.Value with { PecaOrigemId = origemId };
        var autorizado = await acesso.AutorizarAsync(input.CasoId, escrita: false, ct);
        if (autorizado.IsFailure)
        {
            return Result.Failure<SubmissaoResponse>(autorizado.Error);
        }

        if (autorizado.Value.Caso.Status == Domain.Casos.CasoStatus.Encerrado)
        {
            return Result.Failure<SubmissaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_CASE_CLOSED));
        }

        // Só documentos DESTE caso, e que esta pessoa pode ver.
        if (input.DocumentosIds.Count > 0)
        {
            var visiveis = (await anexos.ListarAsync(autorizado.Value.Caso.Id, ct))
                .Where(d => Casos.AnexoService.Visivel(d, autorizado.Value))
                .Select(d => d.Id.Value)
                .ToHashSet();
            if (!input.DocumentosIds.All(visiveis.Contains))
            {
                return Result.Failure<SubmissaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_DOCUMENT_NOT_IN_CASE));
            }
        }

        var excesso = OrcamentoContexto.ExcessoEmCaracteres(input, janela.Value, rag.Value.CharsPorToken);
        if (excesso > 0)
        {
            return Result.Failure<SubmissaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_INPUT_TOO_LARGE_FOR_MODEL, excesso));
        }

        return await RegistrarAsync(input with { ClienteId = autorizado.Value.Caso.ClienteId }, ct);
    }

    private async Task<Result<SubmissaoResponse>> RegistrarAsync(PecaInput input, CancellationToken ct)
    {
        var created = await documents.CreateAndEnqueueAsync(input, ct);
        if (created.IsFailure)
        {
            return Result.Failure<SubmissaoResponse>(created.Error);
        }

        var request = created.Value;
        await audit.RecordAsync(
            request, "Recebida", new { documentos = input.DocumentosIds.Count, versao = request.Versao }, ct);

        return Result.Success(new SubmissaoResponse(
            request.Id.Value,
            LegalDocumentStatus.Pendente.ToString(),
            "Sua solicitação foi registrada e está na fila de processamento.",
            request.CriadoEm,
            $"/api/v1/pecas/{request.Id.Value}/status",
            $"/api/v1/pecas/{request.Id.Value}/stream"));
    }

    /// <summary>Não é do usuário nem ele é sócio/titular do escritório: "não encontrada", nunca "proibida".</summary>
    private async Task<Result<LegalDocumentRequest>> LoadAccessibleAsync(Guid id, CancellationToken ct)
    {
        var request = await repository.GetByIdAsync(LegalDocumentRequestId.From(id), ct);
        if (request is null || !await CanAccessAsync(request, ct))
        {
            return Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
        }

        return Result.Success(request);
    }

    /// <summary>
    /// Peça de um caso segue o acesso ao CASO (inclusive para quem a pediu: acesso revogado vale também para as
    /// minutas). Solicitação antiga, sem caso, fica com quem pediu e com a gestão.
    /// </summary>
    private async Task<bool> CanAccessAsync(LegalDocumentRequest request, CancellationToken ct) =>
        request.CasoId is { } casoId
            ? (await acesso.AutorizarAsync(casoId, escrita: false, ct)).IsSuccess
            : request.UsuarioId == currentUser.UserId || CanSeeAll();

    private bool CanSeeAll() => currentUser.IsInRole("Owner") || currentUser.IsInRole("Partner");

    private static StatusPecaResponse ToStatus(LegalDocumentRequest r) =>
        new(
            r.Id.Value,
            r.Status.ToString(),
            r.EtapaAtual,
            r.Progresso,
            PecaMensagens.Para(r.Status),
            r.CriadoEm,
            r.ConcluidoEm,
            // Nunca o motivo técnico: ele fica em Erro (banco) e no log.
            r.Status == LegalDocumentStatus.Falhou ? PecaMensagens.FalhaGenerica : null,
            LinkDownload: null); // exportação DOCX/PDF não implementada

    internal static PecaDetalheResponse ToDetalhe(LegalDocumentRequest r)
    {
        var fontes = r.Sources.OrderByDescending(s => s.Score).Select(ToFonte).ToList();
        var citacoes = r.Citacoes.OrderBy(c => c.Ordem).Select(ToCitacao).ToList();
        var concluida = r.Status == LegalDocumentStatus.Concluido;

        return new PecaDetalheResponse(
            r.Id.Value,
            r.Status.ToString(),
            r.TipoPeca,
            r.Area,
            r.Versao,
            r.RequestOrigemId?.Value,
            // Só é "a minuta" quando concluída; antes disso é texto parcial e não deve ser copiado como peça.
            concluida ? r.TextoGerado : null,
            fontes,
            ParseStrings(r.AvisosJson),
            Aviso,
            r.TempoGeracaoMs,
            r.CriadoEm,
            r.ConcluidoEm,
            ExportacaoBloqueada: concluida && r.RevisadoEm is null,
            r.CasoId,
            citacoes,
            r.RevisadoEm,
            r.TextoRevisado);
    }

    internal static CitacaoResponse ToCitacao(CitacaoPeca c) =>
        new(
            c.Id, c.Texto, c.Status.ToString(), c.Detalhe, c.Bloqueante, c.FonteUrl, c.Urn,
            c.Decisao.ToString().ToLowerInvariant(), c.Justificativa, c.DecididoEm);

    private static FonteResponse ToFonte(RetrievedSource s)
    {
        var meta = ParseMetadados(s.MetadadosJson);

        return new FonteResponse(
            s.Id,
            s.DocumentoOrigemId,
            s.NomeArquivo,
            s.Pagina,
            s.Trecho.Length <= 400 ? s.Trecho : s.Trecho[..400] + "…",
            s.Score,
            s.UtilizadaNaGeracao,
            meta?.UrlOrigem,
            meta?.Rotulo,
            meta?.Grupo,
            meta?.Urn,
            meta?.Situacao,
            meta?.VersaoTexto,
            meta?.Arquivo);
    }

    private static FonteMetadados? ParseMetadados(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FonteMetadados>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> ParseStrings(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<Guid> ParseGuids(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
