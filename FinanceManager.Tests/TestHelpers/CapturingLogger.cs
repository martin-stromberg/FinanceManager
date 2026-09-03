using Microsoft.Extensions.Logging;

namespace FinanceManager.Tests.TestHelpers;

/// <summary>
/// In-memory <see cref="ILogger{T}"/> that records every call instead of writing anywhere, so tests can
/// assert on what a component logged (level, formatted message, structured state) without wiring up a real
/// logging provider. Prefer this over <c>NullLogger&lt;T&gt;</c> whenever a test needs to verify that a
/// warning/error was actually logged, e.g. for code paths that swallow exceptions but log them instead of
/// rethrowing.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>The log calls received so far, in call order. Inspect this after exercising the system under test.</summary>
    /// <returns>The list of captured log entries; empty if nothing has been logged yet.</returns>
    public List<CapturedLogEntry> Entries { get; } = new();

    /// <summary>No-op scope: this logger does not track scopes, so every call returns <see langword="null"/>.</summary>
    /// <typeparam name="TState">The scope state type, as required by <see cref="ILogger.BeginScope{TState}"/>.</typeparam>
    /// <param name="state">The scope state supplied by the caller; ignored.</param>
    /// <returns>Always <see langword="null"/>.</returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>Always <see langword="true"/> so that log calls at every level are captured regardless of configured minimum level.</summary>
    /// <param name="logLevel">The level being queried; ignored.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>
    /// Records the log call as a <see cref="CapturedLogEntry"/>. Structured state (e.g. from message templates
    /// with named placeholders) is flattened into a "key=value | key=value" string in <see cref="CapturedLogEntry.StateText"/>
    /// so assertions can check individual state values without depending on formatter internals.
    /// </summary>
    /// <typeparam name="TState">The type of the state object passed by the logging call site.</typeparam>
    /// <param name="logLevel">The severity of the log entry.</param>
    /// <param name="eventId">The event id associated with the log entry.</param>
    /// <param name="state">The structured or plain state object describing the entry.</param>
    /// <param name="exception">The exception associated with the entry, if any.</param>
    /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the final message text.</param>
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

/// <summary>A single call captured by <see cref="CapturingLogger{T}"/>, exposing the log level, the fully formatted message, and the flattened structured state for assertions.</summary>
/// <param name="Level">The severity the entry was logged at.</param>
/// <param name="FormattedMessage">The message text produced by the logging call's formatter.</param>
/// <param name="StateText">The structured state flattened to "key=value | key=value", or the state's <see cref="object.ToString"/> if it was not a key/value collection.</param>
/// <returns>A new immutable <see cref="CapturedLogEntry"/> holding the given level, message, and state text.</returns>
internal sealed record CapturedLogEntry(LogLevel Level, string FormattedMessage, string StateText);
