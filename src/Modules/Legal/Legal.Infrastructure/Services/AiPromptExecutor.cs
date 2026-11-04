using System.Diagnostics;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Services;

/// <summary>Resposta bruta de uma chamada, ainda NÃO validada, mais o que a auditoria precisa.</summary>
public sealed record AiCompletion(
    string Purpose,
    string Text,
    string Model,
    string PromptVersion,
    AnthropicUsage Usage,
    decimal CostUsd,
    int LatencyMs,
    string? RequestId,
    bool ContentTruncated);

/// <summary>
/// Passos comuns às duas chamadas de IA (classificação e prazo): carregar o prompt versionado,
/// cortar o texto longo, chamar a Anthropic, medir latência e custo e registrar em
/// ai_usage_logs.
///
/// O registro tem dois momentos porque o desfecho só é conhecido depois da validação:
/// se a chamada FALHA, este executor já grava o log (status Failed); se a chamada RESPONDE,
/// quem chamou valida e então confirma via <see cref="RecordOutcomeAsync"/> (Succeeded ou
/// InvalidResponse). Assim nenhuma chamada — nem a de resposta inválida, que foi cobrada —
/// fica fora da auditoria.
///
/// NUNCA loga o conteúdo da publicação, o prompt renderizado ou o texto devolvido pelo modelo.
/// </summary>
public sealed class AiPromptExecutor(
    IPromptCatalog promptCatalog,
    IAnthropicClient anthropicClient,
    IAiCostCalculator costCalculator,
    IAiUsageRecorder usageRecorder,
    IOptions<AnthropicModelsOptions> modelsOptions,
    ILogger<AiPromptExecutor> logger)
{
    public const string Provider = "Anthropic";

    public async Task<Result<AiCompletion>> CompleteAsync(
        string purpose,
        string promptName,
        string model,
        string rawContent,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var prompt = promptCatalog.Get(promptName);
        if (prompt.IsFailure)
        {
            logger.LogError("IA: prompt '{PromptName}' não encontrado.", promptName);
            return Result.Failure<AiCompletion>(prompt.Error);
        }

        var template = prompt.Value;
        var (content, truncated) = Truncate(rawContent, modelsOptions.Value.MaxContentCharacters);

        var renderValues = new Dictionary<string, string>(values) { ["raw_content"] = content };

        var request = new AnthropicRequest(
            model,
            template.SystemPrompt,
            template.RenderUser(renderValues),
            modelsOptions.Value.MaxOutputTokens);

        var stopwatch = Stopwatch.StartNew();
        var response = await anthropicClient.SendAsync(request, cancellationToken);
        stopwatch.Stop();
        var latencyMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);

        if (response.IsFailure)
        {
            logger.LogWarning(
                "IA: chamada '{Purpose}' ao modelo {Model} falhou ({ErrorCode}).", purpose, model, response.Error.Code);

            await usageRecorder.RecordAsync(
                new AiUsageEntry(
                    purpose, Provider, model, template.Version, 0, 0, 0, 0, 0m, latencyMs,
                    AiCallStatus.Failed, response.Error.Code, null),
                cancellationToken);

            return Result.Failure<AiCompletion>(response.Error);
        }

        var answer = response.Value;
        var cost = costCalculator.Calculate(answer.Model, answer.Usage);

        return Result.Success(new AiCompletion(
            purpose, answer.Text, answer.Model, template.Version, answer.Usage, cost, latencyMs, answer.RequestId, truncated));
    }

    /// <summary>Grava o desfecho de uma chamada que RESPONDEU (e foi cobrada).</summary>
    public Task RecordOutcomeAsync(
        AiCompletion completion, AiCallStatus status, string? errorCode, CancellationToken cancellationToken) =>
        usageRecorder.RecordAsync(
            new AiUsageEntry(
                completion.Purpose,
                Provider,
                completion.Model,
                completion.PromptVersion,
                completion.Usage.InputTokens,
                completion.Usage.OutputTokens,
                completion.Usage.CacheCreationInputTokens,
                completion.Usage.CacheReadInputTokens,
                completion.CostUsd,
                completion.LatencyMs,
                status,
                errorCode,
                completion.RequestId),
            cancellationToken);

    /// <summary>
    /// Corta no limite de caracteres (público para teste) sem partir um par substituto (emoji, por exemplo), que
    /// viraria um caractere inválido no JSON enviado.
    /// </summary>
    public static (string Content, bool Truncated) Truncate(string content, int maxCharacters)
    {
        if (maxCharacters <= 0 || content.Length <= maxCharacters)
        {
            return (content, false);
        }

        var length = maxCharacters;
        if (char.IsHighSurrogate(content[length - 1]))
        {
            length--;
        }

        return (content[..length], true);
    }
}
