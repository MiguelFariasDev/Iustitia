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
    IDraftingAuditService audit) : IGeracaoPecaService
{
    private const string Aviso = SystemPrompt.Aviso;

    public async Task<Result<SubmissaoResponse>> SubmeterAsync(
        GerarPecaRequest request, CancellationToken cancellationToken = default)
    {
        if (!draftingOptions.Value.Enabled)
        {
            return Result.Failure<SubmissaoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_DISABLED));
        }

        var input = GerarPecaValidation.Validate(request);
        if (input.IsFailure)
        {
            return Result.Failure<SubmissaoResponse>(input.Error);
        }

        return await RegistrarAsync(input.Value, cancellationToken);
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

        // Passa pelo MESMO funil de validação: as instruções novas também podem ser uma tentativa de injeção.
        var input = GerarPecaValidation.Validate(new GerarPecaRequest(
            original.TipoPeca,
            original.Area,
            original.Fatos,
            original.Pedidos,
            ParseGuids(original.DocumentosIdsJson),
            request.InstrucoesAdicionais ?? original.InstrucoesAdicionais));

        if (input.IsFailure)
        {
            return Result.Failure<SubmissaoResponse>(input.Error);
        }

        return await RegistrarAsync(input.Value with { PecaOrigemId = original.Id.Value }, cancellationToken);
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
                r.Id.Value, r.TipoPeca, r.Area, r.Status.ToString(), r.Versao, r.CriadoEm, r.ConcluidoEm))
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
        if (request is null || !CanAccess(request))
        {
            return Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
        }

        return Result.Success(request);
    }

    private bool CanAccess(LegalDocumentRequest request) =>
        request.UsuarioId == currentUser.UserId || CanSeeAll();

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

    private static PecaDetalheResponse ToDetalhe(LegalDocumentRequest r)
    {
        var fontes = r.Sources
            .OrderByDescending(s => s.Score)
            .Select(s => new FonteResponse(
                s.Id,
                s.DocumentoOrigemId,
                s.NomeArquivo,
                s.Pagina,
                s.Trecho.Length <= 400 ? s.Trecho : s.Trecho[..400] + "…",
                s.Score,
                s.UtilizadaNaGeracao,
                UrlOf(s.MetadadosJson)))
            .ToList();

        return new PecaDetalheResponse(
            r.Id.Value,
            r.Status.ToString(),
            r.TipoPeca,
            r.Area,
            r.Versao,
            r.RequestOrigemId?.Value,
            // Só é "a minuta" quando concluída; antes disso é texto parcial e não deve ser copiado como peça.
            r.Status == LegalDocumentStatus.Concluido ? r.TextoGerado : null,
            fontes,
            ParseStrings(r.AvisosJson),
            Aviso,
            r.TempoGeracaoMs,
            r.CriadoEm,
            r.ConcluidoEm);
    }

    private static string? UrlOf(string? metadadosJson)
    {
        if (string.IsNullOrEmpty(metadadosJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadadosJson);

            return document.RootElement.TryGetProperty("UrlOrigem", out var url) && url.ValueKind == JsonValueKind.String
                ? url.GetString()
                : null;
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
