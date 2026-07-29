namespace SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

/// <summary>
/// Shared polling helper for tests waiting on an asynchronous condition (e.g. a background service invocation),
/// used instead of a fixed <c>Task.Delay</c> so tests fail fast when the condition is met early and still allow
/// enough time under load before timing out.
/// </summary>
internal static class AsyncTestWait
{
    /// <summary>
    /// Polls <paramref name="condition"/> every 10ms until it returns <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition to poll.</param>
    /// <param name="timeoutMs">The maximum time, in milliseconds, to wait.</param>
    /// <returns>A task that completes once the condition is met.</returns>
    /// <exception cref="TimeoutException">Thrown when <paramref name="condition"/> did not become <see langword="true"/> within <paramref name="timeoutMs"/>.</exception>
    public static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Condition was not met within {timeoutMs}ms.");
            }

            await Task.Delay(10);
        }
    }
}
