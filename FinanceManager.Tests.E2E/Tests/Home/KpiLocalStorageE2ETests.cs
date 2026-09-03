using System.Text.Json;
using FinanceManager.Shared.Dtos.HomeKpi;
using FinanceManager.Shared.Dtos.Users;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// E2E tests for the optional home page KPI local storage caching feature.
/// Verifies that enabling caching persists KPI data in the browser's localStorage
/// and that disabling the feature removes all cached entries.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class KpiLocalStorageE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="KpiLocalStorageE2ETests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public KpiLocalStorageE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// When the user enables KPI caching, the home page KPI list and numeric tile values
    /// are stored in the browser's localStorage after loading.
    /// </summary>
    [Fact]
    public async Task EnableCache_HomeKpiDataIsPersistedInLocalStorage()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        var auth = new AuthGateway(page, _fixture.BaseUrl);

        var username = $"kpi-cache-on-{Guid.NewGuid():N}";
        const string password = "Secret123";

        var user = await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await EnableKpiCacheAsync(page);
        var kpi = await CreateNumericKpiAsync(page);

        await page.GotoAsync("/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator(".num-kpi .value").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var expectedListKey = $"fm.kpi.{user.Id}.home.kpi.list";
        var expectedNumericKey = $"fm.kpi.{user.Id}.home.kpi.numeric-{kpi.Id}";

        var listValue = await GetLocalStorageItemAsync(page, expectedListKey);
        listValue.Should().NotBeNullOrWhiteSpace("home KPI list should be cached in localStorage");

        var numericValue = await GetLocalStorageItemAsync(page, expectedNumericKey);
        numericValue.Should().NotBeNullOrWhiteSpace("numeric KPI value should be cached in localStorage");
    }

    /// <summary>
    /// When the user disables KPI caching in the profile settings, all previously cached
    /// fm.kpi.* entries are removed from the browser's localStorage.
    /// </summary>
    [Fact]
    public async Task DisableCache_RemovesKpiDataFromLocalStorage()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        var auth = new AuthGateway(page, _fixture.BaseUrl);

        var username = $"kpi-cache-off-{Guid.NewGuid():N}";
        const string password = "Secret123";

        var user = await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await EnableKpiCacheAsync(page);
        await CreateNumericKpiAsync(page);

        await page.GotoAsync("/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator(".num-kpi .value").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var anyKpiKey = await page.EvaluateAsync<bool>("""
            () => Object.keys(localStorage).some(k => k.startsWith('fm.kpi.'))
            """);
        anyKpiKey.Should().BeTrue("cache should contain entries after loading the home page");

        await DisableKpiCacheAsync(page);

        var remainingKey = await page.EvaluateAsync<bool>("""
            () => Object.keys(localStorage).some(k => k.startsWith('fm.kpi.'))
            """);
        remainingKey.Should().BeFalse("disabling cache should remove all fm.kpi.* entries");
    }

    private async Task EnableKpiCacheAsync(IPage page)
    {
        await UpdateProfileCacheSettingAsync(page, true);
    }

    private async Task DisableKpiCacheAsync(IPage page)
    {
        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var setup = new SetupProfileTabPageObject(page, _fixture.BaseUrl);
        await setup.ExpandProfileSectionAsync();

        await page.Locator("input#cache-kpis").UncheckAsync();
        await page.Locator("button#Save:enabled").ClickAsync();

        await page.WaitForFunctionAsync(
            "() => !Object.keys(localStorage).some(k => k.startsWith('fm.kpi.'))",
            null,
            new() { Timeout = 10000 });
    }

    private async Task UpdateProfileCacheSettingAsync(IPage page, bool enabled)
    {
        var request = new UserProfileSettingsUpdateRequest(
            null,
            null,
            null,
            null,
            null,
            enabled);

        var payloadJson = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);

        var status = await page.EvaluateAsync<int>("""
            async ({ payloadJson }) => {
                const response = await fetch('/api/user/settings/profile', {
                    method: 'PUT',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: payloadJson
                });
                return response.status;
            }
            """, new { payloadJson });

        if (status < 200 || status >= 300)
        {
            throw new InvalidOperationException($"Profile update failed with status {status}");
        }
    }

    private async Task<HomeKpiDto> CreateNumericKpiAsync(IPage page)
    {
        return await BrowserApiHelper.PostJsonAsync<HomeKpiCreateRequest, HomeKpiDto>(
            page,
            "/api/home-kpis",
            new HomeKpiCreateRequest(
                HomeKpiKind.Predefined,
                null,
                HomeKpiPredefined.OpenStatementDraftsCount,
                null,
                HomeKpiDisplayMode.TotalOnly,
                0));
    }

    private async Task<string?> GetLocalStorageItemAsync(IPage page, string key)
    {
        return await page.EvaluateAsync<string?>(
            $"() => localStorage.getItem('{key}')");
    }
}
