using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.Platform.Domain.Alerts;

namespace Advocacia.Platform.Application.Features.Alerts;

public sealed record AlertResponse(
    Guid Id,
    string Type,
    string Title,
    string? Description,
    Guid? ReferenceId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt)
{
    public static AlertResponse From(Alert alert) => new(
        alert.Id.Value, alert.Type.ToString(), alert.Title, alert.Description, alert.ReferenceId,
        alert.IsRead, alert.CreatedAt, alert.ReadAt);
}

public sealed record AlertsPageResponse(IReadOnlyList<AlertResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <param name="UnreadByType">Não lidos por tipo (as abas "Importantes" e "Publicações").</param>
/// <param name="TotalByType">Todos por tipo, incluindo lidos (a aba "Outros").</param>
public sealed record AlertsSummaryResponse(
    int Total,
    int Unread,
    IReadOnlyDictionary<string, int> UnreadByType,
    IReadOnlyDictionary<string, int> TotalByType);

public sealed record ListAlertsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Type = null,
    bool? IsRead = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<AlertsPageResponse>;

public sealed record GetAlertByIdQuery(Guid AlertId) : IQuery<AlertResponse>;

public sealed record GetAlertsSummaryQuery : IQuery<AlertsSummaryResponse>;

public sealed record MarkAlertAsReadCommand(Guid AlertId) : ICommand;

public sealed record MarkAllAlertsAsReadCommand : ICommand<int>;

public sealed record DismissAlertCommand(Guid AlertId) : ICommand;
