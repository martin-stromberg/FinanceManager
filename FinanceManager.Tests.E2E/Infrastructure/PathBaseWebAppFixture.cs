namespace FinanceManager.Tests.E2E;

/// <summary>
/// Runs the reverse-proxy subfolder (PathBase) smoke scenarios (#612) against an isolated server
/// started with <c>PathBase=/finance</c>, so prefixed navigation is exercised end to end without
/// changing the shared E2E fixture's root hosting.
/// </summary>
public sealed class PathBaseWebAppFixture : IAsyncLifetime
{
    /// <summary>The PathBase prefix the isolated server is configured with.</summary>
    public const string PathBasePrefix = "/finance";

    private readonly PlaywrightWebAppFixture _application;

    /// <summary>Creates an isolated web server hosted under <see cref="PathBasePrefix"/>.</summary>
    public PathBaseWebAppFixture()
    {
        _application = new PlaywrightWebAppFixture(new Dictionary<string, string>
        {
            ["PathBase"] = PathBasePrefix
        });
    }

    /// <summary>Gets the application URL (without the PathBase prefix).</summary>
    public string BaseUrl => _application.BaseUrl;

    /// <summary>Gets the isolated database path used to seed users.</summary>
    public string DatabasePath => _application.DatabasePath;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => _application.InitializeAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _application.DisposeAsync();

    /// <summary>Creates a clean browser session.</summary>
    /// <returns>A clean session against the isolated application.</returns>
    public Task<PlaywrightBrowserSession> CreateSessionAsync() => _application.CreateSessionAsync();
}

/// <summary>Serializes PathBase smoke tests on their isolated host.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PathBaseCollection : ICollectionFixture<PathBaseWebAppFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "PathBase";
}
