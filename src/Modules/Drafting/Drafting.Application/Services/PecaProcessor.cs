using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Processa UMA solicitação da fila, de ponta a ponta: contexto (RAG) → prompt → geração em
/// streaming → conferência das citações → persistência. Roda no worker, com o tenant da
/// solicitação definido e a GPU já pronta.
///
/// Durante a geração o texto PARCIAL é gravado a cada <see cref="FilaOptions.ParcialIntervaloMs"/>:
/// é o que o endpoint de stream lê (funciona mesmo com a API e o worker em processos diferentes) e é
/// também o momento de checar se o usuário cancelou — cancelar fecha a conexão com o Ollama e a GPU
/// para de gastar na hora.
///
/// Falha transitória (rede, Ollama, Qdrant, resposta vazia) com tentativas sobrando volta para a
/// fila; qualquer outra falha, ou tentativas esgotadas, marca Falhou. O usuário só recebe mensagem
/// genérica; o motivo técnico vai para o banco e o log.
/// </summary>
public sealed class PecaProcessor(
    ILegalDocumentRepository repository,
    ILegalDocumentService documents,
    IRagService rag,
    IPromptBuilder promptBuilder,
    ILlmService llm,
    IDraftingAuditService audit,
    INotificacaoService notificacao,
    IOptions<FilaOptions> filaOptions,
    IDateTimeProvider clock,
    ILogger<PecaProcessor> logger) : IQueueWorkHandler
{
    /// <summary>Tamanho típico de uma minuta, só para a barra de progresso (40–90%).</summary>
    private const int TokensEsperados = 2500;

    public QueueItemKind Tipo => QueueItemKind.Geracao;

    public async Task AoReservarAsync(Guid referenciaId, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(referenciaId, cancellationToken);
        if (request is null || request.IsTerminal)
        {
            return;
        }

        await documents.ClaimAsync(request, cancellationToken);
        await audit.RecordAsync(request, "Reservada", new { tentativa = request.Tentativas }, cancellationToken);
    }

    public async Task AoPrepararGpuAsync(Guid referenciaId, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(referenciaId, cancellationToken);
        if (request is null || request.IsTerminal)
        {
            return;
        }

        await documents.SetStageAsync(
            request, LegalDocumentStatus.PreparandoGpu, "Preparando o ambiente de IA", 15, cancellationToken);
    }

    public async Task AoFaltarInfraAsync(Guid referenciaId, string motivo, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(referenciaId, cancellationToken);
        if (request is null || request.IsTerminal)
        {
            return;
        }

        await documents.WaitForInfrastructureAsync(request, "Aguardando o ambiente de IA ficar disponível", cancellationToken);
        await audit.RecordAsync(request, "InfraIndisponivel", new { motivo }, cancellationToken);
    }

    public async Task AoExpirarAsync(Guid referenciaId, CancellationToken cancellationToken = default) =>
        await FailTerminalAsync(referenciaId, "Tempo máximo de espera na fila esgotado.", "Expirada", cancellationToken);

    public async Task AoFalharDefinitivoAsync(Guid referenciaId, string erro, CancellationToken cancellationToken = default) =>
        await FailTerminalAsync(referenciaId, erro, "Falhou", cancellationToken);

    public async Task<WorkOutcome> ProcessarAsync(Guid referenciaId, int tentativa, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(referenciaId, cancellationToken);
        if (request is null)
        {
            return new WorkOutcome(WorkOutcomeKind.Falhou, "Solicitação não encontrada.");
        }

        if (request.Status == LegalDocumentStatus.Cancelado)
        {
            return new WorkOutcome(WorkOutcomeKind.Cancelado);
        }

        if (request.IsTerminal)
        {
            // Já concluída ou falha definitiva (ex.: reprocesso de reserva vencida): nada a fazer.
            return new WorkOutcome(WorkOutcomeKind.Concluido);
        }

        var state = new RunState();

        try
        {
            return await RunAsync(request, state, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // O processo está sendo desligado: devolve à fila sem gastar tentativa.
            await Safe(() => documents.WaitForInfrastructureAsync(request, "Reagendada: o serviço foi reiniciado", CancellationToken.None));

            return new WorkOutcome(WorkOutcomeKind.Interrompido, "Processo interrompido.");
        }
        catch (Exception exception)
        {
            return await HandleFailureAsync(request, state, exception, tentativa);
        }
    }

    private async Task<WorkOutcome> RunAsync(LegalDocumentRequest request, RunState state, CancellationToken ct)
    {
        await documents.SetStageAsync(
            request, LegalDocumentStatus.BuscandoContexto, "Buscando fundamentos no acervo", 35, ct);

        var context = await rag.RetrieveAsync(
            new RagQuery(
                request.TenantId, request.TipoPeca, request.Area, request.Fatos, request.Pedidos,
                ParseDocumentos(request.DocumentosIdsJson), request.UsuarioId),
            ct);
        state.Chunks = context.Chunks;

        await audit.RecordAsync(
            request,
            "ContextoRecuperado",
            new { trechos = context.Chunks.Count, tokensAproximados = context.TokensAproximados, descartados = context.Descartados },
            ct);

        if (await IsCancelledAsync(request, ct))
        {
            return new WorkOutcome(WorkOutcomeKind.Cancelado);
        }

        var input = new PecaInput(
            request.TipoPeca, request.Area, request.Fatos, request.Pedidos, request.InstrucoesAdicionais,
            ParseDocumentos(request.DocumentosIdsJson), null);
        var prompt = promptBuilder.Build(input, context.Chunks);

        await documents.StartGenerationAsync(request, prompt.Versao, llm.ModelName, ct);
        await documents.SetStageAsync(request, LegalDocumentStatus.Gerando, "Gerando a minuta", 40, ct);
        await audit.RecordAsync(request, "GeracaoIniciada", new { modelo = llm.ModelName, prompt = prompt.Versao }, ct);

        state.Clock.Start();
        var chat = new LlmChatRequest(
        [
            new LlmMessage("system", prompt.System),
            new LlmMessage("user", prompt.User),
        ]);

        var options = filaOptions.Value;
        var sinceFlush = Stopwatch.StartNew();

        await foreach (var chunk in llm.ChatStreamAsync(chat, ct))
        {
            state.PromptTokens = chunk.PromptTokens ?? state.PromptTokens;
            state.OutputTokens = chunk.OutputTokens ?? state.OutputTokens;

            if (chunk.Content.Length > 0)
            {
                state.Text.Append(chunk.Content);
            }

            if (sinceFlush.ElapsedMilliseconds < options.ParcialIntervaloMs)
            {
                continue;
            }

            sinceFlush.Restart();
            if (await IsCancelledAsync(request, ct))
            {
                // Sair do foreach descarta o enumerador e fecha a conexão: o Ollama para de gerar.
                state.Clock.Stop();

                return new WorkOutcome(WorkOutcomeKind.Cancelado);
            }

            await documents.SavePartialAsync(request, state.Text.ToString(), ProgressFor(state.Text.Length), ct);
        }

        state.Clock.Stop();
        var text = state.Text.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new TransientGenerationException("O modelo terminou sem devolver texto.");
        }

        await documents.SetStageAsync(request, LegalDocumentStatus.Salvando, "Salvando a minuta", 95, ct);
        if (await IsCancelledAsync(request, ct))
        {
            return new WorkOutcome(WorkOutcomeKind.Cancelado);
        }

        var caseText = $"{request.Fatos}\n{request.Pedidos}\n{request.InstrucoesAdicionais}";
        var report = CitationVerifier.Verify(text, context.Chunks, caseText);
        var avisos = BuildWarnings(text, context, report);
        var fontes = context.Chunks.Select(chunk => (chunk, CitationVerifier.WasCited(chunk, report))).ToList();

        await documents.CompleteAsync(
            request, text, state.Clock.ElapsedMilliseconds, state.PromptTokens, state.OutputTokens, fontes, avisos, ct);
        await audit.RecordAsync(
            request,
            "GeracaoConcluida",
            new
            {
                tempoMs = state.Clock.ElapsedMilliseconds,
                tokensEntrada = state.PromptTokens,
                tokensSaida = state.OutputTokens,
                fontesCitadas = fontes.Count(f => f.Item2),
                citacoesDesconhecidas = report.UnknownCitations.Count,
                referenciasSemFonte = report.UnsupportedReferences.Count,
            },
            ct);
        await notificacao.NotificarConclusaoAsync(request, ct);

        return new WorkOutcome(WorkOutcomeKind.Concluido);
    }

    private async Task<WorkOutcome> HandleFailureAsync(
        LegalDocumentRequest request, RunState state, Exception failure, int tentativa)
    {
        // A requisição/serviço pode estar encerrando: gravar a falha não pode depender do token dele.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = timeout.Token;

        var motivo = $"{failure.GetType().Name}: {failure.Message}";
        var transitoria = IsTransient(failure);
        var podeRepetir = transitoria && tentativa < filaOptions.Value.MaxTentativas;

        // O stack trace vai para o log; o banco guarda o resumo; o usuário não recebe nenhum dos dois.
        logger.LogWarning(
            failure, "Peças: solicitação {SolicitacaoId} falhou na tentativa {Tentativa} (transitória={Transitoria}).",
            request.Id.Value, tentativa, transitoria);

        try
        {
            if (podeRepetir)
            {
                await documents.RequeueAsync(request, motivo, token);
                await audit.RecordAsync(request, "Reagendada", new { erro = failure.GetType().Name, tentativa }, token);

                return new WorkOutcome(WorkOutcomeKind.Reagendar, motivo);
            }

            var fontes = state.Chunks.Select(chunk => (chunk, false)).ToList();
            await documents.FailAsync(request, motivo, state.Text.ToString(), state.Clock.ElapsedMilliseconds, fontes, token);
            await audit.RecordAsync(request, "Falhou", new { erro = failure.GetType().Name, tentativa }, token);
            await notificacao.NotificarFalhaAsync(request, token);
        }
        catch (Exception persistFailure)
        {
            logger.LogError(
                persistFailure, "Peças: não foi possível gravar a falha da solicitação {SolicitacaoId}.", request.Id.Value);
        }

        return new WorkOutcome(WorkOutcomeKind.Falhou, motivo);
    }

    private async Task FailTerminalAsync(Guid referenciaId, string erro, string eventoTipo, CancellationToken ct)
    {
        var request = await LoadAsync(referenciaId, ct);
        if (request is null || request.IsTerminal)
        {
            return;
        }

        await documents.FailAsync(request, erro, null, 0, [], ct);
        await audit.RecordAsync(request, eventoTipo, null, ct);
        await notificacao.NotificarFalhaAsync(request, ct);
    }

    private Task<LegalDocumentRequest?> LoadAsync(Guid id, CancellationToken ct) =>
        repository.GetByIdAsync(LegalDocumentRequestId.From(id), ct);

    private async Task<bool> IsCancelledAsync(LegalDocumentRequest request, CancellationToken ct)
    {
        var snapshot = await repository.GetSnapshotAsync(request.Id, ct);

        return snapshot is null || snapshot.Status == LegalDocumentStatus.Cancelado;
    }

    private static bool IsTransient(Exception exception) =>
        exception is LlmServiceException or VectorStoreException or TimeoutException or HttpRequestException
            or TransientGenerationException;

    private static int ProgressFor(int chars) =>
        Math.Min(90, 40 + (int)(chars / 3.5 / TokensEsperados * 50));

    private static IReadOnlyList<Guid> ParseDocumentos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
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

    private static async Task Safe(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception)
        {
            // Melhor esforço durante o desligamento.
        }
    }

    internal static List<string> BuildWarnings(string text, RagContext context, CitationReport report)
    {
        var avisos = new List<string>();

        if (context.Chunks.Count == 0)
        {
            avisos.Add("Nenhum trecho relevante foi recuperado do acervo do escritório: a minuta não tem base documental e os fundamentos jurídicos precisam ser pesquisados.");
        }

        if (context.Chunks.Count > 0 && report.CitedLabels.Count == 0)
        {
            avisos.Add("A minuta não cita nenhuma das fontes recuperadas no formato [ARQUIVO, p. X]. Confira os fundamentos.");
        }

        if (report.UnknownCitations.Count > 0)
        {
            avisos.Add($"A minuta cita fontes que não constam do contexto recuperado: {string.Join("; ", report.UnknownCitations)}. Não confie nessas citações.");
        }

        if (report.UnsupportedReferences.Count > 0)
        {
            avisos.Add($"Referências legais sem respaldo nas fontes nem nos dados do caso: {string.Join("; ", report.UnsupportedReferences)}. Verifique se existem e se dizem o que a minuta afirma.");
        }

        if (!text.Contains("FONTES UTILIZADAS", StringComparison.OrdinalIgnoreCase)
            || !text.Contains("PONTOS PARA REVISÃO HUMANA", StringComparison.OrdinalIgnoreCase))
        {
            avisos.Add("A minuta não trouxe as seções finais exigidas (FONTES UTILIZADAS e PONTOS PARA REVISÃO HUMANA). Revise com mais atenção.");
        }

        if (text.Contains("pronta para protocolo", StringComparison.OrdinalIgnoreCase)
            || text.Contains("pronto para protocolo", StringComparison.OrdinalIgnoreCase))
        {
            avisos.Add("O texto afirma estar pronto para protocolo. Isso é falso: a minuta exige revisão e validação por profissional habilitado.");
        }

        return avisos;
    }

    private sealed class RunState
    {
        public StringBuilder Text { get; } = new();

        public IReadOnlyList<RetrievedChunk> Chunks { get; set; } = [];

        public Stopwatch Clock { get; } = new();

        public int? PromptTokens { get; set; }

        public int? OutputTokens { get; set; }
    }
}
