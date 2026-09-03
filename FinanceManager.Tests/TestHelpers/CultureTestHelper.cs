using System.Globalization;

namespace FinanceManager.Tests.TestHelpers;

/// <summary>
/// Helper for tests that assert on culture-sensitive output (number/date formatting, localized strings).
/// Test execution order and the host machine's regional settings are otherwise uncontrolled, so any test
/// relying on a specific format must pin the thread culture explicitly via <see cref="WithInvariantCulture"/>
/// rather than assuming the ambient culture.
/// </summary>
internal static class CultureTestHelper
{
    /// <summary>
    /// Runs <paramref name="action"/> with the thread culture pinned to invariant so localized text and
    /// number formatting assertions are deterministic regardless of the environment's default culture.
    /// </summary>
    /// <param name="action">The test body to execute under the pinned culture.</param>
    public static void WithInvariantCulture(Action action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
