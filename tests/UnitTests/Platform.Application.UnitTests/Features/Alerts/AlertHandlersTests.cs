using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Alerts;
using Advocacia.Platform.Domain.Alerts;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Alerts;

public class AlertHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly IAlertRepository _alerts = Substitute.For<IAlertRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public AlertHandlersTests()
    {
        _currentUser.TenantId.Returns(_tenantId);
        _currentUser.UserId.Returns(_userId);
        _clock.UtcNow.Returns(Now);
    }

    private Alert NewAlert() =>
        Alert.Create(_tenantId, _userId, AlertType.Publication, "Nova publicação", null, Guid.NewGuid(), Now).Value;

    [Fact]
    public async Task List_WithoutUserInContext_Fails()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var result = await new ListAlertsHandler(_alerts, _currentUser).Handle(new ListAlertsQuery(), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
    }

    [Fact]
    public async Task List_ScopesTheQueryToTenantAndUser_AndPages()
    {
        var alerts = new[] { NewAlert(), NewAlert() };
        _alerts.ListAsync(_tenantId, _userId, Arg.Any<AlertFilter>(), 20, 20, Arg.Any<CancellationToken>()).Returns(alerts);
        _alerts.CountAsync(_tenantId, _userId, Arg.Any<AlertFilter>(), Arg.Any<CancellationToken>()).Returns(42);

        var result = await new ListAlertsHandler(_alerts, _currentUser)
            .Handle(new ListAlertsQuery(Page: 2, PageSize: 20, Type: "publication", IsRead: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(42);
        result.Value.TotalPages.Should().Be(3);
        await _alerts.Received(1).ListAsync(
            _tenantId, _userId, Arg.Is<AlertFilter>(f => f.Types!.Single() == AlertType.Publication && f.IsRead == false),
            20, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_WithUnknownType_Fails()
    {
        var result = await new ListAlertsHandler(_alerts, _currentUser)
            .Handle(new ListAlertsQuery(Type: "nope"), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_INVALID_ENUM));
    }

    [Fact]
    public async Task List_AcceptsSeveralTypesSeparatedByComma()
    {
        _alerts.ListAsync(_tenantId, _userId, Arg.Any<AlertFilter>(), 0, 20, Arg.Any<CancellationToken>()).Returns([]);

        await new ListAlertsHandler(_alerts, _currentUser)
            .Handle(new ListAlertsQuery(Type: "Task, Birthday,Other"), CancellationToken.None);

        await _alerts.Received(1).ListAsync(
            _tenantId, _userId,
            Arg.Is<AlertFilter>(f => f.Types!.SequenceEqual(new[] { AlertType.Task, AlertType.Birthday, AlertType.Other })),
            0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_OfAnotherUsersAlert_IsNotFound()
    {
        _alerts.GetForUserAsync(_tenantId, _userId, Arg.Any<AlertId>(), Arg.Any<CancellationToken>()).Returns((Alert?)null);

        var result = await new GetAlertByIdHandler(_alerts, _currentUser).Handle(new GetAlertByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.ALERT_NOT_FOUND));
    }

    [Fact]
    public async Task Summary_SumsUnreadAcrossTypes()
    {
        _alerts.CountAsync(_tenantId, _userId, Arg.Any<AlertFilter>(), Arg.Any<CancellationToken>()).Returns(10);
        _alerts.CountUnreadByTypeAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<AlertType, int> { [AlertType.Publication] = 3, [AlertType.Task] = 1 });
        _alerts.CountTotalByTypeAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<AlertType, int> { [AlertType.Publication] = 8, [AlertType.Task] = 2 });

        var result = await new GetAlertsSummaryHandler(_alerts, _currentUser).Handle(new GetAlertsSummaryQuery(), CancellationToken.None);

        result.Value.Total.Should().Be(10);
        result.Value.Unread.Should().Be(4);
        result.Value.UnreadByType["Publication"].Should().Be(3);
        result.Value.TotalByType["Publication"].Should().Be(8);
    }

    [Fact]
    public async Task MarkAsRead_PersistsAndSavesOnce()
    {
        var alert = NewAlert();
        _alerts.GetForUserAsync(_tenantId, _userId, alert.Id, Arg.Any<CancellationToken>()).Returns(alert);

        var result = await new MarkAlertAsReadHandler(_alerts, _unitOfWork, _currentUser, _clock)
            .Handle(new MarkAlertAsReadCommand(alert.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        alert.IsRead.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsRead_WhenAlreadyRead_FailsWithoutSaving()
    {
        var alert = NewAlert();
        alert.MarkAsRead(Now);
        _alerts.GetForUserAsync(_tenantId, _userId, alert.Id, Arg.Any<CancellationToken>()).Returns(alert);

        var result = await new MarkAlertAsReadHandler(_alerts, _unitOfWork, _currentUser, _clock)
            .Handle(new MarkAlertAsReadCommand(alert.Id.Value), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.ALERT_ALREADY_READ));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dismiss_HidesTheAlert()
    {
        var alert = NewAlert();
        _alerts.GetForUserAsync(_tenantId, _userId, alert.Id, Arg.Any<CancellationToken>()).Returns(alert);

        var result = await new DismissAlertHandler(_alerts, _unitOfWork, _currentUser, _clock)
            .Handle(new DismissAlertCommand(alert.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        alert.DismissedAt.Should().Be(Now);
    }

    [Fact]
    public async Task MarkAll_ReturnsHowManyChanged()
    {
        _alerts.MarkAllAsReadAsync(_tenantId, _userId, Now, Arg.Any<CancellationToken>()).Returns(7);

        var result = await new MarkAllAlertsAsReadHandler(_alerts, _currentUser, _clock)
            .Handle(new MarkAllAlertsAsReadCommand(), CancellationToken.None);

        result.Value.Should().Be(7);
    }
}
