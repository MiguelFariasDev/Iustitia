using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Application.Abstractions;

namespace Advocacia.Modules.Legal.Application.Features.GetPublicationsSummary;

/// <summary>
/// Contadores do topo da tela de publicações, numa única agregação com contagens
/// condicionais (ver PublicationRepository.GetCountersAsync).
/// </summary>
public sealed class GetPublicationsSummaryHandler(
    IPublicationRepository publicationRepository,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetPublicationsSummaryQuery, GetPublicationsSummaryResponse>
{
    private static readonly TimeSpan BrasiliaOffset = TimeSpan.FromHours(-3);

    public async Task<Result<GetPublicationsSummaryResponse>> Handle(
        GetPublicationsSummaryQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<GetPublicationsSummaryResponse>(
                ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        // Hoje e início da semana (segunda-feira) no fuso do escritório. O Brasil não tem horário
        // de verão desde 2019, então -03:00 fixo é correto; PublishedOn é a data de divulgação.
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.ToOffset(BrasiliaOffset).DateTime);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        var counters = await publicationRepository.GetCountersAsync(tenantId, today, weekStart, cancellationToken);

        return Result.Success(new GetPublicationsSummaryResponse(
            counters.Total, counters.PendingReview, counters.Unread, counters.PendingToday, counters.TreatedThisWeek));
    }
}
