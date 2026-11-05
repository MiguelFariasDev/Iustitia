using System.Reflection;

namespace Advocacia.Modules.Workflow.Application;

/// <summary>Marcador usado para escaneamento de assembly (MediatR, FluentValidation, Mapster).</summary>
public static class WorkflowApplicationAssembly
{
    public static readonly Assembly Reference = typeof(WorkflowApplicationAssembly).Assembly;
}
