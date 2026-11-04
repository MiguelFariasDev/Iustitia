using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Platform.Domain.Alerts;
using Advocacia.Platform.Domain.Alerts.Events;
using FluentAssertions;

namespace Advocacia.Platform.Domain.UnitTests.Alerts;

public class AlertTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private Alert NewAlert() =>
        Alert.Create(_tenantId, _userId, AlertType.Publication, "Nova publicação", "Processo X", Guid.NewGuid(), Now).Value;

    [Fact]
    public void Create_StartsUnreadAndRaisesAlertCreated()
    {
        var referenceId = Guid.NewGuid();

        var alert = Alert.Create(_tenantId, _userId, AlertType.Publication, "  Nova publicação  ", " desc ", referenceId, Now).Value;

        alert.IsRead.Should().BeFalse();
        alert.ReadAt.Should().BeNull();
        alert.DismissedAt.Should().BeNull();
        alert.Title.Should().Be("Nova publicação");
        alert.Description.Should().Be("desc");
        var created = alert.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AlertCreatedEvent>().Subject;
        created.AlertId.Should().Be(alert.Id.Value);
        created.UserId.Should().Be(_userId);
        created.ReferenceId.Should().Be(referenceId);
    }

    [Theory]
    [InlineData("", "title")]
    [InlineData("  ", "title")]
    public void Create_WithBlankTitle_Fails(string title, string _)
    {
        var result = Alert.Create(_tenantId, _userId, AlertType.Other, title, null, null, Now);

        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_REQUIRED_FIELD));
    }

    [Fact]
    public void Create_WithEmptyTenantOrUser_Fails()
    {
        Alert.Create(Guid.Empty, _userId, AlertType.Other, "t", null, null, Now).IsFailure.Should().BeTrue();
        Alert.Create(_tenantId, Guid.Empty, AlertType.Other, "t", null, null, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_TruncatesAnOverlongTitle()
    {
        var alert = Alert.Create(_tenantId, _userId, AlertType.Other, new string('x', 500), null, null, Now).Value;

        alert.Title.Should().HaveLength(Alert.TitleMaxLength);
    }

    [Fact]
    public void MarkAsRead_SetsReadAtAndRaisesEvent()
    {
        var alert = NewAlert();

        var result = alert.MarkAsRead(Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        alert.IsRead.Should().BeTrue();
        alert.ReadAt.Should().Be(Now.AddMinutes(5));
        alert.DomainEvents.Should().ContainSingle(e => e is AlertReadEvent);
    }

    [Fact]
    public void MarkAsRead_Twice_Fails()
    {
        var alert = NewAlert();
        alert.MarkAsRead(Now);

        alert.MarkAsRead(Now).Error.Code.Should().Be(nameof(ErrorCode.ALERT_ALREADY_READ));
    }

    [Fact]
    public void Dismiss_OnAnUnreadAlert_AlsoMarksItRead()
    {
        var alert = NewAlert();

        alert.Dismiss(Now).IsSuccess.Should().BeTrue();

        alert.DismissedAt.Should().Be(Now);
        alert.IsRead.Should().BeTrue();
    }

    [Fact]
    public void Dismiss_Twice_Fails()
    {
        var alert = NewAlert();
        alert.Dismiss(Now);

        alert.Dismiss(Now).Error.Code.Should().Be(nameof(ErrorCode.ALERT_ALREADY_DISMISSED));
    }

    [Fact]
    public void MarkAsRead_AfterDismiss_Fails()
    {
        var alert = NewAlert();
        alert.Dismiss(Now);

        alert.MarkAsRead(Now).Error.Code.Should().Be(nameof(ErrorCode.ALERT_ALREADY_DISMISSED));
    }
}
