namespace FinanceManager.Tests.E2E;

public sealed class PlaywrightBrowserSession : IAsyncDisposable
{
    private readonly IBrowserContext _context;
    private readonly string? _artifactPrefix;
    private readonly bool _artifactCaptureEnabled;
    private readonly bool _traceEnabled;
    private readonly List<string> _browserMessages = new();

    public PlaywrightBrowserSession(IBrowserContext context, IPage page, string? artifactPrefix, bool artifactCaptureEnabled, bool traceEnabled)
    {
        _context = context;
        _artifactPrefix = artifactPrefix;
        _artifactCaptureEnabled = artifactCaptureEnabled;
        _traceEnabled = traceEnabled;
        Page = page;

        if (_artifactCaptureEnabled)
        {
            Page.Console += (_, message) => _browserMessages.Add($"console.{message.Type}: {message.Text}");
            Page.PageError += (_, error) => _browserMessages.Add($"pageerror: {error}");
        }
    }

    public IPage Page { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_artifactCaptureEnabled && _artifactPrefix != null)
            {
                await Page.ScreenshotAsync(new()
                {
                    Path = $"{_artifactPrefix}.png",
                    FullPage = true
                });

                var html = await Page.ContentAsync();
                await File.WriteAllTextAsync($"{_artifactPrefix}.html", html);

                if (_browserMessages.Count > 0)
                {
                    await File.WriteAllLinesAsync($"{_artifactPrefix}.browser.log", _browserMessages);
                }
            }

            if (_traceEnabled && _artifactPrefix != null)
            {
                await _context.Tracing.StopAsync(new() { Path = $"{_artifactPrefix}.zip" });
            }
        }
        catch
        {
            // Artifact capture must not hide the actual test failure.
        }

        await _context.DisposeAsync();
    }
}
