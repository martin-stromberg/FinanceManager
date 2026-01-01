namespace FinanceManager.Application;

/// <summary>
/// Provides the current system time in UTC. Abstraction useful for testing.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current UTC date/time.
    /// </summary>
    DateTime UtcNow { get; }
}
