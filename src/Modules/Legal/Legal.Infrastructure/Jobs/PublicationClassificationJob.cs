using System.Text.Json.Nodes;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Errors.Exceptions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Ai;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Jobs;

/// <summary>
/// Job recorrente (a cada 15 min) que pede ao Claude uma SUGESTÃO de tipo de ato e prazo para
/// as publicações PendingReview que ainda não têm uma, e a grava em publication_suggestions.
///
/// O que este job NÃO faz — e não deve passar a fazer sem revisão humana (ADR-012, Provimento
/// CFOAB nº 271/2025): alterar o status ou o tipo da publicação, criar prazo, tarefa ou alerta.
/// A sugestão fica parada até um advogado revisá-la (Etapa 3.3).
///
/// Garantias:
///   - Idempotente: só pega publicação sem sugestão, e publication_id é único no banco.
///   - Resiliente por publicação: resposta inválida do modelo é contada e logada, sem abortar
///     o lote; após MaxInvalidResponseAttempts a publicação deixa de ser tentada (cada
///     tentativa é paga).
///   - Para cedo quando o provedor está fora (indisponível, timeout, limite, credencial): é
///     inútil — e caro — insistir publicação a publicação. O que já foi gravado permanece e o
///     job relança ao final para o Hangfire aplicar o retry com backoff.
///   - Cada chamada de IA é auditada em ai_usage_logs, mesmo as que falham na validação.
///
/// NUNCA loga o conteúdo da publicação nem o texto devolvido pelo modelo.
/// </summary>
public sealed class PublicationClassificationJob(
    IPublicationSuggestionRepository suggestionRepository,
    IAnthropicUsageQuery usageQuery,
    IProcessoRepository processoRepository,
    IClassificationService classificationService,
    IDeadlineSuggestionService deadlineSuggestionService,
    IAiUsageContext usageContext,
    ILegalUnitOfWork unitOfWork,
    LegalDbContext dbContext,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IOptions<PublicationClassificationJobOptions> options,
    ILogger<PublicationClassificationJob> logger)
{
    /// <summary>
    /// DisableConcurrentExecution (lock distribuído no Hangfire, vale entre instâncias do
    /// Worker): duas execuções simultâneas classificariam — e cobrariam — a mesma publicação
    /// duas vezes antes de o índice único barrar a segunda gravação.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var jobOptions = options.Value;

        if (!jobOptions.Enabled)
        {
            logger.LogInformation("PublicationClassificationJob: desabilitado por configuração.");
            return;
        }

        var tenantIds = jobOptions.TenantIds.Count > 0
            ? jobOptions.TenantIds
            : await suggestionRepository.ListTenantsAwaitingSuggestionAsync(cancellationToken);

        if (tenantIds.Count == 0)
        {
            logger.LogInformation("PublicationClassificationJob: nenhuma publicação pendente de sugestão.");
            return;
        }

        logger.LogInformation("PublicationClassificationJob iniciado: {TenantCount} escritórios com trabalho.", tenantIds.Count);

        var totals = new Totals();
        Error? providerFailure = null;

        foreach (var tenantId in tenantIds)
        {
            // O job roda fora de uma requisição: sem tenant o filtro global e o RLS não têm o
            // que aplicar (fail closed) e nada seria encontrado nem gravado.
            tenantContext.SetTenant(tenantId);

            providerFailure = await ProcessTenantAsync(tenantId, jobOptions, totals, cancellationToken);

            if (providerFailure is not null)
            {
                // Provedor fora do ar: para tudo, não só este escritório.
                break;
            }
        }

        logger.LogInformation(
            "PublicationClassificationJob concluído: {Suggested} sugestões gravadas ({ManualReview} para revisão manual), {Skipped} ignoradas por tentativas esgotadas, {Invalid} respostas inválidas, {Errors} erros.",
            totals.Suggested, totals.ManualReview, totals.Skipped, totals.Invalid, totals.Errors);

        if (providerFailure is not null)
        {
            // Relança para o Hangfire marcar a execução como falha e retentar com backoff.
            throw new IntegrationException(ParseCode(providerFailure.Code));
        }
    }

    /// <returns>O erro do provedor se ele estiver fora do ar (a execução deve parar); senão nulo.</returns>
    private async Task<Error?> ProcessTenantAsync(
        Guid tenantId, PublicationClassificationJobOptions jobOptions, Totals totals, CancellationToken cancellationToken)
    {
        var batch = Math.Max(1, jobOptions.BatchSizePerTenant);
        var maxAttempts = Math.Max(1, jobOptions.MaxInvalidResponseAttempts);

        var eligible = new List<Publication>();
        var skip = 0;

        // Pagina até juntar um lote elegível: publicações que esgotaram as tentativas ficam no
        // início da fila (as mais antigas) e, sem pular por cima delas, matariam de fome as demais.
        while (eligible.Count < batch)
        {
            var page = await suggestionRepository.ListAwaitingSuggestionAsync(tenantId, skip, batch, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            skip += page.Count;

            var attempts = await usageQuery.CountFailuresByReferenceAsync(
                tenantId,
                ClassificationService.Purpose,
                nameof(ErrorCode.AI_INVALID_RESPONSE),
                page.Select(publication => publication.Id.Value).ToList(),
                cancellationToken);

            foreach (var publication in page)
            {
                if (attempts.TryGetValue(publication.Id.Value, out var count) && count >= maxAttempts)
                {
                    totals.Skipped++;
                    continue;
                }

                if (eligible.Count < batch)
                {
                    eligible.Add(publication);
                }
            }

            if (page.Count < batch)
            {
                break;
            }
        }

        logger.LogInformation(
            "PublicationClassificationJob: escritório {TenantId} com {Count} publicações a classificar.", tenantId, eligible.Count);

        foreach (var publication in eligible)
        {
            try
            {
                usageContext.PublicationId = publication.Id.Value;

                var outcome = await ProcessPublicationAsync(tenantId, publication, totals, cancellationToken);
                if (outcome is not null)
                {
                    return outcome;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateException exception)
            {
                // Falha de persistência de UMA publicação não derruba o lote. Limpa o tracker:
                // a entidade que falhou continuaria pendente e quebraria o próximo SaveChanges.
                dbContext.ChangeTracker.Clear();
                totals.Errors++;

                logger.LogError(
                    exception, "PublicationClassificationJob: falha ao gravar a sugestão da publicação {PublicationId}.",
                    publication.Id.Value);
            }
            finally
            {
                usageContext.PublicationId = null;
            }
        }

        return null;
    }

    private async Task<Error?> ProcessPublicationAsync(
        Guid tenantId, Publication publication, Totals totals, CancellationToken cancellationToken)
    {
        var area = await ResolveAreaAsync(publication, cancellationToken);
        var content = publication.RawContent.Value;

        var classification = await classificationService.ClassifyAsync(content, publication.Tribunal, area, cancellationToken);

        if (classification.IsFailure)
        {
            return HandleFailure("classificação", publication, classification.Error, totals);
        }

        var classified = classification.Value;

        // Segunda chamada, só depois de uma classificação bem-sucedida: o prazo é pedido para
        // o tipo de ato já determinado.
        var deadline = await deadlineSuggestionService.SuggestAsync(content, classified.ActType, cancellationToken);

        Error? providerFailure = null;
        DeadlineSuggestion? refined = null;

        if (deadline.IsSuccess)
        {
            refined = deadline.Value;
        }
        else
        {
            // A classificação já foi paga e é válida: grava a sugestão com o prazo que ela
            // mesma trouxe, em vez de descartar e pagar de novo na próxima execução.
            providerFailure = HandleFailure("prazo", publication, deadline.Error, totals);
        }

        var suggestion = BuildSuggestion(tenantId, publication.Id, classified, refined);
        if (suggestion.IsFailure)
        {
            totals.Errors++;
            logger.LogError(
                "PublicationClassificationJob: sugestão da publicação {PublicationId} rejeitada pelo domínio ({ErrorCode}).",
                publication.Id.Value, suggestion.Error.Code);
            return providerFailure;
        }

        suggestionRepository.Add(suggestion.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        totals.Suggested++;
        if (suggestion.Value.RequiresManualReview)
        {
            totals.ManualReview++;
        }

        logger.LogInformation(
            "PublicationClassificationJob: publicação {PublicationId} classificada (confiança {Confidence}, revisão manual: {ManualReview}).",
            publication.Id.Value, suggestion.Value.Confidence, suggestion.Value.RequiresManualReview);

        return providerFailure;
    }

    private Result<PublicationSuggestion> BuildSuggestion(
        Guid tenantId, PublicationId publicationId, ClassificationResult classified, DeadlineSuggestion? refined)
    {
        // O prazo refinado, quando existe, vale mais que o da classificação. A confiança da
        // sugestão é o MENOR dos dois: ela é tão confiável quanto a parte menos confiável.
        var days = refined is null ? classified.DeadlineDays : refined.Days;
        var type = refined is null ? classified.DeadlineType : refined.Type;
        var basis = refined?.LegalBasis ?? classified.LegalBasis;
        var confidence = refined is null ? classified.Confidence : Math.Min(classified.Confidence, refined.Confidence);

        var raw = new JsonObject
        {
            ["classification"] = JsonNode.Parse(classified.RawJson),
            ["deadline"] = refined is null ? null : JsonNode.Parse(refined.RawJson),
        };

        // Duas versões de prompt, uma por chamada: "classificação/prazo". Sem a segunda,
        // repete a primeira (a sugestão veio só da classificação).
        var promptVersion = refined is null
            ? classified.Call.PromptVersion
            : $"{classified.Call.PromptVersion}/{refined.Call.PromptVersion}";

        var model = refined is null || refined.Call.Model == classified.Call.Model
            ? classified.Call.Model
            : $"{classified.Call.Model}+{refined.Call.Model}";

        return PublicationSuggestion.Create(
            tenantId,
            publicationId,
            classified.ActType,
            days,
            type,
            basis,
            confidence,
            classified.ContentTruncated || refined?.ContentTruncated == true,
            classified.Call.Provider,
            model,
            promptVersion,
            raw.ToJsonString(),
            dateTimeProvider.UtcNow);
    }

    /// <summary>Contabiliza a falha e devolve o erro SE ele indica provedor fora do ar.</summary>
    private Error? HandleFailure(string step, Publication publication, Error error, Totals totals)
    {
        if (error.Code == nameof(ErrorCode.AI_INVALID_RESPONSE))
        {
            totals.Invalid++;
            logger.LogWarning(
                "PublicationClassificationJob: resposta inválida na etapa de {Step} da publicação {PublicationId}.",
                step, publication.Id.Value);
            return null;
        }

        totals.Errors++;
        logger.LogWarning(
            "PublicationClassificationJob: falha na etapa de {Step} da publicação {PublicationId}: {ErrorCode}.",
            step, publication.Id.Value, error.Code);

        return IsProviderOutage(error.Code) ? error : null;
    }

    private static bool IsProviderOutage(string code) =>
        code is nameof(ErrorCode.AI_UNAVAILABLE)
            or nameof(ErrorCode.AI_TIMEOUT)
            or nameof(ErrorCode.AI_UNAUTHORIZED)
            or nameof(ErrorCode.AI_RATE_LIMITED)
            or nameof(ErrorCode.AI_OVERLOADED);

    private static ErrorCode ParseCode(string code) =>
        Enum.TryParse<ErrorCode>(code, out var parsed) ? parsed : ErrorCode.AI_UNAVAILABLE;

    private async Task<string?> ResolveAreaAsync(Publication publication, CancellationToken cancellationToken)
    {
        if (publication.ProcessId is not { } processId)
        {
            return null;
        }

        var processo = await processoRepository.GetByIdAsync(ProcessoId.From(processId), cancellationToken);

        return processo?.Area.ToString();
    }

    private sealed class Totals
    {
        public int Suggested { get; set; }

        public int ManualReview { get; set; }

        public int Skipped { get; set; }

        public int Invalid { get; set; }

        public int Errors { get; set; }
    }
}
