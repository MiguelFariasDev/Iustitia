using System.Reflection;

namespace Advocacia.Platform.Application;

/// <summary>Marcador usado para escaneamento de assembly (MediatR, FluentValidation) — ver Platform.Api.</summary>
public static class PlatformApplicationAssembly
{
    public static readonly Assembly Reference = typeof(PlatformApplicationAssembly).Assembly;
}
