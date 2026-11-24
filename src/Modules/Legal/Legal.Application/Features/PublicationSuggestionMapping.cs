using Advocacia.Modules.Legal.Domain.Suggestions;

namespace Advocacia.Modules.Legal.Application.Features;

internal static class PublicationSuggestionMapping
{
    internal static PublicationSuggestionResponse ToSummary(this PublicationSuggestion suggestion) => new(
        suggestion.ActType,
        suggestion.SuggestedDeadlineDays,
        suggestion.SuggestedDeadlineDays is null ? null : suggestion.DeadlineType?.ToValue(),
        suggestion.LegalBasis,
        suggestion.Confidence,
        suggestion.RequiresManualReview,
        suggestion.ContentTruncated);
}
