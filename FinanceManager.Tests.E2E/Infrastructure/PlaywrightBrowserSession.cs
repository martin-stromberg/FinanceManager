namespace FinanceManager.Tests.E2E;

public sealed class PlaywrightBrowserSession : IAsyncDisposable
{
    private readonly IBrowserContext _context;

    public PlaywrightBrowserSession(IBrowserContext context, IPage page)
    {
        _context = context;
        Page = page;
    }

    public IPage Page { get; }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
