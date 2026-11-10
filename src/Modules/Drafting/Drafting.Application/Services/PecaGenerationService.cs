using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Orquestra a geração: validar → persistir → recuperar contexto → montar prompt → gerar em
/// streaming → conferir citações → persistir → evento final. Falha em qualquer passo grava a
/// solicitação como Falhou (com o motivo técnico) e entrega ao usuário só uma mensagem genérica.
/// </summary>
public sealed class PecaGenerationService(
    IOptions<DraftingOptions> draftingOptions,
    ILegalDocumentService documents,
    IRagService rag,
    IPromptBuilder promptBuilder,
    ILlmService llm,
    IDraftingAuditService audit,
    ILogger<PecaGenerationService> logger) : IPecaGenerationService
{
    private const string MensagemFalha = "Não foi possível gerar a peça. Tente novamente.";

    public async Task<Result<PreparedGeneration>> PrepareAsync(
        GerarPecaRequest request, CancellationToken cancellationToken = default)
    {
        if (!draftingOptions.Value.Enabled)
        {
            return Result.Failure<PreparedGeneration>(ErrorFactory.From(ErrorCode.DRAFTING_DISABLED));
        }

        var input = GerarPecaValidation.Validate(request);
        if (input.IsFailure)
        {
            return Result.Failure<PreparedGeneration>(input.Error);
        }

        var created = await documents.CreateAsync(input.Value, cancellationToken);
        if (created.IsFailure)
        {
            return Result.Failure<PreparedGeneration>(created.Error);
        }

        await audit.RecordAsync(
            created.Value,
            "Recebida",
            new { documentos = input.Value.DocumentosIds.Count, versao = created.Value.Versao },
            cancellationToken);

        return Result.Success(new PreparedGeneration(created.Value, input.Value));
    }

    public async IAsyncEnumerable<GenerationEvent> StreamAsync(
        PreparedGeneration prepared, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var state = new RunState();
        await using var inner = RunAsync(prepared, state, cancellationToken).GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            GenerationEvent? next = null;
            Exception? failure = null;

            try
            {
                if (await inner.MoveNextAsync())
                {
                    next = inner.Current;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure is not null)
            {
                var cancelled = cancellationToken.IsCancellationRequested;
                await RecordFailureAsync(prepared.Request, state, failure, cancelled);

                // Cliente que foi embora não lê mais o stream: não há a quem entregar o evento de erro.
                if (!cancelled)
                {
                    yield return new ErroEvent(MensagemFalha, prepared.Request.Id.Value);
                }

                yield break;
            }

            if (next is null)
            {
                yield break;
            }

            yield return next;
        }
    }

    public async Task<Result<GerarPecaResponse>> GenerateAsync(
        PreparedGeneration prepared, CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder();

        await foreach (var evento in StreamAsync(prepared, cancellationToken))
        {
            switch (evento)
            {
                case FragmentoEvent fragmento:
                    text.Append(fragmento.Conteudo);
                    break;
                case FimEvent fim:
                    return Result.Success(new GerarPecaResponse(
                        fim.SolicitacaoId, fim.Versao, text.ToString(), fim.Fontes, fim.Avisos, fim.Aviso,
                        fim.TempoGeracaoMs, fim.TraceId));
                case ErroEvent:
                    return Result.Failure<GerarPecaResponse>(ErrorFactory.From(ErrorCode.DRAFTING_GENERATION_FAILED));
            }
        }

        return Result.Failure<GerarPecaResponse>(ErrorFactory.From(ErrorCode.DRAFTING_GENERATION_FAILED));
    }

    /// <summary>Os passos felizes. Qualquer exceção sobe para <see cref="StreamAsync"/>, que trata a falha num lugar só.</summary>
    private async IAsyncEnumerable<GenerationEvent> RunAsync(
        PreparedGeneration prepared, RunState state, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = prepared.Request;
        var input = prepared.Input;
        var tenantId = request.TenantId;

        var context = await rag.RetrieveAsync(
            new RagQuery(tenantId, input.TipoPeca, input.Area, input.Fatos, input.Pedidos, input.DocumentosIds),
            cancellationToken);
        state.Chunks = context.Chunks;

        await audit.RecordAsync(
            request,
            "ContextoRecuperado",
            new { trechos = context.Chunks.Count, tokensAproximados = context.TokensAproximados, descartados = context.Descartados },
            cancellationToken);

        var prompt = promptBuilder.Build(input, context.Chunks);
        await documents.StartAsync(request, prompt.Versao, llm.ModelName, cancellationToken);
        await audit.RecordAsync(request, "GeracaoIniciada", new { modelo = llm.ModelName, prompt = prompt.Versao }, cancellationToken);

        state.Clock.Start();
        var chat = new LlmChatRequest(
        [
            new LlmMessage("system", prompt.System),
            new LlmMessage("user", prompt.User),
        ]);

        await foreach (var chunk in llm.ChatStreamAsync(chat, cancellationToken))
        {
            state.PromptTokens = chunk.PromptTokens ?? state.PromptTokens;
            state.OutputTokens = chunk.OutputTokens ?? state.OutputTokens;

            if (chunk.Content.Length == 0)
            {
                continue;
            }

            state.Text.Append(chunk.Content);
            yield return new FragmentoEvent(chunk.Content);
        }

        state.Clock.Stop();
        var text = state.Text.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("O modelo terminou sem devolver texto.");
        }

        var caseText = $"{input.Fatos}\n{input.Pedidos}\n{input.InstrucoesAdicionais}";
        var report = CitationVerifier.Verify(text, context.Chunks, caseText);
        var avisos = BuildWarnings(text, context, report);
        var fontes = context.Chunks.Select(chunk => (chunk, CitationVerifier.WasCited(chunk, report))).ToList();

        await documents.CompleteAsync(request, text, state.Clock.ElapsedMilliseconds, fontes, cancellationToken);
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
            cancellationToken);

        var fonteResponses = request.Sources
            .Select(source => new FonteResponse(
                source.Id,
                source.DocumentoOrigemId,
                source.NomeArquivo,
                source.Pagina,
                Excerpt(source.Trecho),
                source.Score,
                source.Citada,
                UrlOf(source.MetadadosJson)))
            .ToList();

        yield return new FimEvent(
            request.Id.Value,
            request.Versao,
            fonteResponses,
            avisos,
            SystemPrompt.Aviso,
            state.Clock.ElapsedMilliseconds,
            Activity.Current?.TraceId.ToString());
    }

    private async Task RecordFailureAsync(LegalDocumentRequest request, RunState state, Exception failure, bool cancelled)
    {
        // A requisição pode já ter sido abortada: gravar a falha não pode depender do token dela.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = timeout.Token;

        var motivo = cancelled ? "Cancelado pelo cliente." : $"{failure.GetType().Name}: {failure.Message}";
        var fontes = state.Chunks.Select(chunk => (chunk, false)).ToList();

        // O stack trace vai para o log; o banco guarda o resumo; o usuário não recebe nenhum dos dois.
        logger.LogWarning(
            failure, "Peças: geração da solicitação {SolicitacaoId} {Resultado}.",
            request.Id.Value, cancelled ? "cancelada" : "falhou");

        try
        {
            await documents.FailAsync(
                request, motivo, state.Text.ToString(), state.Clock.ElapsedMilliseconds, fontes, token);
            await audit.RecordAsync(
                request, cancelled ? "Cancelada" : "Falhou", new { erro = failure.GetType().Name }, token);
        }
        catch (Exception persistFailure)
        {
            logger.LogError(
                persistFailure, "Peças: não foi possível gravar a falha da solicitação {SolicitacaoId}.", request.Id.Value);
        }
    }

    private static List<string> BuildWarnings(string text, RagContext context, CitationReport report)
    {
        var avisos = new List<string>();

        if (context.Chunks.Count == 0)
        {
            avisos.Add("Nenhum trecho relevante foi recuperado das fontes do escritório: a minuta não tem base documental e os fundamentos jurídicos precisam ser pesquisados.");
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

    private static string Excerpt(string text) => text.Length <= 400 ? text : text[..400] + "…";

    private static string? UrlOf(string? metadadosJson)
    {
        if (string.IsNullOrEmpty(metadadosJson))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(metadadosJson);

            return document.RootElement.TryGetProperty("UrlOrigem", out var url) && url.ValueKind == System.Text.Json.JsonValueKind.String
                ? url.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
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
