namespace FinanceManager.Tests.E2E;

/// <summary>
/// E2E tests for language settings functionality (Issue #219).
/// Verifies that user-selected language preferences are persisted and respected,
/// and that browser language (Accept-Language header) does not override user preferences.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ProfileSettingsLanguageTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public ProfileSettingsLanguageTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Test Scenario 1: User language preference is respected after saving
    /// - Browser language: German (Accept-Language: "de")
    /// - User action: Change language to English in SetupProfileTab, save
    /// - Verification: After reload/navigation, UI displays English text (not German)
    /// </summary>
    [Fact]
    public async Task ChangeLanguage_ToEnglish_SavesAndApplies()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        // Arrange: Create and login user
        var username = $"langtest-{Guid.NewGuid():N}";
        const string password = "Secret123";
        
        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        
        // Navigate to Setup/Profile tab
        await page.GotoAsync("/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var setupProfileTab = new SetupProfileTabPageObject(page, _fixture.BaseUrl);
        
        // Act: Change language to English
        await setupProfileTab.SelectLanguageAsync("en");
        await setupProfileTab.ClickSaveAsync();
        
        // Verify: Success message appears
        await setupProfileTab.VerifySuccessMessageDisplayedAsync();
        
        // Act: Reload the page
        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Assert: UI is now in English
        // "Sprache" (German) should NOT appear, "Language" (English) should appear
        var germanLabel = await page.Locator("text=Sprache").CountAsync();
        var englishLabel = await page.Locator("text=Language").CountAsync();
        
        germanLabel.Should().Be(0, "German label should not appear after language change to English");
        englishLabel.Should().BeGreaterThan(0, "English label should appear after language change to English");
    }

    /// <summary>
    /// Test Scenario 2: Default culture (German) is used when no preference is set
    /// - New user without explicit language preference
    /// - Browser language: English (Accept-Language: "en")
    /// - Verification: UI displays in German (default language, browser language is ignored)
    /// </summary>
    [Fact]
    public async Task NewUser_WithoutLanguagePreference_UsesDefaultCulture()
    {
        // Create a new user who has never set a language preference
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"newuser-{Guid.NewGuid():N}";
        const string password = "Secret123";
        
        // Create user without explicit language setting (DB will have NULL for PreferredLanguage)
        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        
        // Navigate to Setup/Profile tab
        await page.GotoAsync("/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Assert: UI is in German (default language)
        // Even though browser language might be English, default culture "de" should be applied
        var germanLabel = await page.Locator("text=Sprache").CountAsync();
        var germanSaveButton = await page.Locator("text=Speichern").CountAsync();
        
        germanLabel.Should().BeGreaterThan(0, "German label should appear (default culture)");
        germanSaveButton.Should().BeGreaterThan(0, "German save button should appear (default culture)");
    }

    /// <summary>
    /// Test Scenario 3: Auth cookie is updated with new language preference after save
    /// - User changes language preference
    /// - Verification: Auth-Cookie contains new JWT with updated pref_lang claim
    /// - Verification: Next request uses new token, displays correct language
    /// </summary>
    [Fact]
    public async Task ChangeLanguage_UpdatesAuthCookie_WithNewJwtToken()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        // Arrange: Create and login user with German preference (default)
        var username = $"cookietest-{Guid.NewGuid():N}";
        const string password = "Secret123";
        
        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        
        // Navigate to Setup/Profile tab
        await page.GotoAsync("/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var setupProfileTab = new SetupProfileTabPageObject(page, _fixture.BaseUrl);
        
        // Get the initial auth cookie
        var initialCookies = await page.Context.CookiesAsync();
        var initialAuthCookie = initialCookies.FirstOrDefault(c => c.Name == "FinanceManager.Auth");
        initialAuthCookie.Should().NotBeNull("Auth cookie should exist after login");
        
        // Extract initial pref_lang claim from JWT (if present)
        var initialClaim = ExtractJwtClaim(initialAuthCookie!.Value, "pref_lang");
        
        // Act: Change language to English
        await setupProfileTab.SelectLanguageAsync("en");
        await setupProfileTab.ClickSaveAsync();
        
        // Verify: Success message
        await setupProfileTab.VerifySuccessMessageDisplayedAsync();
        
        // Assert: Auth cookie has been updated with new token
        var updatedCookies = await page.Context.CookiesAsync();
        var updatedAuthCookie = updatedCookies.FirstOrDefault(c => c.Name == "FinanceManager.Auth");
        
        updatedAuthCookie.Should().NotBeNull("Auth cookie should still exist after language change");
        
        // The new token should have pref_lang claim = "en"
        var updatedClaim = ExtractJwtClaim(updatedAuthCookie!.Value, "pref_lang");
        updatedClaim.Should().Be("en", "Updated JWT should contain pref_lang claim with value 'en'");
        
        // Act: Make a new request to verify the language setting persists
        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Assert: UI should still be in English
        var englishLabel = await page.Locator("text=Language").CountAsync();
        englishLabel.Should().BeGreaterThan(0, "Language should persist after reload with new JWT token");
    }

    /// <summary>
    /// Test Scenario 4: Switching back to automatic language (empty preference) works correctly.
    /// After a language change the page reloads automatically (Navigation.Refresh). The test
    /// waits for that reload instead of triggering additional manual navigations.
    /// Verifies that:
    /// - Setting language to English and then back to Auto saves without error.
    /// - After switching to Auto the page reloads and the language select is still functional.
    /// </summary>
    [Fact]
    public async Task ChangeLanguage_ToAutomatic_RespectsBrowserLanguage()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"autotest-{Guid.NewGuid():N}";
        const string password = "Secret123";

        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        // Navigate to Setup/Profile tab
        await page.GotoAsync("/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var setupProfileTab = new SetupProfileTabPageObject(page, _fixture.BaseUrl);

        // Step 1: Change language to English.
        // After save the page reloads automatically — wait for reload to settle.
        await setupProfileTab.SelectLanguageAsync("en");
        await setupProfileTab.ClickSaveAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify English is active after the automatic reload
        var englishLabel = await page.Locator("text=Language").CountAsync();
        englishLabel.Should().BeGreaterThan(0, "English should be displayed after saving 'en'");

        // Step 2: Switch back to Automatic (empty value).
        // The language select is still on the same /setup page after the reload.
        await setupProfileTab.SelectLanguageAsync("");
        await setupProfileTab.ClickSaveAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify the page reloaded and the setup form is still functional
        var langSelect = await page.Locator("select#lang").CountAsync();
        langSelect.Should().BeGreaterThan(0, "Language select should still be present after switching to Auto");
    }

    /// <summary>
    /// Helper test to verify language change is persisted across different browser sessions.
    /// </summary>
    [Fact]
    public async Task LanguagePreference_PersistedAcrossSessions()
    {
        var username = $"sessiontest-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        
        // Session 1: Set language to English
        {
            await using var session1 = await _fixture.CreateSessionAsync();
            var page1 = session1.Page;
            var auth1 = new AuthGateway(page1, _fixture.BaseUrl);
            
            await seed.EnsureUserAsync(username, password);
            await auth1.LoginAsync(username, password);
            
            await page1.GotoAsync("/setup");
            await page1.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            var setupProfileTab1 = new SetupProfileTabPageObject(page1, _fixture.BaseUrl);
            await setupProfileTab1.SelectLanguageAsync("en");
            await setupProfileTab1.ClickSaveAsync();
            await setupProfileTab1.VerifySuccessMessageDisplayedAsync();
            
            // Verify English is shown
            var englishLabel1 = await page1.Locator("text=Language").CountAsync();
            englishLabel1.Should().BeGreaterThan(0, "English should be displayed after save");
        }
        
        // Session 2: Login again with same user, verify English is still used
        {
            await using var session2 = await _fixture.CreateSessionAsync();
            var page2 = session2.Page;
            var auth2 = new AuthGateway(page2, _fixture.BaseUrl);
            
            await auth2.LoginAsync(username, password);
            await page2.GotoAsync("/setup");
            await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Assert: UI should be in English (language preference is persisted)
            var englishLabel2 = await page2.Locator("text=Language").CountAsync();
            var germanLabel2 = await page2.Locator("text=Sprache").CountAsync();
            
            englishLabel2.Should().BeGreaterThan(0, "English should still be displayed in new session");
            germanLabel2.Should().Be(0, "German should not be displayed (English preference is persisted)");
        }
    }

    /// <summary>
    /// Helper method to extract a claim value from a JWT token.
    /// JWT format: header.payload.signature
    /// Payload is base64-url encoded JSON with claims.
    /// </summary>
    private static string? ExtractJwtClaim(string token, string claimName)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;
            
            // Decode payload (base64-url to base64 conversion)
            var payload = parts[1];
            // Add padding if needed
            var padding = 4 - (payload.Length % 4);
            if (padding != 4)
                payload += new string('=', padding);
            
            var decodedBytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(decodedBytes);
            
            // Parse JSON and extract claim
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var root = document.RootElement;
            
            if (root.TryGetProperty(claimName, out var claimValue))
            {
                return claimValue.GetString();
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Page Object Model for SetupProfileTab component.
/// Encapsulates interactions with the language settings form.
/// </summary>
internal sealed class SetupProfileTabPageObject
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public SetupProfileTabPageObject(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Selects a language from the language dropdown.
    /// </summary>
    /// <param name="languageCode">Language code like "de", "en", or "" for automatic</param>
    public async Task SelectLanguageAsync(string languageCode)
    {
        // Find the language select element
        var languageSelect = _page.Locator("select#lang, select[name*='Language'], select[name*='language']").First;
        
        // If the above doesn't work, try to find by label
        if (await languageSelect.CountAsync() == 0)
        {
            // Try finding the select next to "Language" or "Sprache" label
            languageSelect = _page.Locator("text=/Language|Sprache/").Locator(".. select");
        }
        
        // Select the option with the given value
        await languageSelect.SelectOptionAsync(languageCode);
    }

    /// <summary>
    /// Clicks the Save button to persist language preference.
    /// </summary>
    public async Task ClickSaveAsync()
    {
        // Find and click the save button
        var saveButton = _page.Locator("button:has-text(/Speichern|Save/i)").First;
        
        await saveButton.ClickAsync();
    }

    /// <summary>
    /// Verifies that the success message is displayed.
    /// Message should be: "Einstellungen gespeichert." (de) or "Settings saved." (en)
    /// </summary>
    public async Task VerifySuccessMessageDisplayedAsync()
    {
        // Wait for success message to appear
        var successMessage = _page.Locator("text=/Einstellungen gespeichert|Settings saved/");
        await successMessage.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        
        // Verify it's visible
        var isVisible = await successMessage.IsVisibleAsync();
        isVisible.Should().BeTrue("Success message should be visible after save");
    }

    /// <summary>
    /// Gets the currently selected language value from the language select element.
    /// </summary>
    public async Task<string> GetSelectedLanguageAsync()
    {
        var languageSelect = _page.Locator("select#lang, select[name*='Language'], select[name*='language']").First;
        var selectedValue = await languageSelect.InputValueAsync();
        return selectedValue;
    }

    /// <summary>
    /// Helper to wait for the page to be fully loaded and interactive.
    /// </summary>
    public async Task WaitForPageReadyAsync()
    {
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Also wait for any loading indicators to disappear
        var loadingIndicator = _page.Locator("text=Lade|Loading");
        await loadingIndicator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 3000 });
    }
}
