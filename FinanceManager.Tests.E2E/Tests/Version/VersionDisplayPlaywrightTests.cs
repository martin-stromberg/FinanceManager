namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end test for the application menu footer's version display: verifies that after login
/// the footer shows the build version (or an "unknown version" fallback) rather than leaking the
/// logged-in user's internal id.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class VersionDisplayPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionDisplayPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public VersionDisplayPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that the menu footer shows the version text (or its fallback) instead of the user id after login.
    /// </summary>
    [Fact]
    public async Task Login_ShowsVersionText_InsteadOfUserId()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"version-user-{Guid.NewGuid():N}";
        const string password = "Secret123";

        var user = await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var loginStatus = page.Locator(".login-status");
        await loginStatus.WaitForAsync();
        var text = await loginStatus.InnerTextAsync();

        text.Should().NotContain(user.Id.ToString());
        text.Should().Match(t => t.Contains("Version unbekannt") || System.Text.RegularExpressions.Regex.IsMatch(t, @"\d+\.\d+\.\d+"));
    }
}
