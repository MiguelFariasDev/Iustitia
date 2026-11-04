using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Domain.Alerts.Events;

namespace Advocacia.Platform.Domain.Alerts;

/// <summary>
/// Alerta in-app endereçado a UM usuário (o sino e o painel de alertas). Genérico de
/// propósito — publicação nova hoje; tarefa e aniversário depois — por isso mora em Platform e
/// os módulos de negócio o criam pela porta <c>IAlertPublisher</c> (ver ADR-065), nunca
/// referenciando Platform diretamente.
///
/// Descartar não apaga: <see cref="DismissedAt"/> esconde o alerta das listagens, mas o
/// registro permanece para auditoria.
/// </summary>
public sealed class Alert : AggregateRoot<AlertId>, IHasTenant
{
    public const int TitleMaxLength = 200;

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public AlertType Type { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>Entidade que originou o alerta (ex.: o id da publicação), para o "ver detalhe".</summary>
    public Guid? ReferenceId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset? DismissedAt { get; private set; }

    private Alert()
    {
        // EF Core.
    }

    private Alert(
        AlertId id, Guid tenantId, Guid userId, AlertType type, string title, string? description,
        Guid? referenceId, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Type = type;
        Title = title;
        Description = description;
        ReferenceId = referenceId;
        CreatedAt = createdAt;
    }

    public static Result<Alert> Create(
        Guid tenantId,
        Guid userId,
        AlertType type,
        string title,
        string? description,
        Guid? referenceId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Alert>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (userId == Guid.Empty)
        {
            return Result.Failure<Alert>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(UserId)));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Alert>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(Title)));
        }

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > TitleMaxLength)
        {
            trimmedTitle = trimmedTitle[..TitleMaxLength];
        }

        var alert = new Alert(
            AlertId.New(),
            tenantId,
            userId,
            type,
            trimmedTitle,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            referenceId,
            now.ToUniversalTime());

        alert.RaiseDomainEvent(new AlertCreatedEvent(
            alert.Id.Value, tenantId, userId, type, alert.Title, alert.Description, referenceId, alert.CreatedAt));

        return Result.Success(alert);
    }

    public Result MarkAsRead(DateTimeOffset now)
    {
        if (DismissedAt is not null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ALERT_ALREADY_DISMISSED));
        }

        if (IsRead)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ALERT_ALREADY_READ));
        }

        IsRead = true;
        ReadAt = now.ToUniversalTime();

        RaiseDomainEvent(new AlertReadEvent(Id.Value, TenantId, UserId));

        return Result.Success();
    }

    /// <summary>Descarta o alerta. Descartar um alerta não lido também o conta como lido.</summary>
    public Result Dismiss(DateTimeOffset now)
    {
        if (DismissedAt is not null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ALERT_ALREADY_DISMISSED));
        }

        var utcNow = now.ToUniversalTime();

        if (!IsRead)
        {
            IsRead = true;
            ReadAt = utcNow;
        }

        DismissedAt = utcNow;

        return Result.Success();
    }
}
