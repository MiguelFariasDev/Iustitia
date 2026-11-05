namespace Advocacia.Modules.Workflow.Domain.WorkTasks;

public readonly record struct WorkTaskId(Guid Value)
{
    public static WorkTaskId New() => new(Guid.NewGuid());

    public static WorkTaskId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
