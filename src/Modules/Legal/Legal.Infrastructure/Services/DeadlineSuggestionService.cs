using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Ai;
using Advocacia.Modules.Legal.Domain.Ai;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Services;

/// <summary>
/// Sugere o prazo com o prompt versionado deadline.md, dado o tipo de ato já classificado.
/// Mesmo fluxo de <see cref="ClassificationService"/>.
/// </summary>
public sealed class DeadlineSuggestionService(
    AiPromptExecutor executor,
    IOptions<AnthropicModelsOptions> modelsOptions,
    ILogger<DeadlineSuggestionService> logger) : IDeadlineSuggestionService
{
    public const string Purpose = "deadline";
    public const string PromptName = "deadline";

    public async Task<Result<DeadlineSuggestion>> SuggestAsync(
        string rawContent, string actType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return Result.Failure<DeadlineSuggestion>(ErrorFactory.From(ErrorCode.PUBLICATION_EMPTY_CONTENT));
        }

        // actType entra no prompt: só aceita valor da lista fechada, nunca texto livre.
        if (!ActTypes.All.Contains(actType))
        {
            return Result.Failure<DeadlineSuggestion>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(actType)));
        }

        var values = new Dictionary<string, string> { ["act_type"] = actType };

        var completion = await executor.CompleteAsync(
            Purpose, PromptName, modelsOptions.Value.Deadline, rawContent, values, cancellationToken);

        if (completion.IsFailure)
        {
            return Result.Failure<DeadlineSuggestion>(completion.Error);
        }

        var answer = completion.Value;
        var parsed = AiResponseParser.ParseDeadline(answer.Text);

        if (!parsed.IsValid)
        {
            logger.LogError(
                "Prazo: resposta do modelo {Model} (prompt {PromptVersion}) rejeitada: {Reason}.",
                answer.Model, answer.PromptVersion, parsed.Reason);

            await executor.RecordOutcomeAsync(
                answer, AiCallStatus.InvalidResponse, nameof(ErrorCode.AI_INVALID_RESPONSE), cancellationToken);

            return Result.Failure<DeadlineSuggestion>(ErrorFactory.From(ErrorCode.AI_INVALID_RESPONSE));
        }

        await executor.RecordOutcomeAsync(answer, AiCallStatus.Succeeded, null, cancellationToken);

        var value = parsed.Value!;

        return Result.Success(new DeadlineSuggestion(
            value.Days,
            value.Type,
            value.LegalBasis,
            value.Confidence,
            value.Confidence < PublicationSuggestion.ManualReviewConfidenceThreshold,
            answer.ContentTruncated,
            new AiCallMetadata(AiPromptExecutor.Provider, answer.Model, answer.PromptVersion),
            value.Json));
    }
}
