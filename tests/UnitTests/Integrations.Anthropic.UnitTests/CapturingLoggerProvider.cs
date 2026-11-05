using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Integrations.Anthropic.UnitTests;

/// <summary>Guarda tudo que foi logado (mensagem + exceção) para asserções de "nada sigiloso vazou".</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public string Text => string.Join('\n', _lines);

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lines.Enqueue(formatter(state, exception));

            if (exception is not null)
            {
                lines.Enqueue(exception.ToString());
            }
        }
    }
}
