using System.Text.RegularExpressions;

namespace FinanceManager.Tests.E2E;

/// <summary>Runs expiry scenarios against the regular application with production-compatible timing.</summary>
public sealed class SessionExpiryWebAppFixture : IAsyncLifetime
{
    private readonly PlaywrightWebAppFixture _application;

    /// <summary>Creates an isolated regular web server with proportional renewal timing for shortened runs.</summary>
    public SessionExpiryWebAppFixture()
    {
        LifetimeMinutes = ResolveLifetimeMinutes(Environment.GetEnvironmentVariable("E2E_SESSION_LIFETIME_MINUTES"));
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        ObservationPath = Path.Combine(root, "TestResults", "E2E", $"session-api-{Guid.NewGuid():N}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(ObservationPath)!);
        _application = new PlaywrightWebAppFixture(new Dictionary<string, string>
        {
            ["Jwt__LifetimeMinutes"] = LifetimeMinutes.ToString(),
            ["Jwt__MinimumRenewalWindowMinutes"] = ResolveMinimumRenewalWindowMinutes(LifetimeMinutes).ToString(),
            ["Logging__LogLevel__FinanceManager.Web.Infrastructure.RequestLoggingMiddleware"] = "Debug",
            ["Logging__Console__FormatterOptions__TimestampFormat"] = "yyyy-MM-ddTHH:mm:ss.fffZ "
        });
    }

    internal static int ResolveLifetimeMinutes(string? configuredLifetime)
    {
        var minutes = int.TryParse(configuredLifetime, out var value) ? value : 3;
        if (minutes < 3) throw new InvalidOperationException("Session expiry scenarios require at least three minutes to preserve the sixty-second activity throttle.");
        return minutes;
    }

    internal static int ResolveMinimumRenewalWindowMinutes(int lifetimeMinutes) => lifetimeMinutes < 10 ? 1 : 5;

    /// <summary>Gets the configured real token lifetime.</summary>
    public int LifetimeMinutes { get; }
    /// <summary>Gets the application URL.</summary>
    public string BaseUrl => _application.BaseUrl;
    /// <summary>Gets the isolated database.</summary>
    public string DatabasePath => _application.DatabasePath;
    /// <summary>Gets the redacted regular server request log.</summary>
    public string ObservationPath { get; }
    /// <inheritdoc />
    public ValueTask InitializeAsync() => _application.InitializeAsync();
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        File.WriteAllText(ObservationPath, _application.GetServerLogs());
        await _application.DisposeAsync();
    }
    /// <summary>Creates a clean browser session.</summary>
    /// <returns>A clean session against the isolated application.</returns>
    public Task<PlaywrightBrowserSession> CreateSessionAsync() => _application.CreateSessionAsync();

    /// <summary>Reads actual server API status logs, including circuit calls invisible to browser network events.</summary>
    /// <returns>Completed API responses in their observed completion order.</returns>
    public IReadOnlyList<ApiObservation> ReadObservations()
    {
        return Regex.Matches(_application.GetServerLogs(), @"HTTP (?<method>\w+) (?<path>/api/\S*) responded (?<status>\d+)")
            .Select((match, index) => new ApiObservation(index + 1, match.Groups["method"].Value,
                match.Groups["path"].Value.Split('?')[0], int.Parse(match.Groups["status"].Value))).ToArray();
    }
}

/// <summary>Actual regular-server response metadata.</summary>
/// <param name="Sequence">Completion sequence.</param>
/// <param name="Method">HTTP method.</param>
/// <param name="Path">API path.</param>
/// <param name="Status">Response status.</param>
/// <returns>The observed server response metadata.</returns>
public sealed record ApiObservation(long Sequence, string Method, string Path, int Status);

/// <summary>Serializes timing tests on their isolated regular host.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SessionExpiryCollection : ICollectionFixture<SessionExpiryWebAppFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "SessionExpiry";
}
