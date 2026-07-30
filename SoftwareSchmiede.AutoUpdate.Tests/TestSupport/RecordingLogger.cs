using Microsoft.Extensions.Logging;

namespace SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

/// <summary>
/// Minimal <see cref="ILogger"/> implementation recording every formatted log message, used to assert that a
/// production code path logged something without depending on a real logging provider.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    public List<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Messages.Add(formatter(state, exception));
}
