using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Alerts;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Alerts;

/// <summary>
/// Implementa a porta <see cref="IAlertPublisher"/> sobre o PlatformDbContext (ADR-065). O
/// SaveChanges grava o alerta e a mensagem de outbox do AlertCreatedEvent na mesma transação.
/// </summary>
public sealed class AlertPublisher(
    IAlertRepository alerts,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<AlertPublisher> logger) : IAlertPublisher
{
    public async Task<bool> PublishAsync(AlertRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AlertType>(request.Type, ignoreCase: true, out var type))
        {
            type = AlertType.Other;
        }

        if (request.ReferenceId is { } referenceId
            && await alerts.ExistsAsync(request.TenantId, request.UserId, type, referenceId, cancellationToken))
        {
            return false;
        }

        var created = Alert.Create(
            request.TenantId, request.UserId, type, request.Title, request.Description, request.ReferenceId, clock.UtcNow);

        if (created.IsFailure)
        {
            logger.LogWarning("Alerta {Type} não criado: {ErrorCode}.", type, created.Error.Code);
            return false;
        }

        alerts.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
