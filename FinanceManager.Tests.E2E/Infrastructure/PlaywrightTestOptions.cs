namespace FinanceManager.Tests.E2E;

public sealed class PlaywrightTestOptions
{
    public string BrowserChannel { get; init; } = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSER_CHANNEL") ?? "msedge";

    public bool Headless { get; init; } = !string.Equals(Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADED"), "true", StringComparison.OrdinalIgnoreCase);

    public int ActionTimeoutSeconds { get; init; } = 10;

    public int NavigationTimeoutSeconds { get; init; } = 30;
}
