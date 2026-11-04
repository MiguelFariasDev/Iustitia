namespace Advocacia.Modules.Legal.Application.Features.GetPublicationsSummary;

/// <param name="Total">Publicações capturadas para o escritório.</param>
/// <param name="PendingReview">Aguardando revisão humana — o número que importa para o prazo.</param>
/// <param name="Unread">Ainda não abertas por ninguém do escritório.</param>
public sealed record GetPublicationsSummaryResponse(
    int Total,
    int PendingReview,
    int Unread,
    int PendingToday,
    int TreatedThisWeek);
