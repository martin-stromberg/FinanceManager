using System.Text.RegularExpressions;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the delete-icon readability fix in the setup backup table: a
/// <c>button.icon-btn.danger</c> must render its glyph in black (<c>color: #000</c>) in the
/// non-hover state so it stays readable on the red danger background.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class SetupBackupTabPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupBackupTabPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public SetupBackupTabPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Creates one backup in the setup "Sicherungen" section and asserts the rendered delete
    /// button's computed text color is black and distinct from its background color.
    /// </summary>
    [Fact]
    public async Task BackupTable_DeleteIcon_IsReadableInNonHoverState()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginAsAdminAsync(page, "backup-delete-icon");

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var toggle = page.Locator("button.setup-section-toggle")
            .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("Sicherung|Backup") });
        (await toggle.CountAsync()).Should().Be(1, "the backup section must exist on the setup card");
        await ExpandSectionAsync(page, toggle);

        var createAction = page.Locator("button#CreateBackup");
        await createAction.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await createAction.ClickAsync();

        var deleteButton = page.Locator(".setup-backup-tab button.icon-btn.danger").First;
        await deleteButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 120_000 });

        var colors = await deleteButton.EvaluateAsync<string>(
            """el => { const s = getComputedStyle(el); return s.color + '|' + s.backgroundColor; }""")
            ?? string.Empty;
        var parts = colors.Split('|');
        parts[0].Should().Be("rgb(0, 0, 0)", "the delete glyph must render black in the non-hover state");
        parts[0].Should().NotBe(parts[1], "icon color must differ from the button background");
    }

    /// <summary>
    /// Clicks the accordion section toggle until <c>aria-expanded</c> reports the expanded state.
    /// Blazor renders boolean-bound attributes only when true (as an empty value) and omits them
    /// when false, so expansion is detected by attribute presence, not by its value.
    /// On a freshly loaded page the Blazor Server circuit may not be connected yet, in which case
    /// the first click is silently lost; the retry loop makes the expansion deterministic.
    /// The expanded check runs before every click, so the section is guaranteed to end expanded.
    /// </summary>
    /// <param name="page">The Playwright page containing the setup accordion.</param>
    /// <param name="toggle">The locator of the accordion section toggle to click.</param>
    private static async Task ExpandSectionAsync(IPage page, ILocator toggle)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            if (await toggle.GetAttributeAsync("aria-expanded") is not null)
            {
                return;
            }

            await toggle.ClickAsync();
            await page.WaitForTimeoutAsync(700);
        }

        (await toggle.GetAttributeAsync("aria-expanded")).Should().NotBeNull("the backup section did not expand");
    }

    private async Task LoginAsAdminAsync(IPage page, string prefix)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        await seeder.EnsureUserAsync(username, password, isAdmin: true, timeZoneId: "UTC");
        await auth.LoginAsync(username, password);
    }
}
