namespace Advocacia.Modules.Workflow.Domain.Agenda;

public readonly record struct CalendarEventId(Guid Value)
{
    public static CalendarEventId New() => new(Guid.NewGuid());

    public static CalendarEventId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
