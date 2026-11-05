using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Workflow.Domain.Calendar;
using FluentAssertions;

namespace Advocacia.Modules.Workflow.Domain.UnitTests.Calendar;

public class CalendarEventTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 14, 0, 0, TimeSpan.Zero);

    private static CalendarEvent Create(DateTimeOffset? end = null, bool allDay = false) =>
        CalendarEvent.Create(
            Guid.NewGuid(), "Audiência de instrução", null, CalendarEventType.Audiencia,
            Start, end, allDay, null, null, Guid.NewGuid()).Value;

    [Fact]
    public void Create_WithoutEnd_DefaultsToOneHour()
    {
        Create().EndAt.Should().Be(Start.AddHours(1));
    }

    [Fact]
    public void Create_AllDayWithoutEnd_DefaultsToOneDay()
    {
        Create(allDay: true).EndAt.Should().Be(Start.AddDays(1));
    }

    [Fact]
    public void Create_WithEndBeforeStart_Fails()
    {
        var result = CalendarEvent.Create(
            Guid.NewGuid(), "Prazo", null, CalendarEventType.Prazo, Start, Start.AddMinutes(-1),
            false, null, null, null);

        result.Error.Code.Should().Be(nameof(ErrorCode.CALENDAR_EVENT_INVALID_PERIOD));
    }

    [Fact]
    public void Create_WithoutTitle_Fails()
    {
        var result = CalendarEvent.Create(
            Guid.NewGuid(), " ", null, CalendarEventType.Prazo, Start, null, false, null, null, null);

        result.Error.Code.Should().Be(nameof(ErrorCode.CALENDAR_EVENT_TITLE_REQUIRED));
    }

    [Fact]
    public void Reschedule_WithoutEnd_PreservesDuration()
    {
        var calendarEvent = Create(Start.AddHours(3));

        calendarEvent.Reschedule(Start.AddDays(2), null, null).IsSuccess.Should().BeTrue();

        calendarEvent.StartAt.Should().Be(Start.AddDays(2));
        calendarEvent.EndAt.Should().Be(Start.AddDays(2).AddHours(3));
    }

    [Fact]
    public void Reschedule_ToDifferentStart_ResetsReminders()
    {
        var calendarEvent = Create();
        calendarEvent.MarkReminderSent(ReminderKind.OneDayBefore, Start.AddDays(-1));
        calendarEvent.MarkReminderSent(ReminderKind.OneHourBefore, Start.AddHours(-1));

        calendarEvent.Reschedule(Start.AddDays(1), null, null);

        calendarEvent.OneDayReminderSentAt.Should().BeNull();
        calendarEvent.OneHourReminderSentAt.Should().BeNull();
    }

    [Fact]
    public void Update_KeepingSameStart_KeepsRemindersSent()
    {
        var calendarEvent = Create();
        calendarEvent.MarkReminderSent(ReminderKind.OneDayBefore, Start.AddDays(-1));

        calendarEvent.Update(
            "Novo título", null, CalendarEventType.Reuniao, Start, null, false, null, null, null);

        calendarEvent.OneDayReminderSentAt.Should().NotBeNull();
    }
}
