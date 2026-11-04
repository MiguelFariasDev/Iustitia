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
/// Classifica a publicação com o prompt versionado classification.md. Confiança abaixo de
/// <see cref="PublicationSuggestion.ManualReviewConfidenceThreshold"/>
/// marca o resultado como pendente de revisão manual — o resultado nunca é aplicado sozinho.
/// </summary>
public sealed class ClassificationService(
    AiPromptExecutor executor,
    IOptions<AnthropicModelsOptions> modelsOptions,
    ILogger<ClassificationService> logger) : IClassificationService
{
    public const string Purpose = "classification";
    public const string PromptName = "classification";

    public async Task<Result<ClassificationResult>> ClassifyAsync(
        string rawContent, string? tribunal, string? area, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return Result.Failure<ClassificationResult>(ErrorFactory.From(ErrorCode.PUBLICATION_EMPTY_CONTENT));
        }

        var values = new Dictionary<string, string>
        {
            ["tribunal"] = string.IsNullOrWhiteSpace(tribunal) ? "não informado" : tribunal.Trim(),
            ["area"] = string.IsNullOrWhiteSpace(area) ? "não informada" : area.Trim(),
        };

        var completion = await executor.CompleteAsync(
            Purpose, PromptName, modelsOptions.Value.Classification, rawContent, values, cancellationToken);

        if (completion.IsFailure)
        {
            return Result.Failure<ClassificationResult>(completion.Error);
        }

        var answer = completion.Value;
        var parsed = AiResponseParser.ParseClassification(answer.Text);

        if (!parsed.IsValid)
        {
            // O motivo é um texto fixo com o nome do campo — nunca o conteúdo devolvido.
            logger.LogError(
                "Classificação: resposta do modelo {Model} (prompt {PromptVersion}) rejeitada: {Reason}.",
                answer.Model, answer.PromptVersion, parsed.Reason);

            await executor.RecordOutcomeAsync(
                answer, AiCallStatus.InvalidResponse, nameof(ErrorCode.AI_INVALID_RESPONSE), cancellationToken);

            return Result.Failure<ClassificationResult>(ErrorFactory.From(ErrorCode.AI_INVALID_RESPONSE));
        }

        await executor.RecordOutcomeAsync(answer, AiCallStatus.Succeeded, null, cancellationToken);

        var value = parsed.Value!;
        var requiresManualReview = value.Confidence < PublicationSuggestion.ManualReviewConfidenceThreshold;

        if (requiresManualReview)
        {
            logger.LogInformation(
                "Classificação: confiança {Confidence} abaixo do limiar — pendente de revisão manual.", value.Confidence);
        }

        return Result.Success(new ClassificationResult(
            value.ActType,
            value.DeadlineDays,
            value.DeadlineType,
            value.LegalBasis,
            value.Confidence,
            requiresManualReview,
            answer.ContentTruncated,
            new AiCallMetadata(AiPromptExecutor.Provider, answer.Model, answer.PromptVersion),
            value.Json));
    }
}
