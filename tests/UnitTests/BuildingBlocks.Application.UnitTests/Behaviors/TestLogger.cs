using Microsoft.Extensions.Logging;

namespace Advocacia.BuildingBlocks.Application.UnitTests.Behaviors;

/// <summary>
/// Fake mínimo de <see cref="ILogger{T}"/> para os testes de behaviors — NSubstitute não
/// consegue verificar chamadas aos métodos de extensão (LogInformation/LogWarning/...) de
/// forma confiável, porque eles delegam para o método genérico Log&lt;TState&gt;.
/// </summary>
public sealed class TestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
