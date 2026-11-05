using System.Reflection;

namespace Advocacia.Modules.Atendimentos.Application;

/// <summary>Marcador usado para escaneamento de assembly (MediatR, FluentValidation).</summary>
public static class AtendimentoApplicationAssembly
{
    public static readonly Assembly Reference = typeof(AtendimentoApplicationAssembly).Assembly;
}
