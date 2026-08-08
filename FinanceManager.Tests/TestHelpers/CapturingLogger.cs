using Microsoft.Extensions.Logging;

namespace FinanceManager.Tests.TestHelpers;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<CapturedLogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var stateText = state is IEnumerable<KeyValuePair<string, object?>> values
            ? string.Join(" | ", values.Select(v => $"{v.Key}={v.Value}"))
            : state?.ToString() ?? string.Empty;

        Entries.Add(new CapturedLogEntry(logLevel, formatter(state, exception), stateText));
    }
}

internal sealed record CapturedLogEntry(LogLevel Level, string FormattedMessage, string StateText);
