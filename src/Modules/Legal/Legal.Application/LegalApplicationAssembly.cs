using System.Reflection;

namespace Advocacia.Modules.Legal.Application;

/// <summary>Marcador usado para escaneamento de assembly (MediatR, FluentValidation, Mapster).</summary>
public static class LegalApplicationAssembly
{
    public static readonly Assembly Reference = typeof(LegalApplicationAssembly).Assembly;
}
