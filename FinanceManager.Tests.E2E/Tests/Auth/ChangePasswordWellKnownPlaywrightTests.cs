namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the self-service password change and the W3C well-known discovery URL:
/// the anonymous <c>/.well-known/change-password</c> redirect that routes through the login page
/// back to the change-password page, the authenticated UI flow, and the wrong-current-password
/// error path.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ChangePasswordWellKnownPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordWellKnownPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ChangePasswordWellKnownPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that an anonymous request to <c>/.well-known/change-password</c> lands on the login
    /// page with a return URL and, after signing in, ends up on the change-password page — the
    /// complete discovery flow a password manager follows.
    /// </summary>
    [Fact]
    public async Task AnonymousWellKnown_RedirectsThroughLoginToChangePassword()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"wellknown-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password, isAdmin: false);

        await page.GotoAsync("/.well-known/change-password");
        await page.Locator("#login-user").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        page.Url.Should().Contain("/login");

        await auth.LoginThroughUiAsync(username, password, "/change-password");

        await page.Locator("#change-password-current").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        page.Url.Should().Contain("/change-password");
    }

    /// <summary>
    /// Verifies that an authenticated user reaches the change-password page through the visible
    /// "Change password" link in the login-status area of the sidebar — the discoverable entry
    /// point for the self-service flow.
    /// </summary>
    [Fact]
    public async Task LoginStatus_ChangePasswordLink_NavigatesToChangePassword()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"pwlink-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password, isAdmin: false);
        await auth.LoginAsync(username, password);

        var link = page.Locator("#change-password-link");
        await link.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await link.ClickAsync();

        await page.Locator("#change-password-current").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        page.Url.Should().Contain("/change-password");
    }

    /// <summary>
    /// Verifies the authenticated password change through the UI: filling current/new/confirmation
    /// shows the success message and the new password authenticates on the next login while the
    /// session stays valid through the reissued auth cookie.
    /// </summary>
    [Fact]
    public async Task AuthenticatedUser_ChangesPassword_ViaUi()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"changepw-user-{Guid.NewGuid():N}";
        const string oldPassword = "Secret123";
        const string newPassword = "NewSecret456";
        await seed.EnsureUserAsync(username, oldPassword, isAdmin: false);
        await auth.LoginAsync(username, oldPassword);

        await page.GotoAsync("/change-password");
        await page.Locator("#change-password-current").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await SubmitUntilFeedbackAsync(page, oldPassword, newPassword, newPassword);

        await page.Locator("div.info").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

        // The reissued cookie keeps the session valid after the security-stamp rotation.
        await page.GotoAsync("/card/setup");
        await page.Locator("button.setup-section-toggle").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        page.Url.Should().NotContain("/login");

        // New password authenticates; old password no longer does.
        await auth.LogoutAsync();
        await auth.LoginAsync(username, newPassword);
        page.Url.Should().NotContain("/login");
    }

    /// <summary>
    /// Verifies that submitting a wrong current password surfaces an inline error and does not
    /// change the password — the user can still sign in with the original password afterwards.
    /// </summary>
    [Fact]
    public async Task ChangePassword_WrongCurrent_ShowsError()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"wrongpw-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password, isAdmin: false);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/change-password");
        await page.Locator("#change-password-current").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await SubmitUntilFeedbackAsync(page, "wrong-password", "NewSecret456", "NewSecret456");

        await page.Locator("div.error").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

        await auth.LogoutAsync();
        await auth.LoginAsync(username, password);
        page.Url.Should().NotContain("/login");
    }

    /// <summary>
    /// Fills the form and clicks submit until the page shows a success or error feedback element.
    /// On a freshly loaded page the Blazor Server circuit may not be connected yet, in which case
    /// early fills and clicks are silently lost and the server-side model stays empty - a submit
    /// then only renders the <c>ValidationSummary</c>. The loop therefore re-fills whenever a
    /// previous attempt produced no result, and skips re-clicking while a request is in flight
    /// (submit button disabled) so a successful change is not overwritten by a duplicate submit.
    /// </summary>
    /// <param name="page">The Playwright page showing the change-password form.</param>
    /// <param name="currentPassword">The value to enter into the current password field.</param>
    /// <param name="newPassword">The value to enter into the new password field.</param>
    /// <param name="confirmPassword">The value to enter into the confirm password field.</param>
    private static async Task SubmitUntilFeedbackAsync(IPage page, string currentPassword, string newPassword, string confirmPassword)
    {
        var feedback = page.Locator("div.info, div.error");
        var submit = page.Locator("button[type=submit]");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            if (await feedback.CountAsync() > 0)
            {
                return;
            }

            if (!await submit.IsDisabledAsync())
            {
                await page.Locator("#change-password-current").FillAsync(currentPassword);
                await page.Locator("#change-password-new").FillAsync(newPassword);
                await page.Locator("#change-password-confirm").FillAsync(confirmPassword);
                await submit.ClickAsync();
            }

            await page.WaitForTimeoutAsync(700);
        }

        (await feedback.CountAsync()).Should().BeGreaterThan(0, "the form submit produced no feedback");
    }
}
