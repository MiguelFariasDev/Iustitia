namespace Advocacia.Modules.Legal.Application.Features;

/// <summary>
/// O que a IA sugeriu para a publicação — só o resultado validado, nunca o prompt, a resposta bruta nem o
/// texto da publicação. É uma SUGESTÃO: nada vira prazo sem a decisão explícita do advogado (ADR-012).
/// </summary>
/// <param name="ActType">Providência/ato sugerido (ex.: "Contestação").</param>
/// <param name="DeadlineDays">Prazo sugerido em dias; nulo quando a publicação não abre prazo ou a IA não soube determiná-lo.</param>
/// <param name="DeadlineKind">"business" (dias úteis) ou "calendar" (corridos); nulo junto com o prazo.</param>
/// <param name="LegalBasis">Fundamento legal sugerido.</param>
/// <param name="Confidence">0–100: o menor entre a confiança da classificação e a do prazo.</param>
/// <param name="RequiresManualReview">Confiança baixa: a sugestão não deve ser aceita sem conferência atenta.</param>
/// <param name="ContentTruncated">A IA viu só o começo do texto (publicação muito longa).</param>
public sealed record PublicationSuggestionResponse(
    string ActType,
    int? DeadlineDays,
    string? DeadlineKind,
    string? LegalBasis,
    int Confidence,
    bool RequiresManualReview,
    bool ContentTruncated);
