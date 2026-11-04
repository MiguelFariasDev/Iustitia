using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts;

namespace Advocacia.Platform.Application.Features.Alerts;

public sealed class ListAlertsHandler(IAlertRepository alerts, ICurrentUser currentUser)
    : IQueryHandler<ListAlertsQuery, AlertsPageResponse>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<AlertsPageResponse>> Handle(ListAlertsQuery request, CancellationToken cancellationToken)
    {
        if (!AlertScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<AlertsPageResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        // Aceita um ou mais tipos separados por vírgula (ex.: "Task,Birthday,Other").
        var types = new List<AlertType>();
        foreach (var part in (request.Type ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<AlertType>(part, ignoreCase: true, out var parsed))
            {
                return Result.Failure<AlertsPageResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
            }

            types.Add(parsed);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;
        var filter = new AlertFilter(types, request.IsRead, request.From, request.To);

        var items = await alerts.ListAsync(tenantId, userId, filter, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await alerts.CountAsync(tenantId, userId, filter, cancellationToken);

        return Result.Success(new AlertsPageResponse(
            items.Select(AlertResponse.From).ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }
}

public sealed class GetAlertByIdHandler(IAlertRepository alerts, ICurrentUser currentUser)
    : IQueryHandler<GetAlertByIdQuery, AlertResponse>
{
    public async Task<Result<AlertResponse>> Handle(GetAlertByIdQuery request, CancellationToken cancellationToken)
    {
        if (!AlertScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<AlertResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var alert = await alerts.GetForUserAsync(tenantId, userId, AlertId.From(request.AlertId), cancellationToken);

        return alert is null
            ? Result.Failure<AlertResponse>(ErrorFactory.From(ErrorCode.ALERT_NOT_FOUND))
            : Result.Success(AlertResponse.From(alert));
    }
}

public sealed class GetAlertsSummaryHandler(IAlertRepository alerts, ICurrentUser currentUser)
    : IQueryHandler<GetAlertsSummaryQuery, AlertsSummaryResponse>
{
    public async Task<Result<AlertsSummaryResponse>> Handle(GetAlertsSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!AlertScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<AlertsSummaryResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var total = await alerts.CountAsync(tenantId, userId, new AlertFilter(), cancellationToken);
        var unreadByType = await alerts.CountUnreadByTypeAsync(tenantId, userId, cancellationToken);
        var totalByType = await alerts.CountTotalByTypeAsync(tenantId, userId, cancellationToken);

        return Result.Success(new AlertsSummaryResponse(
            total,
            unreadByType.Values.Sum(),
            unreadByType.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            totalByType.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value)));
    }
}

public sealed class MarkAlertAsReadHandler(
    IAlertRepository alerts, IUnitOfWork unitOfWork, ICurrentUser currentUser, IDateTimeProvider clock)
    : ICommandHandler<MarkAlertAsReadCommand>
{
    public async Task<Result> Handle(MarkAlertAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!AlertScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var alert = await alerts.GetForUserAsync(tenantId, userId, AlertId.From(request.AlertId), cancellationToken);
        if (alert is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ALERT_NOT_FOUND));
        }

        var result = alert.MarkAsRead(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        alerts.Update(alert);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class MarkAllAlertsAsReadHandler(
    IAlertRepository alerts, ICurrentUser currentUser, IDateTimeProvider clock)
    : ICommandHandler<MarkAllAlertsAsReadCommand, int>
{
    public async Task<Result<int>> Handle(MarkAllAlertsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!AlertScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<int>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        return Result.Success(await alerts.MarkAllAsReadAsync(tenantId, userId, clock.UtcNow, cancellationToken));
    }
}

public sealed class DismissAlertHandler(
    IAlertRepository alerts, IUnitOfWork unitOfWork, ICurrentUser currentUser, IDateTimeProvider clock)
    : ICommandHandler<DismissAlertCommand>
{
    public async Task<Result> Handle(DismissAlertCommand request, CancellationToken cancellationToken)
    {
        if (!AlertScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var alert = await alerts.GetForUserAsync(tenantId, userId, AlertId.From(request.AlertId), cancellationToken);
        if (alert is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ALERT_NOT_FOUND));
        }

        var result = alert.Dismiss(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        alerts.Update(alert);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
