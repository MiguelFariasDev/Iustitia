using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Domain.Suggestions;

/// <summary>
/// Sugestão gerada por IA (tipo do ato e prazo) para UMA publicação. É só uma sugestão: não
/// altera <see cref="Publication.Type"/>, não cria tarefa nem prazo — isso só acontece depois
/// da confirmação explícita do advogado, na revisão humana (Etapa 3.3; ADR-012, Provimento
/// CFOAB nº 271/2025). Por isso a entidade é imutável depois de criada.
///
/// Uma publicação tem no máximo uma sugestão (índice único em publication_id): reprocessar
/// não duplica nem sobrescreve o que um advogado possa já estar revendo.
/// </summary>
public sealed class PublicationSuggestion : Entity<PublicationSuggestionId>, IHasTenant
{
    /// <summary>Abaixo disto a sugestão vai para revisão manual sem apoio da IA.</summary>
    public const int ManualReviewConfidenceThreshold = 50;

    public const int MaxDeadlineDays = 365;

    public Guid TenantId { get; private set; }

    public PublicationId PublicationId { get; private set; }

    public string ActType { get; private set; } = null!;

    /// <summary>Nulo quando a publicação não abre prazo ou a IA não soube determiná-lo.</summary>
    public int? SuggestedDeadlineDays { get; private set; }

    public DeadlineType? DeadlineType { get; private set; }

    public string? LegalBasis { get; private set; }

    /// <summary>0–100. Na sugestão final é o MENOR entre a confiança da classificação e a do prazo.</summary>
    public int Confidence { get; private set; }

    /// <summary>Confiança abaixo de <see cref="ManualReviewConfidenceThreshold"/>: "pendente de revisão manual".</summary>
    public bool RequiresManualReview { get; private set; }

    /// <summary>O texto enviado ao modelo foi cortado por tamanho: a sugestão viu só o começo.</summary>
    public bool ContentTruncated { get; private set; }

    public string AiProvider { get; private set; } = null!;

    public string AiModel { get; private set; } = null!;

    public string PromptVersion { get; private set; } = null!;

    /// <summary>JSON (jsonb) com as respostas validadas do modelo. Nunca o texto da publicação.</summary>
    public string RawResponse { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    private PublicationSuggestion()
    {
        // EF Core.
    }

    private PublicationSuggestion(PublicationSuggestionId id)
        : base(id)
    {
    }

    public static Result<PublicationSuggestion> Create(
        Guid tenantId,
        PublicationId publicationId,
        string actType,
        int? suggestedDeadlineDays,
        DeadlineType? deadlineType,
        string? legalBasis,
        int confidence,
        bool contentTruncated,
        string aiProvider,
        string aiModel,
        string promptVersion,
        string rawResponse,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId));
        }

        if (publicationId.Value == Guid.Empty)
        {
            return Failure(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(PublicationId));
        }

        if (string.IsNullOrWhiteSpace(actType))
        {
            return Failure(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(ActType));
        }

        if (confidence is < 0 or > 100)
        {
            return Failure(ErrorCode.VALIDATION_OUT_OF_RANGE, nameof(Confidence));
        }

        if (suggestedDeadlineDays is < 0 or > MaxDeadlineDays)
        {
            return Failure(ErrorCode.VALIDATION_OUT_OF_RANGE, nameof(SuggestedDeadlineDays));
        }

        // Prazo e forma de contagem andam juntos: um sem o outro não é uma sugestão utilizável.
        if (suggestedDeadlineDays.HasValue != deadlineType.HasValue)
        {
            return Failure(ErrorCode.VALIDATION_INVALID_FORMAT, nameof(DeadlineType));
        }

        if (string.IsNullOrWhiteSpace(aiProvider) || string.IsNullOrWhiteSpace(aiModel)
            || string.IsNullOrWhiteSpace(promptVersion))
        {
            return Failure(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(AiModel));
        }

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return Failure(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(RawResponse));
        }

        var suggestion = new PublicationSuggestion(PublicationSuggestionId.New())
        {
            TenantId = tenantId,
            PublicationId = publicationId,
            ActType = actType.Trim(),
            SuggestedDeadlineDays = suggestedDeadlineDays,
            DeadlineType = deadlineType,
            LegalBasis = string.IsNullOrWhiteSpace(legalBasis) ? null : legalBasis.Trim(),
            Confidence = confidence,
            RequiresManualReview = confidence < ManualReviewConfidenceThreshold,
            ContentTruncated = contentTruncated,
            AiProvider = aiProvider.Trim(),
            AiModel = aiModel.Trim(),
            PromptVersion = promptVersion.Trim(),
            RawResponse = rawResponse,
            CreatedAt = createdAt.ToUniversalTime(),
        };

        return Result.Success(suggestion);
    }

    private static Result<PublicationSuggestion> Failure(ErrorCode code, string field) =>
        Result.Failure<PublicationSuggestion>(ErrorFactory.From(code, field));
}
