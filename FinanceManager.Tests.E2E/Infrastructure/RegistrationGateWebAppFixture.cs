namespace FinanceManager.Tests.E2E;

/// <summary>
/// Runs the self-registration gate scenarios (#456) against an isolated server started with
/// <c>Auth:AllowSelfRegistration=false</c>, so the closed-by-default registration behavior is
/// exercised end to end without weakening the shared E2E fixture's open-registration setting.
/// </summary>
public sealed class RegistrationGateWebAppFixture : IAsyncLifetime
{
    private readonly PlaywrightWebAppFixture _application;

    /// <summary>Creates an isolated web server with self-registration disabled.</summary>
    public RegistrationGateWebAppFixture()
    {
        _application = new PlaywrightWebAppFixture(new Dictionary<string, string>
        {
            ["Auth__AllowSelfRegistration"] = "false"
        });
    }

    /// <summary>Gets the application URL.</summary>
    public string BaseUrl => _application.BaseUrl;

    /// <summary>Gets the isolated database path used to seed pre-existing users.</summary>
    public string DatabasePath => _application.DatabasePath;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => _application.InitializeAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _application.DisposeAsync();

    /// <summary>Creates a clean browser session.</summary>
    /// <returns>A clean session against the isolated application.</returns>
    public Task<PlaywrightBrowserSession> CreateSessionAsync() => _application.CreateSessionAsync();

    /// <summary>Returns the captured server output for diagnostics.</summary>
    /// <returns>The captured server logs.</returns>
    internal string GetServerLogs() => _application.GetServerLogs();
}

/// <summary>Serializes registration-gate tests on their isolated host.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RegistrationGateCollection : ICollectionFixture<RegistrationGateWebAppFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "RegistrationGate";
}
