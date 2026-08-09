namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class UpdateSetupPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public UpdateSetupPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Admin öffnet Setup → Update-Tab und sieht Status und Einstellungen aus der neuen Bibliothek.
    /// </summary>
    [Fact]
    public async Task Admin_OpensUpdateTab_ShowsStatus()
    {
        var (session, gateway) = await LoginAsAdminAndOpenUpdateTabAsync();
        await using var _ = session;

        var status = await gateway.GetStatusValueAsync();
        status.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Admin löst die Prüfung manuell aus und erhält ein Ergebnis (lokale Ordnerquelle mit bereitgestellter neuer Version).
    /// </summary>
    [Fact]
    public async Task Admin_TriggersCheck_ShowsAvailableUpdate()
    {
        var (session, gateway) = await LoginAsAdminAndOpenUpdateTabAsync();
        await using var _ = session;

        await gateway.SetEnabledAsync(true);
        await gateway.AllowChecksAnyTimeAsync();
        await gateway.SaveSettingsAsync();
        await gateway.CheckNowAsync();

        await gateway.WaitForAvailableVersionAsync(PlaywrightWebAppFixture.AvailableUpdateVersion);
    }

    /// <summary>
    /// Admin speichert geänderte Update-Einstellungen und sieht sie nach dem Neuladen.
    /// </summary>
    [Fact]
    public async Task Admin_SavesSettings_PersistsAcrossReload()
    {
        var (session, gateway) = await LoginAsAdminAndOpenUpdateTabAsync();
        await using var _ = session;
        var page = session.Page;

        await gateway.SetEnabledAsync(false);
        await gateway.SaveSettingsAsync();

        await page.ReloadAsync();
        await gateway.OpenAsync();

        (await gateway.IsEnabledAsync()).Should().BeFalse();
    }

    /// <summary>
    /// Creates a browser session, seeds and logs in as a fresh admin user, and opens the setup update tab.
    /// Shared by all tests in this class to avoid repeating the session/seeder/login boilerplate.
    /// </summary>
    /// <returns>The browser session (caller-owned, must be disposed) and a gateway for the already-opened update tab.</returns>
    private async Task<(PlaywrightBrowserSession Session, SetupUpdateGateway Gateway)> LoginAsAdminAndOpenUpdateTabAsync()
    {
        var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        var username = $"update-admin-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password, isAdmin: true);
        await auth.LoginAsync(username, password);

        var gateway = new SetupUpdateGateway(page);
        await gateway.OpenAsync();

        return (session, gateway);
    }
}
