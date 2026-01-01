namespace FinanceManager.Application;

/// <summary>
/// Production implementation of <see cref="IDateTimeProvider"/> that returns system UTC time.
/// Use this implementation for runtime; tests can replace it with a deterministic provider.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time from the system clock.
    /// </summary>
    /// <value>The current <see cref="DateTime"/> in UTC.</value>
    public DateTime UtcNow => DateTime.UtcNow;
}
