using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using System.Text.Json;

namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class ReportingFlowPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public ReportingFlowPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that a report favorite can be created and reloaded from the dashboard.
    /// </summary>
    [Fact]
    public async Task SaveFavorite_ShouldPersistAndReload()
    {
        await SaveFavoriteShouldPersistAndReloadAsync(
            () => _fixture.CreateSessionAsync(),
            "report-user",
            "Playwright Favorite");
    }

    [Fact]
    public async Task SaveFavorite_ShouldPersistAndReload_OnMobileViewport()
    {
        await SaveFavoriteShouldPersistAndReloadAsync(
            () => _fixture.CreateMobileSessionAsync(),
            "report-mobile-user",
            "Mobile Favorite");
    }

    private async Task SaveFavoriteShouldPersistAndReloadAsync(
        Func<Task<PlaywrightBrowserSession>> createSessionAsync,
        string userPrefix,
        string favoritePrefix)
    {
        await using var session = await createSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var favoriteName = $"{favoritePrefix} {Guid.NewGuid():N}";
        var created = await BrowserApiHelper.PostJsonAsync<ReportFavoriteCreateApiRequest, ReportFavoriteDto>(page, "/api/report-favorites", new ReportFavoriteCreateApiRequest
        {
            Name = favoriteName,
            PostingKind = 0,
            IncludeCategory = false,
            Interval = 0,
            Take = 6,
            ComparePrevious = false,
            CompareYear = false,
            ShowChart = false,
            Expandable = false,
            UseValutaDate = false
        });
        created.Should().NotBeNull();

        await page.GotoAsync($"/reports/dashboard?favoriteId={created.Id}&edit=false");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var favoriteHeading = page.Locator("h2").Filter(new LocatorFilterOptions { HasTextString = favoriteName });
        await favoriteHeading.WaitForAsync();
        var heading = await favoriteHeading.TextContentAsync();
        heading.Should().Contain(favoriteName);

        await page.ReloadAsync();
        await favoriteHeading.WaitForAsync();
        heading = await favoriteHeading.TextContentAsync();
        heading.Should().Contain(favoriteName);
    }

    [Fact]
    public async Task CreateBackup_EditMasterData_AndRestoreBackup_ShouldRestoreOriginalState()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"backup-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var contact = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(
            page,
            "/api/contacts",
            new ContactCreateRequest($"Backup Kontakt {Guid.NewGuid():N}", ContactType.Other, null, null, false));
        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest($"Backup Konto {Guid.NewGuid():N}", AccountType.Giro, "DE50700500000007882997", null, "Backup Bank", null, SavingsPlanExpectation.Optional, true));
        var savingsPlan = await BrowserApiHelper.PostJsonAsync<SavingsPlanCreateRequest, SavingsPlanDto>(
            page,
            "/api/savings-plans",
            new SavingsPlanCreateRequest($"Backup Plan {Guid.NewGuid():N}", SavingsPlanType.OneTime, 250m, DateTime.UtcNow.Date.AddMonths(8), null, null, null));
        var security = await BrowserApiHelper.PostJsonAsync<SecurityRequest, SecurityDto>(
            page,
            "/api/securities",
            new SecurityRequest { Name = $"Backup Security {Guid.NewGuid():N}", Identifier = $"BCK-{Guid.NewGuid():N}", CurrencyCode = "EUR" });

        var backupCreate = await BrowserApiHelper.PostWithStatusAsync<JsonElement>(page, "/api/setup/backups");
        backupCreate.Status.Should().Be(200);
        var backupId = GetGuid(backupCreate.Value, "id");
        var backupFileName = GetString(backupCreate.Value, "fileName");

        await BrowserApiHelper.PutJsonAsync<ContactUpdateRequest, ContactDto>(
            page,
            $"/api/contacts/{contact.Id}",
            new ContactUpdateRequest("Kontakt Nach Backup", ContactType.Other, null, "changed", false));
        await BrowserApiHelper.PostNoContentAsync(page, $"/api/savings-plans/{savingsPlan.Id}/archive");
        var deleteSavingsStatus = await BrowserApiHelper.DeleteAsync(page, $"/api/savings-plans/{savingsPlan.Id}");
        deleteSavingsStatus.Should().BeOneOf(200, 204);
        await BrowserApiHelper.PostNoContentAsync(page, $"/api/securities/{security.Id}/archive");
        var deleteSecurityStatus = await BrowserApiHelper.DeleteAsync(page, $"/api/securities/{security.Id}");
        deleteSecurityStatus.Should().BeOneOf(200, 204);

        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/setup/backups/{backupId}/apply",
            new BackupRestoreRequestDto(backupFileName, backupFileName));

        var restoredContacts = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<ContactDto>>(page, "/api/contacts?all=true");
        restoredContacts.Should().Contain(c => c.Name == contact.Name);

        var restoredSavings = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<SavingsPlanDto>>(page, "/api/savings-plans?onlyActive=false");
        restoredSavings.Should().Contain(s => s.Name == savingsPlan.Name);

        var restoredSecurities = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<SecurityDto>>(page, "/api/securities?onlyActive=false");
        restoredSecurities.Should().Contain(s => s.Name == security.Name);

        var restoredAccounts = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<AccountDto>>(page, "/api/accounts");
        restoredAccounts.Should().Contain(a => a.Name == account.Name);
    }

    private static Guid GetGuid(JsonElement? element, string propertyName)
    {
        if (element is null)
        {
            throw new InvalidOperationException("Missing JSON payload.");
        }

        var value = element.Value;
        if (value.TryGetProperty(propertyName, out var id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var guid))
        {
            return guid;
        }

        if (value.TryGetProperty(char.ToUpperInvariant(propertyName[0]) + propertyName[1..], out id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out guid))
        {
            return guid;
        }

        throw new InvalidOperationException($"Property '{propertyName}' with a GUID value was not found.");
    }

    private static string GetString(JsonElement? element, string propertyName)
    {
        if (element is null)
        {
            throw new InvalidOperationException("Missing JSON payload.");
        }

        var value = element.Value;
        if (value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? throw new InvalidOperationException($"Property '{propertyName}' was null.");
        }

        if (value.TryGetProperty(char.ToUpperInvariant(propertyName[0]) + propertyName[1..], out property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? throw new InvalidOperationException($"Property '{propertyName}' was null.");
        }

        throw new InvalidOperationException($"Property '{propertyName}' with a string value was not found.");
    }
}
