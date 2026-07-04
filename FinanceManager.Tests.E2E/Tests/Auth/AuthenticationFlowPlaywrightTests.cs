namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class AuthenticationFlowPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public AuthenticationFlowPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies registration, login and logout through the browser UI.
    /// </summary>
    [Fact]
    public async Task Register_Login_Logout_Flow_ShouldWork()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"ui-user-{Guid.NewGuid():N}";
        const string password = "Secret123";

        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        page.Url.Should().EndWith("/");

        await auth.LogoutAsync();
        page.Url.Should().EndWith("/login");

        await auth.LoginAsync(username, password);
        page.Url.Should().EndWith("/");
    }
}
