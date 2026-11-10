using System.Reflection;

namespace Advocacia.Modules.CRM.Application;

/// <summary>Marcador usado para escaneamento de assembly (MediatR, FluentValidation, Mapster).</summary>
public static class CrmApplicationAssembly
{
    public static readonly Assembly Reference = typeof(CrmApplicationAssembly).Assembly;
}
