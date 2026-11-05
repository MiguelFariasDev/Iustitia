using Advocacia.Modules.Legal.Domain.Suggestions;

namespace Advocacia.Modules.Legal.Application.Ai;

/// <summary>De onde veio a resposta: provedor, modelo e versão do prompt que a produziu.</summary>
public sealed record AiCallMetadata(string Provider, string Model, string PromptVersion);

/// <summary>
/// Classificação já VALIDADA da publicação. <see cref="RawJson"/> é o JSON devolvido pelo
/// modelo (nunca o texto da publicação) e vai para publication_suggestions.raw_response.
/// </summary>
/// <param name="RequiresManualReview">Confiança abaixo do limiar: "pendente de revisão manual".</param>
/// <param name="ContentTruncated">O texto enviado ao modelo foi cortado por tamanho.</param>
public sealed record ClassificationResult(
    string ActType,
    int? DeadlineDays,
    DeadlineType? DeadlineType,
    string? LegalBasis,
    int Confidence,
    bool RequiresManualReview,
    bool ContentTruncated,
    AiCallMetadata Call,
    string RawJson);

/// <summary>Sugestão de prazo já VALIDADA. Mesmas garantias de <see cref="ClassificationResult"/>.</summary>
public sealed record DeadlineSuggestion(
    int? Days,
    DeadlineType? Type,
    string? LegalBasis,
    int Confidence,
    bool RequiresManualReview,
    bool ContentTruncated,
    AiCallMetadata Call,
    string RawJson);
